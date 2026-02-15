using System;
using System.Windows;
using System.Windows.Media;
using WhisperKey.Configuration;
using WhisperKey.Services;

namespace WhisperKey.Services
{
    public interface IThemeService
    {
        string CurrentTheme { get; }
        void Initialize();
        void ApplyTheme(string theme);
        void ApplyDarkMode();
        void ApplyLightMode();
    }

    public class ThemeService : IThemeService
    {
        private readonly ISettingsService _settingsService;
        private string _currentTheme = "light";

        public string CurrentTheme => _currentTheme;

        public ThemeService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public void Initialize()
        {
            var theme = _settingsService?.Settings?.UI?.Theme ?? "light";
            ApplyTheme(theme);
        }

        public void ApplyTheme(string theme)
        {
            _currentTheme = theme?.ToLowerInvariant() ?? "light";

            switch (_currentTheme)
            {
                case "dark":
                    ApplyDarkMode();
                    break;
                case "light":
                default:
                    ApplyLightMode();
                    break;
            }

            _settingsService.Settings.UI.Theme = _currentTheme;
            _ = _settingsService.SaveAsync();
        }

        public void ApplyDarkMode()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var resources = Application.Current.Resources;

                resources["PrimaryBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
                resources["DarkBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0x2D, 0x2D));
                resources["TextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
                resources["TextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0xAB, 0xAB, 0xAB));
                resources["TextMutedBrush"] = new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x6E));

                resources["SuccessBrush"] = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
                resources["InfoBrush"] = new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3));
                resources["WarningBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07));
                resources["DangerBrush"] = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));

                _currentTheme = "dark";
            });
        }

        public void ApplyLightMode()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var resources = Application.Current.Resources;

                resources["PrimaryBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0xF8, 0xF9, 0xFA));
                resources["DarkBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0x2D, 0x2D));
                resources["TextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30));
                resources["TextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0x6C, 0x75, 0x7D));
                resources["TextMutedBrush"] = new SolidColorBrush(Color.FromRgb(0xAD, 0xB5, 0xBD));

                resources["SuccessBrush"] = new SolidColorBrush(Color.FromRgb(0x28, 0xA7, 0x45));
                resources["InfoBrush"] = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC));
                resources["WarningBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07));
                resources["DangerBrush"] = new SolidColorBrush(Color.FromRgb(0xDC, 0x35, 0x45));

                _currentTheme = "light";
            });
        }
    }
}
