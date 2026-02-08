using System;
using System.Buffers;
using System.Collections.Concurrent;

namespace WhisperKey.Services.Memory
{
    /// <summary>
    /// Wrapper around System.Buffers.ArrayPool for managed byte array pooling.
    /// </summary>
    public class ByteArrayPool : IByteArrayPool
    {
        private static readonly Lazy<ByteArrayPool> _instance = new Lazy<ByteArrayPool>(() => new ByteArrayPool());
        public static ByteArrayPool Instance => _instance.Value;

        private readonly ArrayPool<byte> _pool;
        private long _rentCount;
        private long _returnCount;

        public long RentCount => _rentCount;
        public long ReturnCount => _returnCount;

        public ByteArrayPool(int maxArrayLength = 1024 * 1024, int maxArraysPerBucket = 50)
        {
            _pool = ArrayPool<byte>.Create(maxArrayLength, maxArraysPerBucket);
        }

        public byte[] Rent(int minimumLength)
        {
            System.Threading.Interlocked.Increment(ref _rentCount);
            return _pool.Rent(minimumLength);
        }

        public void Return(byte[] array, bool clearArray = false)
        {
            System.Threading.Interlocked.Increment(ref _returnCount);
            _pool.Return(array, clearArray);
        }
    }

    /// <summary>
    /// Thread-safe generic object pool implementation.
    /// </summary>
    public class GenericObjectPool<T> : IObjectPool<T> where T : class, new()
    {
        private readonly ConcurrentQueue<T> _objects = new ConcurrentQueue<T>();
        private readonly int _maxSize;
        private int _currentSize;

        public GenericObjectPool(int maxSize = 100)
        {
            _maxSize = maxSize;
        }

        public T Get()
        {
            if (_objects.TryDequeue(out var item))
            {
                System.Threading.Interlocked.Decrement(ref _currentSize);
                return item;
            }
            return new T();
        }

        public void Return(T obj)
        {
            if (_currentSize < _maxSize)
            {
                _objects.Enqueue(obj);
                System.Threading.Interlocked.Increment(ref _currentSize);
            }
        }
    }
}
