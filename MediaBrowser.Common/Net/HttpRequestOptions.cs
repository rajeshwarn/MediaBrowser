﻿using System;
using System.Threading;

namespace MediaBrowser.Common.Net
{
    /// <summary>
    /// Class HttpRequestOptions
    /// </summary>
    public class HttpRequestOptions
    {
        /// <summary>
        /// Gets or sets the URL.
        /// </summary>
        /// <value>The URL.</value>
        public string Url { get; set; }

        /// <summary>
        /// Gets or sets the cancellation token.
        /// </summary>
        /// <value>The cancellation token.</value>
        public CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// Gets or sets the resource pool.
        /// </summary>
        /// <value>The resource pool.</value>
        public SemaphoreSlim ResourcePool { get; set; }

        /// <summary>
        /// Gets or sets the user agent.
        /// </summary>
        /// <value>The user agent.</value>
        public string UserAgent { get; set; }

        /// <summary>
        /// Gets or sets the progress.
        /// </summary>
        /// <value>The progress.</value>
        public IProgress<double> Progress { get; set; }
    }
}
