using System;
using System.Security.Cryptography;
using System.Text;
using BenchmarkDotNet.Attributes;
using WhisperKey.Models;
using WhisperKey.Services;
using WhisperKey.Services.Database;
using Microsoft.Extensions.Logging.Abstractions;

namespace WhisperKey.Benchmarks
{
    [MemoryDiagnoser]
    public class SecurityBenchmarks
    {
        private AuditLoggingService _auditService = null!;
        private string _testLogDir = null!;

        [GlobalSetup]
        public void Setup()
        {
            _testLogDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WhisperKeyBenchmarks_" + Guid.NewGuid().ToString());
            System.IO.Directory.CreateDirectory(_testLogDir);
            _auditService = new AuditLoggingService(NullLogger<AuditLoggingService>.Instance, new NullAuditRepository(), _testLogDir);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            if (System.IO.Directory.Exists(_testLogDir))
            {
                try
                {
                    System.IO.Directory.Delete(_testLogDir, true);
                }
                catch { }
            }
        }

        [Benchmark]
        public async System.Threading.Tasks.Task LogSecurityEvent()
        {
            await _auditService.LogEventAsync(
                AuditEventType.SecurityEvent,
                "Benchmark security event",
                "{\"benchmark\": true}",
                DataSensitivity.Medium);
        }

        [Benchmark]
        public string HashValue()
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes("User123_MachineABC_2026");
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }
    }
}
