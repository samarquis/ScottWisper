using System;
using System.Text.RegularExpressions;

namespace WhisperKey.Services.Validation
{
    /// <summary>
    /// Strategy pattern for validating API keys for different providers
    /// </summary>
    public interface IApiKeyValidationStrategy
    {
        /// <summary>
        /// Provider name this strategy validates
        /// </summary>
        string ProviderName { get; }
        
        /// <summary>
        /// Validates an API key for this provider
        /// </summary>
        bool IsValid(string apiKey);
        
        /// <summary>
        /// Gets a user-friendly error message for invalid keys
        /// </summary>
        string GetValidationErrorMessage();
    }

    /// <summary>
    /// Validates OpenAI API keys (starts with sk-, minimum 20 chars)
    /// </summary>
    public class OpenAIApiKeyValidationStrategy : IApiKeyValidationStrategy
    {
        public string ProviderName => "openai";
        
        public bool IsValid(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return false;
                
            return apiKey.StartsWith("sk-") && apiKey.Length >= 20;
        }
        
        public string GetValidationErrorMessage()
        {
            return "OpenAI API key must start with 'sk-' and be at least 20 characters long.";
        }
    }

    /// <summary>
    /// Validates Azure API keys (GUID format or minimum 32 chars)
    /// </summary>
    public class AzureApiKeyValidationStrategy : IApiKeyValidationStrategy
    {
        public string ProviderName => "azure";
        
        public bool IsValid(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return false;
                
            // Azure keys can be GUIDs or long strings
            return (Guid.TryParse(apiKey, out _) || apiKey.Length >= 32);
        }
        
        public string GetValidationErrorMessage()
        {
            return "Azure API key must be a valid GUID or at least 32 characters long.";
        }
    }

    /// <summary>
    /// Validates Google API keys (minimum 30 chars)
    /// </summary>
    public class GoogleApiKeyValidationStrategy : IApiKeyValidationStrategy
    {
        public string ProviderName => "google";
        
        public bool IsValid(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return false;
                
            return apiKey.Length >= 30;
        }
        
        public string GetValidationErrorMessage()
        {
            return "Google API key must be at least 30 characters long.";
        }
    }

    /// <summary>
    /// Default validation strategy for unknown providers
    /// </summary>
    public class DefaultApiKeyValidationStrategy : IApiKeyValidationStrategy
    {
        public string ProviderName => "default";
        
        public bool IsValid(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return false;
                
            return apiKey.Length >= 10;
        }
        
        public string GetValidationErrorMessage()
        {
            return "API key must be at least 10 characters long.";
        }
    }

    /// <summary>
    /// Factory for getting the appropriate validation strategy
    /// </summary>
    public static class ApiKeyValidationFactory
    {
        private static readonly System.Collections.Generic.Dictionary<string, IApiKeyValidationStrategy> _strategies = new()
        {
            ["openai"] = new OpenAIApiKeyValidationStrategy(),
            ["azure"] = new AzureApiKeyValidationStrategy(),
            ["google"] = new GoogleApiKeyValidationStrategy()
        };
        
        private static readonly IApiKeyValidationStrategy _defaultStrategy = new DefaultApiKeyValidationStrategy();
        
        /// <summary>
        /// Gets the validation strategy for a specific provider
        /// </summary>
        public static IApiKeyValidationStrategy GetStrategy(string providerName)
        {
            var normalizedProvider = providerName?.ToLowerInvariant() ?? "default";
            return _strategies.TryGetValue(normalizedProvider, out var strategy) 
                ? strategy 
                : _defaultStrategy;
        }
        
        /// <summary>
        /// Registers a custom validation strategy
        /// </summary>
        public static void RegisterStrategy(IApiKeyValidationStrategy strategy)
        {
            _strategies[strategy.ProviderName.ToLowerInvariant()] = strategy;
        }
    }
}
