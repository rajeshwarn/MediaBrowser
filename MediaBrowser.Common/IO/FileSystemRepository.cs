﻿using MediaBrowser.Common.Extensions;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace MediaBrowser.Common.IO
{
    /// <summary>
    /// This is a wrapper for storing large numbers of files within a directory on a file system.
    /// Simply pass a filename into GetResourcePath and it will return a full path location of where the file should be stored.
    /// </summary>
    public class FileSystemRepository : IDisposable
    {
        /// <summary>
        /// Contains the list of subfolders under the main directory
        /// The directory entry is created when the item is first added to the dictionary
        /// </summary>
        private readonly ConcurrentDictionary<string, string> _subFolderPaths = new ConcurrentDictionary<string, string>();

        /// <summary>
        /// The _file locks
        /// </summary>
        private readonly NamedLock _fileLocks = new NamedLock();

        /// <summary>
        /// Gets or sets the path.
        /// </summary>
        /// <value>The path.</value>
        protected string Path { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileSystemRepository" /> class.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <exception cref="System.ArgumentNullException"></exception>
        public FileSystemRepository(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException();
            }

            Path = path;
            Initialize();
        }

        /// <summary>
        /// Initializes this instance.
        /// </summary>
        protected void Initialize()
        {
            if (!Directory.Exists(Path))
            {
                Directory.CreateDirectory(Path);
            }
        }

        /// <summary>
        /// Gets the full path of where a resource should be stored within the repository
        /// </summary>
        /// <param name="uniqueName">Name of the unique.</param>
        /// <param name="fileExtension">The file extension.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public string GetResourcePath(string uniqueName, string fileExtension)
        {
            if (string.IsNullOrEmpty(uniqueName))
            {
                throw new ArgumentNullException();
            }

            if (string.IsNullOrEmpty(fileExtension))
            {
                throw new ArgumentNullException();
            }
            
            var filename = uniqueName.GetMD5() + fileExtension;

            return GetResourcePath(filename);
        }

        /// <summary>
        /// Gets the full path of where a file should be stored within the repository
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public string GetResourcePath(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentNullException();
            }
            
            return GetInternalResourcePath(filename);
        }

        /// <summary>
        /// Takes a filename and returns the full path of where it should be stored
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// <returns>System.String.</returns>
        private string GetInternalResourcePath(string filename)
        {
            var prefix = filename.Substring(0, 1);

            var folder = _subFolderPaths.GetOrAdd(prefix, GetCachePath);

            return System.IO.Path.Combine(folder, filename);
        }

        /// <summary>
        /// Creates a subfolder under the image cache directory and returns the full path
        /// </summary>
        /// <param name="prefix">The prefix.</param>
        /// <returns>System.String.</returns>
        private string GetCachePath(string prefix)
        {
            var path = System.IO.Path.Combine(Path, prefix);

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            return path;
        }

        /// <summary>
        /// Determines if a resource is present in the repository
        /// </summary>
        /// <param name="uniqueName">Name of the unique.</param>
        /// <param name="fileExtension">The file extension.</param>
        /// <returns><c>true</c> if the specified unique name contains resource; otherwise, <c>false</c>.</returns>
        public bool ContainsResource(string uniqueName, string fileExtension)
        {
            return ContainsFilePath(GetResourcePath(uniqueName, fileExtension));
        }

        /// <summary>
        /// Determines if a file with a given name is present in the repository
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// <returns><c>true</c> if the specified filename contains filename; otherwise, <c>false</c>.</returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public bool ContainsFilename(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentNullException();
            }
            
            return ContainsFilePath(GetInternalResourcePath(filename));
        }

        /// <summary>
        /// Determines if a file is present in the repository
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns><c>true</c> if [contains file path] [the specified path]; otherwise, <c>false</c>.</returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public bool ContainsFilePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException();
            }
            
            return File.Exists(path);
        }

        /// <summary>
        /// Waits for lock.
        /// </summary>
        /// <param name="resourcePath">The resource path.</param>
        public Task WaitForLockAsync(string resourcePath)
        {
            return _fileLocks.WaitAsync(resourcePath);
        }

        /// <summary>
        /// Releases the lock.
        /// </summary>
        /// <param name="resourcePath">The resource path.</param>
        public void ReleaseLock(string resourcePath)
        {
            _fileLocks.Release(resourcePath);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="dispose"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool dispose)
        {
            if (dispose)
            {
                _fileLocks.Dispose();
            }
        }
    }
}
