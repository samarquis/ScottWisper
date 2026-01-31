using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ScottWisper.Services;

namespace ScottWisper.Validation
{
    public interface ICrossApplicationValidator
    {
        Task<ScottWisper.CrossApplicationValidationResult> ValidateCrossApplicationInjectionAsync();
    }
}
