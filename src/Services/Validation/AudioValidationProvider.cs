using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Models;

namespace WhisperKey.Services.Validation
{
    /// <summary>
    /// Implementation of audio validation following NIST SI-10 controls
    /// </summary>
    public class AudioValidationProvider : IAudioValidationProvider
    {
        private readonly ILogger<AudioValidationProvider> _logger;
        private readonly IAuditLoggingService _auditService;
        
        // Maximum audio file size (25MB - OpenAI API limit)
        private const int MaxAudioSizeBytes = 25 * 1024 * 1024;

        public AudioValidationProvider(ILogger<AudioValidationProvider> logger, IAuditLoggingService auditService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        }

        public ValidationResult ValidateAudioData(byte[] audioData, string format = "wav")
        {
            var result = new ValidationResult();

            if (audioData == null)
            {
                result.AddError("Audio data cannot be null");
                return LogAndReturn(result);
            }

            if (audioData.Length == 0)
            {
                result.AddError("Audio data cannot be empty");
                return LogAndReturn(result);
            }

            // Enforce maximum file size (SI-10 / DoS protection)
            if (audioData.Length > MaxAudioSizeBytes)
            {
                result.AddError($"Audio data size ({audioData.Length} bytes) exceeds maximum allowed size ({MaxAudioSizeBytes} bytes)");
            }

            if (format.ToLower() == "wav")
            {
                ValidateWavHeader(audioData, result);
            }
            else
            {
                // NIST SI-10: Restrict allowed file types
                result.AddError($"Unsupported audio format: {format}. Only WAV is allowed for security validation.");
            }

            return LogAndReturn(result);
        }

        public async Task<ValidationResult> ValidateAudioFileAsync(string filePath)
        {
            var result = new ValidationResult();

            if (string.IsNullOrEmpty(filePath))
            {
                result.AddError("File path cannot be null or empty");
                return LogAndReturn(result);
            }

            if (!File.Exists(filePath))
            {
                result.AddError($"Audio file not found: {filePath}");
                return LogAndReturn(result);
            }

            try
            {
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > MaxAudioSizeBytes)
                {
                    result.AddError($"Audio file size ({fileInfo.Length} bytes) exceeds maximum allowed size ({MaxAudioSizeBytes} bytes)");
                }

                var audioData = await File.ReadAllBytesAsync(filePath).ConfigureAwait(false);
                var extension = Path.GetExtension(filePath).TrimStart('.').ToLower();
                
                var dataResult = ValidateAudioData(audioData, extension == "wav" ? "wav" : extension);
                if (!dataResult.IsValid)
                {
                    foreach (var error in dataResult.Errors)
                    {
                        result.AddError(error);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading audio file for validation: {FilePath}", filePath);
                result.AddError($"Failed to read audio file: {ex.Message}");
            }

            return LogAndReturn(result);
        }

        private void ValidateWavHeader(byte[] audioData, ValidationResult result)
        {
            // Verify WAV format headers (minimum 44 bytes for standard WAV header)
            if (audioData.Length < 44)
            {
                result.AddError($"Audio data too small to be a valid WAV file ({audioData.Length} bytes)");
                return;
            }

            // Check RIFF header (Magic Number check - NIST SI-10)
            if (audioData[0] != 'R' || audioData[1] != 'I' || audioData[2] != 'F' || audioData[3] != 'F')
            {
                result.AddError("Invalid audio format: missing RIFF header");
            }

            // Check WAVE format
            if (audioData[8] != 'W' || audioData[9] != 'A' || audioData[10] != 'V' || audioData[11] != 'E')
            {
                result.AddError("Invalid audio format: not a valid WAVE file");
            }

            // Check fmt subchunk
            if (audioData[12] != 'f' || audioData[13] != 'm' || audioData[14] != 't' || audioData[15] != ' ')
            {
                result.AddError("Invalid audio format: missing fmt subchunk");
            }

            // Verify audio format is PCM (1) or IEEE Float (3)
            ushort audioFormat = BitConverter.ToUInt16(audioData, 20);
            if (audioFormat != 1 && audioFormat != 3)
            {
                result.AddError($"Unsupported audio format code: {audioFormat}. Only PCM or IEEE Float are supported.");
            }

            // Check data subchunk marker location (Magic Number check - NIST SI-10)
            // We search for 'data' subchunk to handle optional chunks (like JUNK or LIST)
            bool foundDataChunk = false;
            int offset = 12; // Start after 'WAVE'
            
            while (offset < audioData.Length - 8)
            {
                string chunkId = System.Text.Encoding.ASCII.GetString(audioData, offset, 4);
                uint chunkSize = BitConverter.ToUInt32(audioData, offset + 4);
                
                if (chunkId == "data")
                {
                    foundDataChunk = true;
                    // Validate that the reported data size doesn't exceed the actual remaining data
                    if (offset + 8 + chunkSize > audioData.Length)
                    {
                        result.AddError("Invalid WAV file: data chunk size exceeds file length");
                    }
                    break;
                }
                
                offset += 8 + (int)chunkSize;
                
                // Safety break to prevent infinite loops on malformed files
                if (offset < 0 || offset > audioData.Length) break;
            }

            if (!foundDataChunk)
            {
                result.AddError("Invalid audio format: missing data chunk");
            }
        }

        private ValidationResult LogAndReturn(ValidationResult result)
        {
            if (!result.IsValid)
            {
                var errorMessage = $"Audio validation failed: {string.Join(", ", result.Errors)}";
                _logger.LogWarning(errorMessage);
                
                // NIST SI-12: Audit validation failures
                _ = _auditService.LogEventAsync(
                    AuditEventType.SecurityEvent,
                    errorMessage,
                    null,
                    DataSensitivity.Low);
            }
            return result;
        }
    }
}
