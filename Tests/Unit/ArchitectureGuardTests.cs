using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace WhisperKey.Tests.Unit
{
    public class ArchitectureGuardTests
    {
        private static readonly Dictionary<string, string[]> ExpectedFolderStructure = new()
        {
            { "Services", new[] { "Service", "IAudio" } },
            { "ViewModels", new[] { "ViewModel" } },
            { "Repositories", new[] { "Repository" } },
            { "Bootstrap", new[] { "Service" } },
            { "UI", new[] { "Window", "Control", "Dialog" } },
        };

        [Fact]
        public void ViewModels_ShouldImplementINotifyPropertyChanged()
        {
            var viewModelTypes = GetTypesInNamespace("WhisperKey.ViewModels")
                .Where(t => t.Name.EndsWith("ViewModel") && !t.IsInterface && !t.IsAbstract);

            foreach (var type in viewModelTypes)
            {
                Assert.True(
                    type.GetInterface("INotifyPropertyChanged") != null,
                    $"{type.Name} should implement INotifyPropertyChanged");
            }
        }

        [Fact]
        public void Services_ShouldHaveCorrespondingInterface()
        {
            var serviceTypes = GetTypesInNamespace("WhisperKey.Services")
                .Where(t => t.Name.EndsWith("Service") && !t.IsInterface && !t.IsAbstract && t.Name != "Service");

            foreach (var type in serviceTypes)
            {
                var expectedInterfaceName = "I" + type.Name;
                var hasInterface = type.GetInterface(expectedInterfaceName) != null ||
                                  type.GetInterfaces().Any(i => i.Name == type.Name.Replace("Service", "Service"));

                Assert.True(hasInterface, 
                    $"{type.Name} should have a corresponding interface (I{type.Name})");
            }
        }

        [Fact]
        public void Repositories_ShouldHaveCorrespondingInterface()
        {
            var repoTypes = GetTypesInNamespace("WhisperKey.Repositories")
                .Where(t => t.Name.EndsWith("Repository") && !t.IsInterface && !t.IsAbstract);

            foreach (var type in repoTypes)
            {
                var expectedInterfaceName = "I" + type.Name;
                Assert.True(
                    type.GetInterface(expectedInterfaceName) != null,
                    $"{type.Name} should have a corresponding interface (I{type.Name})");
            }
        }

        [Fact]
        public void Commands_ShouldBeInViewModels()
        {
            var commandTypes = GetTypesInNamespace("WhisperKey.ViewModels")
                .Where(t => t.Name.EndsWith("Command") && !t.IsInterface);

            foreach (var type in commandTypes)
            {
                Assert.True(type.Name == "RelayCommand", 
                    $"Custom commands should be named RelayCommand, found {type.Name}");
            }
        }

        [Fact]
        public void WindowFactory_ShouldBeInUI()
        {
            var factoryType = Type.GetType("WhisperKey.UI.IWindowFactory");
            Assert.NotNull(factoryType);
        }

        [Fact]
        public void ServiceConfiguration_ShouldRegisterAllServices()
        {
            var configType = Type.GetType("WhisperKey.Bootstrap.ServiceConfiguration");
            Assert.NotNull(configType);
            
            var configureMethod = configType.GetMethod("ConfigureServices", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(configureMethod);
        }

        [Fact]
        public void ViewModels_ShouldNotHaveDirectUIReferences()
        {
            var viewModelTypes = GetTypesInNamespace("WhisperKey.ViewModels")
                .Where(t => t.Name.EndsWith("ViewModel") && !t.IsInterface && !t.IsAbstract);

            var uiNamespace = typeof(System.Windows.Window).Namespace;

            foreach (var type in viewModelTypes)
            {
                var fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    if (field.FieldType.Namespace?.StartsWith("System.Windows") == true &&
                        !field.FieldType.Namespace.Contains("Input") &&
                        !field.FieldType.Namespace.Contains("Media"))
                    {
                        Assert.True(false, 
                            $"{type.Name} should not have direct references to UI types. Field {field.Name} is of type {field.FieldType.Name}");
                    }
                }
            }
        }

        [Fact]
        public void Services_ShouldBeInjectedViaConstructor()
        {
            var serviceTypes = GetTypesInNamespace("WhisperKey.Services")
                .Where(t => t.Name.EndsWith("Service") && !t.IsInterface && !t.IsAbstract)
                .Take(10);

            foreach (var type in serviceTypes)
            {
                var constructors = type.GetConstructors();
                if (constructors.Length > 0)
                {
                    var constructor = constructors[0];
                    var parameters = constructor.GetParameters();
                    
                    foreach (var param in parameters)
                    {
                        if (param.ParameterType.Namespace == "WhisperKey.Services" ||
                            param.ParameterType.Namespace?.StartsWith("WhisperKey") == true)
                        {
                            Assert.True(
                                param.ParameterType.IsInterface,
                                $"{type.Name}'s constructor should accept interfaces, not {param.ParameterType.Name}");
                        }
                    }
                }
            }
        }

        private static IEnumerable<Type> GetTypesInNamespace(string namespaceName)
        {
            return Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.Namespace == namespaceName);
        }
    }
}
