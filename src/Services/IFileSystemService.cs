using System;
using System.IO;
using System.Threading.Tasks;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for file system operations to enable testing without touching the real file system.
    /// This abstraction allows unit tests to use mock file systems for reliable, isolated testing.
    /// </summary>
    public interface IFileSystemService
    {
        /// <summary>
        /// Checks if a file exists at the specified path.
        /// </summary>
        bool FileExists(string path);
        
        /// <summary>
        /// Checks if a directory exists at the specified path.
        /// </summary>
        bool DirectoryExists(string path);
        
        /// <summary>
        /// Creates a directory and all parent directories if they don't exist.
        /// </summary>
        void CreateDirectory(string path);
        
        /// <summary>
        /// Reads all text from a file asynchronously.
        /// </summary>
        Task<string> ReadAllTextAsync(string path);
        
        /// <summary>
        /// Writes text to a file asynchronously.
        /// </summary>
        Task WriteAllTextAsync(string path, string content);
        
        /// <summary>
        /// Reads all bytes from a file asynchronously.
        /// </summary>
        Task<byte[]> ReadAllBytesAsync(string path);
        
        /// <summary>
        /// Writes bytes to a file asynchronously.
        /// </summary>
        Task WriteAllBytesAsync(string path, byte[] data);
        
        /// <summary>
        /// Deletes a file if it exists.
        /// </summary>
        void DeleteFile(string path);
        
        /// <summary>
        /// Deletes a directory and optionally all contents.
        /// </summary>
        void DeleteDirectory(string path, bool recursive);
        
        /// <summary>
        /// Gets files in a directory matching a search pattern.
        /// </summary>
        string[] GetFiles(string path, string searchPattern);
        
        /// <summary>
        /// Gets subdirectories in a directory.
        /// </summary>
        string[] GetDirectories(string path);
        
        /// <summary>
        /// Gets the file information.
        /// </summary>
        FileInfo GetFileInfo(string path);
        
        /// <summary>
        /// Copies a file from source to destination.
        /// </summary>
        void CopyFile(string sourcePath, string destPath, bool overwrite);
        
        /// <summary>
        /// Moves a file from source to destination.
        /// </summary>
        void MoveFile(string sourcePath, string destPath);
        
        /// <summary>
        /// Appends text to a file.
        /// </summary>
        Task AppendAllTextAsync(string path, string content);
        
        /// <summary>
        /// Gets the current application data folder path.
        /// </summary>
        string GetAppDataPath();
        
        /// <summary>
        /// Combines multiple path parts into a single path.
        /// </summary>
        string CombinePath(params string[] paths);
        
        /// <summary>
        /// Gets the directory name from a path.
        /// </summary>
        string GetDirectoryName(string path);
        
        /// <summary>
        /// Gets the file name from a path.
        /// </summary>
        string GetFileName(string path);
        
        /// <summary>
        /// Gets a temporary file path.
        /// </summary>
        string GetTempFilePath();
        
        /// <summary>
        /// Gets the temporary directory path.
        /// </summary>
        string GetTempPath();
    }

    /// <summary>
    /// Real implementation of IFileSystemService that uses the actual file system.
    /// This is the production implementation that performs real I/O operations.
    /// </summary>
    public class FileSystemService : IFileSystemService
    {
        private readonly string _appName;
        private readonly System.IO.Abstractions.IFileSystem _fileSystem;

        public FileSystemService(string appName = "WhisperKey", System.IO.Abstractions.IFileSystem? fileSystem = null)
        {
            _appName = appName ?? throw new ArgumentNullException(nameof(appName));
            _fileSystem = fileSystem ?? new System.IO.Abstractions.FileSystem();
        }

        public bool FileExists(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or whitespace", nameof(path));
            return _fileSystem.File.Exists(path);
        }

        public bool DirectoryExists(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or whitespace", nameof(path));
            return _fileSystem.Directory.Exists(path);
        }

        public void CreateDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or whitespace", nameof(path));
            _fileSystem.Directory.CreateDirectory(path);
        }

        public async Task<string> ReadAllTextAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or whitespace", nameof(path));
            if (!FileExists(path))
                throw new FileNotFoundException($"File not found: {path}");
            return await _fileSystem.File.ReadAllTextAsync(path);
        }

        public async Task WriteAllTextAsync(string path, string content)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or whitespace", nameof(path));
            if (content == null)
                throw new ArgumentNullException(nameof(content));
                
            var directory = _fileSystem.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !DirectoryExists(directory))
            {
                CreateDirectory(directory);
            }
            
            await _fileSystem.File.WriteAllTextAsync(path, content);
        }

        public async Task<byte[]> ReadAllBytesAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or whitespace", nameof(path));
            if (!FileExists(path))
                throw new FileNotFoundException($"File not found: {path}");
            return await _fileSystem.File.ReadAllBytesAsync(path);
        }

        public async Task WriteAllBytesAsync(string path, byte[] data)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or whitespace", nameof(path));
            if (data == null)
                throw new ArgumentNullException(nameof(data));
                
            var directory = _fileSystem.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !DirectoryExists(directory))
            {
                CreateDirectory(directory);
            }
            
            await _fileSystem.File.WriteAllBytesAsync(path, data);
        }

        public void DeleteFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or whitespace", nameof(path));
            if (FileExists(path))
            {
                _fileSystem.File.Delete(path);
            }
        }

        public void DeleteDirectory(string path, bool recursive)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or whitespace", nameof(path));
            if (DirectoryExists(path))
            {
                _fileSystem.Directory.Delete(path, recursive);
            }
        }

        public string[] GetFiles(string path, string searchPattern)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or whitespace", nameof(path));
            if (!DirectoryExists(path))
                throw new DirectoryNotFoundException($"Directory not found: {path}");
            return _fileSystem.Directory.GetFiles(path, searchPattern);
        }

        public string[] GetDirectories(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or whitespace", nameof(path));
            if (!DirectoryExists(path))
                throw new DirectoryNotFoundException($"Directory not found: {path}");
            return _fileSystem.Directory.GetDirectories(path);
        }

        public FileInfo GetFileInfo(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or whitespace", nameof(path));
            return new FileInfo(path);
        }

        public void CopyFile(string sourcePath, string destPath, bool overwrite)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new ArgumentException("Source path cannot be null or whitespace", nameof(sourcePath));
            if (string.IsNullOrWhiteSpace(destPath))
                throw new ArgumentException("Destination path cannot be null or whitespace", nameof(destPath));
            if (!FileExists(sourcePath))
                throw new FileNotFoundException($"Source file not found: {sourcePath}");
                
            _fileSystem.File.Copy(sourcePath, destPath, overwrite);
        }

        public void MoveFile(string sourcePath, string destPath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new ArgumentException("Source path cannot be null or whitespace", nameof(sourcePath));
            if (string.IsNullOrWhiteSpace(destPath))
                throw new ArgumentException("Destination path cannot be null or whitespace", nameof(destPath));
            if (!FileExists(sourcePath))
                throw new FileNotFoundException($"Source file not found: {sourcePath}");
                
            _fileSystem.File.Move(sourcePath, destPath);
        }

        public async Task AppendAllTextAsync(string path, string content)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or whitespace", nameof(path));
            if (content == null)
                throw new ArgumentNullException(nameof(content));
                
            var directory = _fileSystem.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !DirectoryExists(directory))
            {
                CreateDirectory(directory);
            }
            
            await _fileSystem.File.AppendAllTextAsync(path, content);
        }

        public string GetAppDataPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return CombinePath(appData, _appName);
        }

        public string CombinePath(params string[] paths)
        {
            if (paths == null || paths.Length == 0)
                throw new ArgumentException("At least one path must be provided", nameof(paths));
            return _fileSystem.Path.Combine(paths);
        }

        public string GetDirectoryName(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or whitespace", nameof(path));
            return _fileSystem.Path.GetDirectoryName(path) ?? string.Empty;
        }

        public string GetFileName(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or whitespace", nameof(path));
            return _fileSystem.Path.GetFileName(path);
        }

        public string GetTempFilePath()
        {
            return _fileSystem.Path.GetTempFileName();
        }

        public string GetTempPath()
        {
            return _fileSystem.Path.GetTempPath();
        }
    }
}
