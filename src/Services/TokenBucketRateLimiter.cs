using System;
using System.Threading;

namespace WhisperKey.Services
{
    /// <summary>
    /// Token bucket rate limiter implementation.
    /// Allows a configurable number of requests per time period.
    /// Thread-safe for concurrent access.
    /// </summary>
    public class TokenBucketRateLimiter
    {
        private long _tokens;
        private long _lastRefillTimestamp;
        private readonly long _maxTokens;
        private readonly long _refillPeriodMs;
        private readonly object _lock = new object();

        /// <summary>
        /// Creates a new token bucket rate limiter.
        /// </summary>
        /// <param name="maxRequests">Maximum number of requests allowed per period</param>
        /// <param name="periodMinutes">Time period in minutes</param>
        public TokenBucketRateLimiter(int maxRequests, int periodMinutes)
        {
            if (maxRequests <= 0)
                throw new ArgumentException("Max requests must be greater than 0", nameof(maxRequests));
            if (periodMinutes <= 0)
                throw new ArgumentException("Period must be greater than 0", nameof(periodMinutes));

            _maxTokens = maxRequests;
            _tokens = maxRequests;
            _refillPeriodMs = periodMinutes * 60 * 1000; // Convert to milliseconds
            _lastRefillTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// Maximum number of tokens (requests) allowed.
        /// </summary>
        public long MaxTokens => _maxTokens;

        /// <summary>
        /// Time period for token refill in minutes.
        /// </summary>
        public int PeriodMinutes => (int)(_refillPeriodMs / 60000);

        /// <summary>
        /// Attempts to consume a token from the bucket.
        /// </summary>
        /// <returns>True if a token was consumed (request allowed), false otherwise (rate limit exceeded)</returns>
        public bool TryConsume()
        {
            lock (_lock)
            {
                RefillTokens();

                if (_tokens > 0)
                {
                    _tokens--;
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Gets the number of available tokens.
        /// </summary>
        public long GetAvailableTokens()
        {
            lock (_lock)
            {
                RefillTokens();
                return _tokens;
            }
        }

        /// <summary>
        /// Gets the time until the next token will be available.
        /// </summary>
        public TimeSpan GetTimeUntilNextToken()
        {
            lock (_lock)
            {
                if (_tokens > 0)
                    return TimeSpan.Zero;

                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var elapsed = now - _lastRefillTimestamp;
                var tokensToAdd = elapsed * _maxTokens / _refillPeriodMs;

                if (tokensToAdd > 0)
                    return TimeSpan.Zero;

                // Calculate time until next token
                var timePerToken = _refillPeriodMs / (double)_maxTokens;
                var nextTokenTime = (long)(timePerToken - (elapsed % timePerToken));
                return TimeSpan.FromMilliseconds(nextTokenTime);
            }
        }

        /// <summary>
        /// Refills tokens based on elapsed time.
        /// Must be called within lock.
        /// </summary>
        private void RefillTokens()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var elapsed = now - _lastRefillTimestamp;

            if (elapsed >= _refillPeriodMs)
            {
                // Full refill
                _tokens = _maxTokens;
                _lastRefillTimestamp = now;
            }
            else
            {
                // Partial refill based on elapsed time
                var tokensToAdd = elapsed * _maxTokens / _refillPeriodMs;
                if (tokensToAdd > 0)
                {
                    _tokens = Math.Min(_tokens + tokensToAdd, _maxTokens);
                    _lastRefillTimestamp = now;
                }
            }
        }

        /// <summary>
        /// Resets the rate limiter to full capacity.
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _tokens = _maxTokens;
                _lastRefillTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
        }
    }
}
