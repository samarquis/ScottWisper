using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WhisperKey.Services;

namespace WhisperKey
{
    public class CostTrackingService : IDisposable
    {
        private readonly string _usageDataPath;
        private UsageData _usageData = null!;
        private readonly object _lockObject = new object();
        private readonly Timer _saveTimer;
        private readonly ISettingsService? _settingsService;
        
        // Pricing configuration
        private const decimal CostPerMinute = 0.006m; // $0.006 per minute for Whisper API
        private const decimal FreeTierLimit = 5.00m; // $5.00 monthly free tier
        private const decimal WarningThreshold = 0.80m; // Warn at 80% of free tier
        
        // Constants for usage calculation
        private const int BytesPerSecond = 32000; // 16kHz, 16-bit, mono
        
        public event EventHandler<UsageStats>? UsageUpdated;
        public event EventHandler<FreeTierWarning>? FreeTierWarning;
        public event EventHandler<FreeTierExceeded>? FreeTierExceeded;

        public CostTrackingService()
        {
            _usageDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WhisperKey",
                "usage.json"
            );
            
            // Initialize synchronously for constructor - async version available for explicit init
            LoadUsageData();
            
            // Auto-save timer - saves every minute
            _saveTimer = new Timer(SaveUsageDataCallback, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }
        
        public CostTrackingService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            
            _usageDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WhisperKey",
                "usage.json"
            );
            
            // Initialize synchronously for constructor - async version available for explicit init
            LoadUsageData();
            
            // Subscribe to settings changes
            if (_settingsService != null)
            {
                _settingsService.SettingsChanged += OnSettingsChanged;
            }
            
            // Auto-save timer - saves every minute
            _saveTimer = new Timer(SaveUsageDataCallback, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        /// <summary>
        /// Asynchronously initializes the service by loading usage data.
        /// Call this after construction if you want async initialization.
        /// </summary>
        public async Task InitializeAsync()
        {
            await LoadUsageDataAsync().ConfigureAwait(false);
        }

        private async void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
        {
            // Handle cost tracking settings changes
            if (e.Category == "CostTracking" || e.Category == "Transcription")
            {
                // Settings like cost thresholds, free tier limits, etc. would be applied here
                return; // No additional processing needed for cost tracking changes
            }
        }

        public async Task TrackUsage(int bytes, bool success)
        {
            await Task.Run(() => {
                // Implementation
            });
        }

        public UsageStats GetUsageStats()
        {
            lock (_lockObject)
            {
                var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var monthlyUsage = CalculateMonthlyUsage(currentMonth);

                return new UsageStats
                {
                    RequestCount = _usageData.TotalRequests,
                    FailedRequests = _usageData.FailedRequests,
                    EstimatedCost = _usageData.TotalCost,
                    EstimatedMinutes = _usageData.TotalMinutes,
                    MonthlyUsage = monthlyUsage,
                    RemainingFreeTier = Math.Max(0, FreeTierLimit - monthlyUsage.Cost),
                    FreeTierPercentage = Math.Min(100, (double)(monthlyUsage.Cost / FreeTierLimit) * 100),
                    DailyUsage = _usageData.DailyUsage.Values.ToArray()
                };
            }
        }

        public void ResetUsageData()
        {
            lock (_lockObject)
            {
                _usageData = new UsageData
                {
                    CreatedAt = DateTime.Now,
                    TotalRequests = 0,
                    FailedRequests = 0,
                    TotalMinutes = 0,
                    TotalCost = 0,
                    DailyUsage = new Dictionary<DateTime, DailyUsage>()
                };
                
                // Synchronous save for backward compatibility
                SaveUsageData(null);
                UpdateAndNotify();
            }
        }

        public async Task ResetUsageDataAsync()
        {
            lock (_lockObject)
            {
                _usageData = new UsageData
                {
                    CreatedAt = DateTime.Now,
                    TotalRequests = 0,
                    FailedRequests = 0,
                    TotalMinutes = 0,
                    TotalCost = 0,
                    DailyUsage = new Dictionary<DateTime, DailyUsage>()
                };
            }
            
            await SaveUsageDataAsync().ConfigureAwait(false);
            UpdateAndNotify();
        }

        public UsageReport GenerateReport(ReportPeriod period)
        {
            lock (_lockObject)
            {
                var endDate = DateTime.Today;
                var startDate = period switch
                {
                    ReportPeriod.Daily => endDate.AddDays(-7),
                    ReportPeriod.Weekly => endDate.AddDays(-30),
                    ReportPeriod.Monthly => endDate.AddDays(-90),
                    _ => endDate.AddDays(-30)
                };

                var reportData = new List<DailyUsage>();
                
                foreach (var dailyEntry in _usageData.DailyUsage)
                {
                    if (dailyEntry.Key >= startDate && dailyEntry.Key <= endDate)
                    {
                        reportData.Add(dailyEntry.Value);
                    }
                }

                return new UsageReport
                {
                    Period = period,
                    StartDate = startDate,
                    EndDate = endDate,
                    TotalRequests = reportData.Sum(d => d.Requests),
                    TotalMinutes = reportData.Sum(d => d.Minutes),
                    TotalCost = reportData.Sum(d => d.Cost),
                    AverageDailyMinutes = reportData.Count > 0 ? reportData.Average(d => d.Minutes) : 0,
                    DailyUsage = reportData.OrderBy(d => d.Date).ToArray()
                };
            }
        }

        private void CheckFreeTierLimits()
        {
            var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var monthlyUsage = CalculateMonthlyUsage(currentMonth);
            var usagePercentage = (double)(monthlyUsage.Cost / FreeTierLimit) * 100;

            if (usagePercentage >= 100.0 && !_usageData.FreeTierExceededNotified)
            {
                _usageData.FreeTierExceededNotified = true;
                FreeTierExceeded?.Invoke(this, new FreeTierExceeded
                {
                    MonthlyUsage = monthlyUsage,
                    Limit = FreeTierLimit
                });
            }
            else if (usagePercentage >= (double)WarningThreshold && !_usageData.FreeTierWarningNotified)
            {
                _usageData.FreeTierWarningNotified = true;
                FreeTierWarning?.Invoke(this, new FreeTierWarning
                {
                    MonthlyUsage = monthlyUsage,
                    Limit = FreeTierLimit,
                    UsagePercentage = usagePercentage
                });
            }
            else if (usagePercentage < (double)WarningThreshold)
            {
                // Reset warning notifications if usage drops below threshold
                _usageData.FreeTierWarningNotified = false;
            }
        }

        private MonthlyUsage CalculateMonthlyUsage(DateTime monthStart)
        {
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var monthlyUsage = new MonthlyUsage
            {
                Month = monthStart,
                Requests = 0,
                Minutes = 0,
                Cost = 0
            };

            foreach (var dailyEntry in _usageData.DailyUsage)
            {
                if (dailyEntry.Key >= monthStart && dailyEntry.Key <= monthEnd)
                {
                    monthlyUsage.Requests += dailyEntry.Value.Requests;
                    monthlyUsage.Minutes += dailyEntry.Value.Minutes;
                    monthlyUsage.Cost += dailyEntry.Value.Cost;
                }
            }

            return monthlyUsage;
        }

        private void LoadUsageData()
        {
            // Synchronous wrapper that blocks on the async operation
            // For true async, use LoadUsageDataAsync() with await
            LoadUsageDataAsync().GetAwaiter().GetResult();
        }

        private async Task LoadUsageDataAsync()
        {
            try
            {
                if (File.Exists(_usageDataPath))
                {
                    var json = await File.ReadAllTextAsync(_usageDataPath).ConfigureAwait(false);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    
                    var loadedData = JsonSerializer.Deserialize<UsageData>(json, options);
                    if (loadedData != null)
                    {
                        _usageData = loadedData;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but continue with default data
                System.Diagnostics.Debug.WriteLine($"Error loading usage data: {ex.Message}");
            }

            // Create new usage data if loading failed
            _usageData = new UsageData
            {
                CreatedAt = DateTime.Now,
                TotalRequests = 0,
                FailedRequests = 0,
                TotalMinutes = 0,
                TotalCost = 0,
                DailyUsage = new Dictionary<DateTime, DailyUsage>()
            };
        }

        private void SaveUsageData(object? state)
        {
            // Synchronous wrapper for backward compatibility
            // For async operations from timer, use SaveUsageDataAsync directly
            SaveUsageDataAsync().GetAwaiter().GetResult();
        }

        private void SaveUsageDataCallback(object? state)
        {
            // Fire-and-forget async save for timer callback
            // Using ConfigureAwait(false) since we're on a background thread
            _ = SaveUsageDataAsync().ConfigureAwait(false);
        }

        private async Task SaveUsageDataAsync()
        {
            try
            {
                var directory = Path.GetDirectoryName(_usageDataPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNameCaseInsensitive = true
                };

                var json = JsonSerializer.Serialize(_usageData, options);
                await File.WriteAllTextAsync(_usageDataPath, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving usage data: {ex.Message}");
            }
        }

        private void UpdateAndNotify()
        {
            UsageUpdated?.Invoke(this, GetUsageStats());
        }

        public void Dispose()
        {
            // Unsubscribe from settings changes to prevent memory leak
            if (_settingsService != null)
            {
                _settingsService.SettingsChanged -= OnSettingsChanged;
            }
            
            _saveTimer?.Dispose();
            // Fire-and-forget async save on dispose
            _ = SaveUsageDataAsync().ConfigureAwait(false);
        }
    }

    // Data models
    public class UsageData
    {
        public DateTime CreatedAt { get; set; }
        public int TotalRequests { get; set; }
        public int FailedRequests { get; set; }
        public double TotalMinutes { get; set; }
        public decimal TotalCost { get; set; }
        public Dictionary<DateTime, DailyUsage> DailyUsage { get; set; } = new();
        public bool FreeTierWarningNotified { get; set; }
        public bool FreeTierExceededNotified { get; set; }
    }

    public class DailyUsage
    {
        public DateTime Date { get; set; }
        public int Requests { get; set; }
        public double Minutes { get; set; }
        public decimal Cost { get; set; }
    }

    public class UsageStats
    {
        public int RequestCount { get; set; }
        public int FailedRequests { get; set; }
        public decimal EstimatedCost { get; set; }
        public double EstimatedMinutes { get; set; }
        public MonthlyUsage MonthlyUsage { get; set; } = new();
        public decimal RemainingFreeTier { get; set; }
        public double FreeTierPercentage { get; set; }
        public DailyUsage[] DailyUsage { get; set; } = Array.Empty<DailyUsage>();
        
        public override string ToString()
        {
            return $"Requests: {RequestCount}, Cost: ${EstimatedCost:F4}, Minutes: {EstimatedMinutes:F2}";
        }
    }

    public class MonthlyUsage
    {
        public DateTime Month { get; set; }
        public int Requests { get; set; }
        public double Minutes { get; set; }
        public decimal Cost { get; set; }
    }

    public class UsageReport
    {
        public ReportPeriod Period { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalRequests { get; set; }
        public double TotalMinutes { get; set; }
        public decimal TotalCost { get; set; }
        public double AverageDailyMinutes { get; set; }
        public DailyUsage[] DailyUsage { get; set; } = Array.Empty<DailyUsage>();
    }

    public class FreeTierWarning
    {
        public MonthlyUsage MonthlyUsage { get; set; } = new();
        public decimal Limit { get; set; }
        public double UsagePercentage { get; set; }
    }

    public class FreeTierExceeded
    {
        public MonthlyUsage MonthlyUsage { get; set; } = new();
        public decimal Limit { get; set; }
    }

    public enum ReportPeriod
    {
        Daily,
        Weekly,
        Monthly
    }
}