using System;

namespace WhisperKey.Services.Memory
{
    /// <summary>
    /// Interface for byte array pooling.
    /// </summary>
    public interface IByteArrayPool
    {
        /// <summary>
        /// Rents a byte array of at least the specified minimum length.
        /// </summary>
        byte[] Rent(int minimumLength);

        /// <summary>
        /// Returns a byte array to the pool.
        /// </summary>
        void Return(byte[] array, bool clearArray = false);
    }

    /// <summary>
    /// Interface for generic object pooling.
    /// </summary>
    public interface IObjectPool<T> where T : class
    {
        /// <summary>
        /// Gets an object from the pool.
        /// </summary>
        T Get();

        /// <summary>
        /// Returns an object to the pool.
        /// </summary>
        void Return(T obj);
    }
}
