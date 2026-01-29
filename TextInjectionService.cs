using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ScottWisper.Services;

namespace ScottWisper
{
    /// <summary>
    /// Legacy TextInjectionService for backward compatibility
    /// Delegates to the new service in src/Services/
    /// </summary>
    public class TextInjectionService : Services.TextInjectionService
    {
        public TextInjectionService(ILogger<TextInjectionService>? logger = null) 
            : base(logger)
        {
        }
    }
}