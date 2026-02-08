using System;
using System.Threading.Tasks;
using WhisperKey.Services.Validation;

namespace WhisperKey.Services.Validation
{
    /// <summary>
    /// Interface for validating audio data streams and files
    /// </summary>
    public interface IAudioValidationProvider
    {
        /// <summary>
        /// Validates raw audio byte data
        /// </summary>
        /// <param name="audioData">The audio data to validate</param>
        /// <param name="format">Expected format (e.g., "wav")</param>
        /// <returns>Validation result indicating success or failure with errors</returns>
        ValidationResult ValidateAudioData(byte[] audioData, string format = "wav");

        /// <summary>
        /// Validates an audio file
        /// </summary>
        /// <param name="filePath">Path to the audio file</param>
        /// <returns>Validation result</returns>
        Task<ValidationResult> ValidateAudioFileAsync(string filePath);
    }
}
