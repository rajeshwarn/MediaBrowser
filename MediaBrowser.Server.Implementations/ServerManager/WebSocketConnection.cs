﻿using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Serialization;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Server.Implementations.ServerManager
{
    /// <summary>
    /// Class WebSocketConnection
    /// </summary>
    public class WebSocketConnection : IWebSocketConnection
    {
        /// <summary>
        /// The _socket
        /// </summary>
        private readonly IWebSocket _socket;

        /// <summary>
        /// The _remote end point
        /// </summary>
        public string RemoteEndPoint { get; private set; }

        /// <summary>
        /// The _cancellation token source
        /// </summary>
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        /// <summary>
        /// The _send semaphore
        /// </summary>
        private readonly SemaphoreSlim _sendSemaphore = new SemaphoreSlim(1,1);

        /// <summary>
        /// The logger
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// The _json serializer
        /// </summary>
        private readonly IJsonSerializer _jsonSerializer;

        /// <summary>
        /// Gets or sets the receive action.
        /// </summary>
        /// <value>The receive action.</value>
        public Action<WebSocketMessageInfo> OnReceive { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WebSocketConnection" /> class.
        /// </summary>
        /// <param name="socket">The socket.</param>
        /// <param name="remoteEndPoint">The remote end point.</param>
        /// <param name="jsonSerializer">The json serializer.</param>
        /// <param name="logger">The logger.</param>
        /// <exception cref="System.ArgumentNullException">socket</exception>
        public WebSocketConnection(IWebSocket socket, string remoteEndPoint, IJsonSerializer jsonSerializer, ILogger logger)
        {
            if (socket == null)
            {
                throw new ArgumentNullException("socket");
            }
            if (string.IsNullOrEmpty(remoteEndPoint))
            {
                throw new ArgumentNullException("remoteEndPoint");
            }
            if (jsonSerializer == null)
            {
                throw new ArgumentNullException("jsonSerializer");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            _jsonSerializer = jsonSerializer;
            _socket = socket;
            _socket.OnReceiveDelegate = OnReceiveInternal;
            RemoteEndPoint = remoteEndPoint;
            _logger = logger;
        }

        /// <summary>
        /// Called when [receive].
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        private void OnReceiveInternal(byte[] bytes)
        {
            if (OnReceive == null)
            {
                return;
            }
            try
            {
                WebSocketMessageInfo info;

                using (var memoryStream = new MemoryStream(bytes))
                {
                    info = (WebSocketMessageInfo)_jsonSerializer.DeserializeFromStream(memoryStream, typeof(WebSocketMessageInfo));
                }

                info.Connection = this;

                OnReceive(info);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error processing web socket message", ex);
            }
        }

        /// <summary>
        /// Sends a message asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="message">The message.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="System.ArgumentNullException">message</exception>
        public Task SendAsync<T>(WebSocketMessage<T> message, CancellationToken cancellationToken)
        {
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }

            var bytes = _jsonSerializer.SerializeToBytes(message);

            return SendAsync(bytes, cancellationToken);
        }

        /// <summary>
        /// Sends a message asynchronously.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        public Task SendAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            return SendAsync(buffer, WebSocketMessageType.Text, cancellationToken);
        }

        /// <summary>
        /// Sends a message asynchronously.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="type">The type.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="System.ArgumentNullException">buffer</exception>
        public async Task SendAsync(byte[] buffer, WebSocketMessageType type, CancellationToken cancellationToken)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }

            if (cancellationToken == null)
            {
                throw new ArgumentNullException("cancellationToken");
            }
            
            cancellationToken.ThrowIfCancellationRequested();

            // Per msdn docs, attempting to send simultaneous messages will result in one failing.
            // This should help us workaround that and ensure all messages get sent
            await _sendSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await _socket.SendAsync(buffer, type, true, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.Info("WebSocket message to {0} was cancelled", RemoteEndPoint);

                throw;
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error sending WebSocket message {0}", ex, RemoteEndPoint);

                throw;
            }
            finally
            {
                _sendSemaphore.Release();
            }
        }

        /// <summary>
        /// Gets the state.
        /// </summary>
        /// <value>The state.</value>
        public WebSocketState State
        {
            get { return _socket.State; }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="dispose"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool dispose)
        {
            if (dispose)
            {
                _cancellationTokenSource.Dispose();
                _socket.Dispose();
            }
        }
    }
}
