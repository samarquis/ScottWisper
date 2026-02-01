using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WhisperKey.Services;

namespace WhisperKey.Validation
{
    public interface ICrossApplicationValidator
    {
        Task<WhisperKey.CrossApplicationValidationResult> ValidateCrossApplicationInjectionAsync();
    }
}
