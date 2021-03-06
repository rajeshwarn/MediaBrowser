﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Common.Net
{
    /// <summary>
    /// Interface IHttpClient
    /// </summary>
    public interface IHttpClient : IDisposable
    {
        /// <summary>
        /// Performs a GET request and returns the resulting stream
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="resourcePool">The resource pool.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{Stream}.</returns>
        /// <exception cref="MediaBrowser.Model.Net.HttpException"></exception>
        Task<Stream> Get(string url, SemaphoreSlim resourcePool, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the specified URL.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{Stream}.</returns>
        Task<Stream> Get(string url, CancellationToken cancellationToken);
        
        /// <summary>
        /// Performs a POST request
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="postData">Params to add to the POST data.</param>
        /// <param name="resourcePool">The resource pool.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>stream on success, null on failure</returns>
        /// <exception cref="System.ArgumentNullException">postData</exception>
        /// <exception cref="MediaBrowser.Model.Net.HttpException"></exception>
        Task<Stream> Post(string url, Dictionary<string, string> postData, SemaphoreSlim resourcePool, CancellationToken cancellationToken);

        /// <summary>
        /// Posts the specified URL.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="postData">The post data.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{Stream}.</returns>
        Task<Stream> Post(string url, Dictionary<string, string> postData, CancellationToken cancellationToken);

        /// <summary>
        /// Downloads the contents of a given url into a temporary location
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>Task{System.String}.</returns>
        /// <exception cref="System.ArgumentNullException">progress</exception>
        /// <exception cref="MediaBrowser.Model.Net.HttpException"></exception>
        Task<string> GetTempFile(HttpRequestOptions options);
        
        /// <summary>
        /// Downloads the contents of a given url into a MemoryStream
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="resourcePool">The resource pool.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{MemoryStream}.</returns>
        /// <exception cref="MediaBrowser.Model.Net.HttpException"></exception>
        Task<MemoryStream> GetMemoryStream(string url, SemaphoreSlim resourcePool, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the memory stream.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{MemoryStream}.</returns>
        Task<MemoryStream> GetMemoryStream(string url, CancellationToken cancellationToken);
    }
}