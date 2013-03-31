﻿using System.Text;
using MediaBrowser.Common.IO;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.MediaInfo
{
    /// <summary>
    /// Class FFMpegManager
    /// </summary>
    public class FFMpegManager : IDisposable
    {
        /// <summary>
        /// Gets or sets the video image cache.
        /// </summary>
        /// <value>The video image cache.</value>
        internal FileSystemRepository VideoImageCache { get; set; }

        /// <summary>
        /// Gets or sets the image cache.
        /// </summary>
        /// <value>The image cache.</value>
        internal FileSystemRepository AudioImageCache { get; set; }

        /// <summary>
        /// Gets or sets the subtitle cache.
        /// </summary>
        /// <value>The subtitle cache.</value>
        internal FileSystemRepository SubtitleCache { get; set; }

        /// <summary>
        /// Gets or sets the zip client.
        /// </summary>
        /// <value>The zip client.</value>
        private readonly IZipClient _zipClient;

        /// <summary>
        /// The _logger
        /// </summary>
        private readonly Kernel _kernel;

        /// <summary>
        /// The _logger
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// Gets the json serializer.
        /// </summary>
        /// <value>The json serializer.</value>
        private readonly IJsonSerializer _jsonSerializer;

        /// <summary>
        /// The _protobuf serializer
        /// </summary>
        private readonly IProtobufSerializer _protobufSerializer;
        private readonly IServerApplicationPaths _appPaths;

        /// <summary>
        /// Initializes a new instance of the <see cref="FFMpegManager" /> class.
        /// </summary>
        /// <param name="kernel">The kernel.</param>
        /// <param name="zipClient">The zip client.</param>
        /// <param name="jsonSerializer">The json serializer.</param>
        /// <param name="protobufSerializer">The protobuf serializer.</param>
        /// <param name="logger">The logger.</param>
        /// <exception cref="System.ArgumentNullException">zipClient</exception>
        public FFMpegManager(Kernel kernel, IZipClient zipClient, IJsonSerializer jsonSerializer, IProtobufSerializer protobufSerializer, ILogManager logManager, IServerApplicationPaths appPaths)
        {
            if (kernel == null)
            {
                throw new ArgumentNullException("kernel");
            }
            if (zipClient == null)
            {
                throw new ArgumentNullException("zipClient");
            }
            if (jsonSerializer == null)
            {
                throw new ArgumentNullException("jsonSerializer");
            }
            if (protobufSerializer == null)
            {
                throw new ArgumentNullException("protobufSerializer");
            }

            _kernel = kernel;
            _zipClient = zipClient;
            _jsonSerializer = jsonSerializer;
            _protobufSerializer = protobufSerializer;
            _appPaths = appPaths;
            _logger = logManager.GetLogger("FFMpegManager");

            // Not crazy about this but it's the only way to suppress ffmpeg crash dialog boxes
            SetErrorMode(ErrorModes.SEM_FAILCRITICALERRORS | ErrorModes.SEM_NOALIGNMENTFAULTEXCEPT | ErrorModes.SEM_NOGPFAULTERRORBOX | ErrorModes.SEM_NOOPENFILEERRORBOX);

            VideoImageCache = new FileSystemRepository(VideoImagesDataPath);
            AudioImageCache = new FileSystemRepository(AudioImagesDataPath);
            SubtitleCache = new FileSystemRepository(SubtitleCachePath);

            Task.Run(() => VersionedDirectoryPath = GetVersionedDirectoryPath());
        }

        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="dispose"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected void Dispose(bool dispose)
        {
            if (dispose)
            {
                SetErrorMode(ErrorModes.SYSTEM_DEFAULT);

                AudioImageCache.Dispose();
                VideoImageCache.Dispose();
            }
        }

        /// <summary>
        /// The FF probe resource pool count
        /// </summary>
        private const int FFProbeResourcePoolCount = 3;
        /// <summary>
        /// The audio image resource pool count
        /// </summary>
        private const int AudioImageResourcePoolCount = 3;
        /// <summary>
        /// The video image resource pool count
        /// </summary>
        private const int VideoImageResourcePoolCount = 2;

        /// <summary>
        /// The FF probe resource pool
        /// </summary>
        private readonly SemaphoreSlim FFProbeResourcePool = new SemaphoreSlim(FFProbeResourcePoolCount, FFProbeResourcePoolCount);
        /// <summary>
        /// The audio image resource pool
        /// </summary>
        private readonly SemaphoreSlim AudioImageResourcePool = new SemaphoreSlim(AudioImageResourcePoolCount, AudioImageResourcePoolCount);
        /// <summary>
        /// The video image resource pool
        /// </summary>
        private readonly SemaphoreSlim VideoImageResourcePool = new SemaphoreSlim(VideoImageResourcePoolCount, VideoImageResourcePoolCount);

        /// <summary>
        /// Gets or sets the versioned directory path.
        /// </summary>
        /// <value>The versioned directory path.</value>
        private string VersionedDirectoryPath { get; set; }

        /// <summary>
        /// Gets the FFMPEG version.
        /// </summary>
        /// <value>The FFMPEG version.</value>
        public string FFMpegVersion
        {
            get { return Path.GetFileNameWithoutExtension(VersionedDirectoryPath); }
        }

        /// <summary>
        /// The _ FF MPEG path
        /// </summary>
        private string _FFMpegPath;
        /// <summary>
        /// Gets the path to ffmpeg.exe
        /// </summary>
        /// <value>The FF MPEG path.</value>
        public string FFMpegPath
        {
            get
            {
                return _FFMpegPath ?? (_FFMpegPath = Path.Combine(VersionedDirectoryPath, "ffmpeg.exe"));
            }
        }

        /// <summary>
        /// The _ FF probe path
        /// </summary>
        private string _FFProbePath;
        /// <summary>
        /// Gets the path to ffprobe.exe
        /// </summary>
        /// <value>The FF probe path.</value>
        public string FFProbePath
        {
            get
            {
                return _FFProbePath ?? (_FFProbePath = Path.Combine(VersionedDirectoryPath, "ffprobe.exe"));
            }
        }

        /// <summary>
        /// The _video images data path
        /// </summary>
        private string _videoImagesDataPath;
        /// <summary>
        /// Gets the video images data path.
        /// </summary>
        /// <value>The video images data path.</value>
        public string VideoImagesDataPath
        {
            get
            {
                if (_videoImagesDataPath == null)
                {
                    _videoImagesDataPath = Path.Combine(_appPaths.DataPath, "ffmpeg-video-images");

                    if (!Directory.Exists(_videoImagesDataPath))
                    {
                        Directory.CreateDirectory(_videoImagesDataPath);
                    }
                }

                return _videoImagesDataPath;
            }
        }

        /// <summary>
        /// The _audio images data path
        /// </summary>
        private string _audioImagesDataPath;
        /// <summary>
        /// Gets the audio images data path.
        /// </summary>
        /// <value>The audio images data path.</value>
        public string AudioImagesDataPath
        {
            get
            {
                if (_audioImagesDataPath == null)
                {
                    _audioImagesDataPath = Path.Combine(_appPaths.DataPath, "ffmpeg-audio-images");

                    if (!Directory.Exists(_audioImagesDataPath))
                    {
                        Directory.CreateDirectory(_audioImagesDataPath);
                    }
                }

                return _audioImagesDataPath;
            }
        }

        /// <summary>
        /// The _subtitle cache path
        /// </summary>
        private string _subtitleCachePath;
        /// <summary>
        /// Gets the subtitle cache path.
        /// </summary>
        /// <value>The subtitle cache path.</value>
        public string SubtitleCachePath
        {
            get
            {
                if (_subtitleCachePath == null)
                {
                    _subtitleCachePath = Path.Combine(_appPaths.CachePath, "ffmpeg-subtitles");

                    if (!Directory.Exists(_subtitleCachePath))
                    {
                        Directory.CreateDirectory(_subtitleCachePath);
                    }
                }

                return _subtitleCachePath;
            }
        }

        /// <summary>
        /// Gets the versioned directory path.
        /// </summary>
        /// <returns>System.String.</returns>
        private string GetVersionedDirectoryPath()
        {
            var assembly = GetType().Assembly;

            const string prefix = "MediaBrowser.Controller.MediaInfo.";
            const string srch = prefix + "ffmpeg";

            var resource = assembly.GetManifestResourceNames().First(r => r.StartsWith(srch));

            var filename = resource.Substring(resource.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) + prefix.Length);

            var versionedDirectoryPath = Path.Combine(_appPaths.MediaToolsPath, Path.GetFileNameWithoutExtension(filename));

            if (!Directory.Exists(versionedDirectoryPath))
            {
                Directory.CreateDirectory(versionedDirectoryPath);
            }

            ExtractTools(assembly, resource, versionedDirectoryPath);

            return versionedDirectoryPath;
        }

        /// <summary>
        /// Extracts the tools.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <param name="zipFileResourcePath">The zip file resource path.</param>
        /// <param name="targetPath">The target path.</param>
        private void ExtractTools(Assembly assembly, string zipFileResourcePath, string targetPath)
        {
            using (var resourceStream = assembly.GetManifestResourceStream(zipFileResourcePath))
            {
                _zipClient.ExtractAll(resourceStream, targetPath, false);
            }
        }

        /// <summary>
        /// Gets the probe size argument.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>System.String.</returns>
        public string GetProbeSizeArgument(BaseItem item)
        {
            var video = item as Video;

            return video != null ? GetProbeSizeArgument(video.VideoType, video.IsoType) : string.Empty;
        }

        /// <summary>
        /// Gets the probe size argument.
        /// </summary>
        /// <param name="videoType">Type of the video.</param>
        /// <param name="isoType">Type of the iso.</param>
        /// <returns>System.String.</returns>
        public string GetProbeSizeArgument(VideoType videoType, IsoType? isoType)
        {
            if (videoType == VideoType.Dvd || (isoType.HasValue && isoType.Value == IsoType.Dvd))
            {
                return "-probesize 1G -analyzeduration 200M";
            }

            return string.Empty;
        }

        /// <summary>
        /// Runs FFProbe against a BaseItem
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="inputPath">The input path.</param>
        /// <param name="lastDateModified">The last date modified.</param>
        /// <param name="cache">The cache.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{FFProbeResult}.</returns>
        /// <exception cref="System.ArgumentNullException">item</exception>
        public Task<FFProbeResult> RunFFProbe(BaseItem item, string inputPath, DateTime lastDateModified, FileSystemRepository cache, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(inputPath))
            {
                throw new ArgumentNullException("inputPath");
            }

            if (cache == null)
            {
                throw new ArgumentNullException("cache");
            }

            // Put the ffmpeg version into the cache name so that it's unique per-version
            // We don't want to try and deserialize data based on an old version, which could potentially fail
            var resourceName = item.Id + "_" + lastDateModified.Ticks + "_" + FFMpegVersion;

            // Forumulate the cache file path
            var cacheFilePath = cache.GetResourcePath(resourceName, ".pb");

            cancellationToken.ThrowIfCancellationRequested();

            // Avoid File.Exists by just trying to deserialize
            try
            {
                return Task.FromResult(_protobufSerializer.DeserializeFromFile<FFProbeResult>(cacheFilePath));
            }
            catch (FileNotFoundException)
            {
                var extractChapters = false;
                var video = item as Video;
                var probeSizeArgument = string.Empty;

                if (video != null)
                {
                    extractChapters = true;
                    probeSizeArgument = GetProbeSizeArgument(video.VideoType, video.IsoType);
                }

                return RunFFProbeInternal(inputPath, extractChapters, cacheFilePath, probeSizeArgument, cancellationToken);
            }
        }

        /// <summary>
        /// Runs FFProbe against a BaseItem
        /// </summary>
        /// <param name="inputPath">The input path.</param>
        /// <param name="extractChapters">if set to <c>true</c> [extract chapters].</param>
        /// <param name="cacheFile">The cache file.</param>
        /// <param name="probeSizeArgument">The probe size argument.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{FFProbeResult}.</returns>
        /// <exception cref="System.ApplicationException"></exception>
        private async Task<FFProbeResult> RunFFProbeInternal(string inputPath, bool extractChapters, string cacheFile, string probeSizeArgument, CancellationToken cancellationToken)
        {
            await FFProbeResourcePool.WaitAsync(cancellationToken).ConfigureAwait(false);

            bool success;
            StringBuilder output = new StringBuilder();
            StringBuilder error = new StringBuilder();

            int processId = -1;
            try
            {
                // Code copied from StackOverflow, http://stackoverflow.com/questions/139593/processstartinfo-hanging-on-waitforexit-why
                // The previous code had some issues when it tried to read from the redirected outputs, this code uses a different approach.
                using (Process process = new Process())
                {
                    process.EnableRaisingEvents = true;
                    process.Exited += ProcessExited;

                    process.StartInfo.FileName = FFProbePath;
                    process.StartInfo.Arguments = string.Format("{0} -i {1} -threads 0 -v info -print_format json -show_streams -show_format", probeSizeArgument, inputPath).Trim();

                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;

                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    process.StartInfo.ErrorDialog = false;

                    _logger.Debug("{0} {1}", process.StartInfo.FileName, process.StartInfo.Arguments);

                    using (AutoResetEvent outputWaitHandle = new AutoResetEvent(false))
                    using (AutoResetEvent errorWaitHandle = new AutoResetEvent(false))
                    {
                        process.OutputDataReceived += (sender, e) =>
                            {
                                if (e.Data == null)
                                {
                                    outputWaitHandle.Set();
                                }
                                else
                                {
                                    output.AppendLine(e.Data);
                                }
                            };
                        process.ErrorDataReceived += (sender, e) =>
                            {
                                if (e.Data == null)
                                {
                                    errorWaitHandle.Set();
                                }
                                else
                                {
                                    error.AppendLine(e.Data);
                                }
                            };

                        process.Start();
                        processId = process.Id;

                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        int timeout = 60000;
                        if (process.WaitForExit(timeout) &&
                            outputWaitHandle.WaitOne(timeout) &&
                            errorWaitHandle.WaitOne(timeout))
                        {
                            // Process completed. Check process.ExitCode here.
                            success = true;
                        }
                        else
                        {
                            // Timed out.
                            success = false;
                        }
                    }
                }
            }
            catch
            {
                // Try to kill the process
                if (processId != -1)
                {
                    try
                    {
                        Process proc = Process.GetProcessById(processId);
                        if (!proc.HasExited)
                            proc.Kill();
                    }
                    catch (InvalidOperationException ex1)
                    {
                        _logger.ErrorException("Error killing ffprobe", ex1);
                    }
                    catch (Win32Exception ex1)
                    {
                        _logger.ErrorException("Error killing ffprobe", ex1);
                    }
                }

                throw;
            }
            finally
            {
                FFProbeResourcePool.Release();
            }

            if (!success)
                return null;

            FFProbeResult result = _jsonSerializer.DeserializeFromString<FFProbeResult>(output.ToString());

            if (result == null)
            {
                throw new ApplicationException(string.Format("FFProbe failed for {0}", inputPath));
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (extractChapters && error.Length > 0)
            {
                AddChapters(result, error.ToString());
            }

            _protobufSerializer.SerializeToFile(result, cacheFile);

            return result;
        }

        /// <summary>
        /// Adds the chapters.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <param name="standardError">The standard error.</param>
        private void AddChapters(FFProbeResult result, string standardError)
        {
            var lines = standardError.Split('\n').Select(l => l.TrimStart());

            var chapters = new List<ChapterInfo>();

            ChapterInfo lastChapter = null;

            foreach (var line in lines)
            {
                if (line.StartsWith("Chapter", StringComparison.OrdinalIgnoreCase))
                {
                    // Example:
                    // Chapter #0.2: start 400.534, end 4565.435
                    const string srch = "start ";
                    var start = line.IndexOf(srch, StringComparison.OrdinalIgnoreCase);

                    if (start == -1)
                    {
                        continue;
                    }

                    var subString = line.Substring(start + srch.Length);
                    subString = subString.Substring(0, subString.IndexOf(','));

                    double seconds;

                    if (double.TryParse(subString, out seconds))
                    {
                        lastChapter = new ChapterInfo
                        {
                            StartPositionTicks = TimeSpan.FromSeconds(seconds).Ticks
                        };

                        chapters.Add(lastChapter);
                    }
                }

                else if (line.StartsWith("title", StringComparison.OrdinalIgnoreCase))
                {
                    if (lastChapter != null && string.IsNullOrEmpty(lastChapter.Name))
                    {
                        var index = line.IndexOf(':');

                        if (index != -1)
                        {
                            lastChapter.Name = line.Substring(index + 1).Trim().TrimEnd('\r');
                        }
                    }
                }
            }

            result.Chapters = chapters;
        }

        /// <summary>
        /// The first chapter ticks
        /// </summary>
        private static long FirstChapterTicks = TimeSpan.FromSeconds(15).Ticks;

        /// <summary>
        /// Extracts the chapter images.
        /// </summary>
        /// <param name="video">The video.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="extractImages">if set to <c>true</c> [extract images].</param>
        /// <param name="saveItem">if set to <c>true</c> [save item].</param>
        /// <returns>Task.</returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public async Task PopulateChapterImages(Video video, CancellationToken cancellationToken, bool extractImages, bool saveItem)
        {
            if (video.Chapters == null)
            {
                throw new ArgumentNullException();
            }

            // Can't extract images if there are no video streams
            if (video.MediaStreams == null || video.MediaStreams.All(m => m.Type != MediaStreamType.Video))
            {
                return;
            }

            var changesMade = false;

            foreach (var chapter in video.Chapters)
            {
                var filename = video.Id + "_" + video.DateModified.Ticks + "_" + chapter.StartPositionTicks;

                var path = VideoImageCache.GetResourcePath(filename, ".jpg");

                if (!VideoImageCache.ContainsFilePath(path))
                {
                    if (extractImages)
                    {
                        // Disable for now on folder rips
                        if (video.VideoType != VideoType.VideoFile)
                        {
                            continue;
                        }

                        // Add some time for the first chapter to make sure we don't end up with a black image
                        var time = chapter.StartPositionTicks == 0 ? TimeSpan.FromTicks(Math.Min(FirstChapterTicks, video.RunTimeTicks ?? 0)) : TimeSpan.FromTicks(chapter.StartPositionTicks);

                        var success = await ExtractImage(GetInputArgument(video), time, path, cancellationToken).ConfigureAwait(false);

                        if (success)
                        {
                            chapter.ImagePath = path;
                            changesMade = true;
                        }
                    }
                }
                else if (!string.Equals(path, chapter.ImagePath, StringComparison.OrdinalIgnoreCase))
                {
                    chapter.ImagePath = path;
                    changesMade = true;
                }
            }

            if (saveItem && changesMade)
            {
                await _kernel.ItemRepository.SaveItem(video, CancellationToken.None).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Extracts an image from an Audio file and returns a Task whose result indicates whether it was successful or not
        /// </summary>
        /// <param name="input">The input.</param>
        /// <param name="outputPath">The output path.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{System.Boolean}.</returns>
        /// <exception cref="System.ArgumentNullException">input</exception>
        public async Task<bool> ExtractImage(Audio input, string outputPath, CancellationToken cancellationToken)
        {
            if (input == null)
            {
                throw new ArgumentNullException("input");
            }

            if (string.IsNullOrEmpty(outputPath))
            {
                throw new ArgumentNullException("outputPath");
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    FileName = FFMpegPath,
                    Arguments = string.Format("-i {0} -threads 0 -v quiet -f image2 \"{1}\"", GetInputArgument(input), outputPath),
                    WindowStyle = ProcessWindowStyle.Hidden,
                    ErrorDialog = false
                }
            };

            await AudioImageResourcePool.WaitAsync(cancellationToken).ConfigureAwait(false);

            await RunAsync(process).ConfigureAwait(false);

            AudioImageResourcePool.Release();

            var exitCode = process.ExitCode;

            process.Dispose();

            if (exitCode != -1 && File.Exists(outputPath))
            {
                return true;
            }

            _logger.Error("ffmpeg audio image extraction failed for {0}", input.Path);
            return false;
        }

        /// <summary>
        /// Determines whether [is subtitle cached] [the specified input].
        /// </summary>
        /// <param name="input">The input.</param>
        /// <param name="subtitleStreamIndex">Index of the subtitle stream.</param>
        /// <param name="outputExtension">The output extension.</param>
        /// <returns><c>true</c> if [is subtitle cached] [the specified input]; otherwise, <c>false</c>.</returns>
        public bool IsSubtitleCached(Video input, int subtitleStreamIndex, string outputExtension)
        {
            return SubtitleCache.ContainsFilePath(GetSubtitleCachePath(input, subtitleStreamIndex, outputExtension));
        }

        /// <summary>
        /// Gets the subtitle cache path.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <param name="subtitleStreamIndex">Index of the subtitle stream.</param>
        /// <param name="outputExtension">The output extension.</param>
        /// <returns>System.String.</returns>
        public string GetSubtitleCachePath(Video input, int subtitleStreamIndex, string outputExtension)
        {
            return SubtitleCache.GetResourcePath(input.Id + "_" + subtitleStreamIndex + "_" + input.DateModified.Ticks, outputExtension);
        }

        /// <summary>
        /// Extracts the text subtitle.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <param name="subtitleStreamIndex">Index of the subtitle stream.</param>
        /// <param name="outputPath">The output path.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{System.Boolean}.</returns>
        /// <exception cref="System.ArgumentNullException">input</exception>
        public async Task<bool> ExtractTextSubtitle(Video input, int subtitleStreamIndex, string outputPath, CancellationToken cancellationToken)
        {
            if (input == null)
            {
                throw new ArgumentNullException("input");
            }

            if (cancellationToken == null)
            {
                throw new ArgumentNullException("cancellationToken");
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    FileName = FFMpegPath,
                    Arguments = string.Format("-i {0} -map 0:{1} -an -vn -c:s ass \"{2}\"", GetInputArgument(input), subtitleStreamIndex, outputPath),
                    WindowStyle = ProcessWindowStyle.Hidden,
                    ErrorDialog = false
                }
            };

            _logger.Debug("{0} {1}", process.StartInfo.FileName, process.StartInfo.Arguments);

            await AudioImageResourcePool.WaitAsync(cancellationToken).ConfigureAwait(false);

            await RunAsync(process).ConfigureAwait(false);

            AudioImageResourcePool.Release();

            var exitCode = process.ExitCode;

            process.Dispose();

            if (exitCode != -1 && File.Exists(outputPath))
            {
                return true;
            }

            _logger.Error("ffmpeg subtitle extraction failed for {0}", input.Path);
            return false;
        }

        /// <summary>
        /// Converts the text subtitle.
        /// </summary>
        /// <param name="mediaStream">The media stream.</param>
        /// <param name="outputPath">The output path.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{System.Boolean}.</returns>
        /// <exception cref="System.ArgumentNullException">mediaStream</exception>
        /// <exception cref="System.ArgumentException">The given MediaStream is not an external subtitle stream</exception>
        public async Task<bool> ConvertTextSubtitle(MediaStream mediaStream, string outputPath, CancellationToken cancellationToken)
        {
            if (mediaStream == null)
            {
                throw new ArgumentNullException("mediaStream");
            }

            if (!mediaStream.IsExternal || string.IsNullOrEmpty(mediaStream.Path))
            {
                throw new ArgumentException("The given MediaStream is not an external subtitle stream");
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    FileName = FFMpegPath,
                    Arguments = string.Format("-i \"{0}\" \"{1}\"", mediaStream.Path, outputPath),
                    WindowStyle = ProcessWindowStyle.Hidden,
                    ErrorDialog = false
                }
            };

            _logger.Debug("{0} {1}", process.StartInfo.FileName, process.StartInfo.Arguments);

            await AudioImageResourcePool.WaitAsync(cancellationToken).ConfigureAwait(false);

            await RunAsync(process).ConfigureAwait(false);

            AudioImageResourcePool.Release();

            var exitCode = process.ExitCode;

            process.Dispose();

            if (exitCode != -1 && File.Exists(outputPath))
            {
                return true;
            }

            _logger.Error("ffmpeg subtitle conversion failed for {0}", mediaStream.Path);
            return false;
        }

        /// <summary>
        /// Extracts an image from a Video and returns a Task whose result indicates whether it was successful or not
        /// </summary>
        /// <param name="inputPath">The input path.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="outputPath">The output path.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{System.Boolean}.</returns>
        /// <exception cref="System.ArgumentNullException">video</exception>
        public async Task<bool> ExtractImage(string inputPath, TimeSpan offset, string outputPath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(inputPath))
            {
                throw new ArgumentNullException("inputPath");
            }

            if (string.IsNullOrEmpty(outputPath))
            {
                throw new ArgumentNullException("outputPath");
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    FileName = FFMpegPath,
                    Arguments = string.Format("-ss {0} -i {1} -threads 0 -v quiet -vframes 1 -filter:v select=\\'eq(pict_type\\,I)\\' -f image2 \"{2}\"", Convert.ToInt32(offset.TotalSeconds), inputPath, outputPath),
                    WindowStyle = ProcessWindowStyle.Hidden,
                    ErrorDialog = false
                }
            };

            await VideoImageResourcePool.WaitAsync(cancellationToken).ConfigureAwait(false);

            process.Start();

            var ranToCompletion = process.WaitForExit(10000);

            if (!ranToCompletion)
            {
                try
                {
                    _logger.Info("Killing ffmpeg process");

                    process.Kill();
                    process.WaitForExit(1000);
                }
                catch (Win32Exception ex)
                {
                    _logger.ErrorException("Error killing process", ex);
                }
                catch (InvalidOperationException ex)
                {
                    _logger.ErrorException("Error killing process", ex);
                }
                catch (NotSupportedException ex)
                {
                    _logger.ErrorException("Error killing process", ex);
                }
            }

            VideoImageResourcePool.Release();

            var exitCode = ranToCompletion ? process.ExitCode : -1;

            process.Dispose();

            if (exitCode == -1)
            {
                if (File.Exists(outputPath))
                {
                    try
                    {
                        _logger.Info("Deleting extracted image due to failure: ", outputPath);
                        File.Delete(outputPath);
                    }
                    catch (IOException ex)
                    {
                        _logger.ErrorException("Error deleting extracted image {0}", ex, outputPath);
                    }
                }
            }
            else
            {
                if (File.Exists(outputPath))
                {
                    return true;
                }
            }

            _logger.Error("ffmpeg video image extraction failed for {0}", inputPath);
            return false;
        }

        /// <summary>
        /// Gets the input argument.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>System.String.</returns>
        public string GetInputArgument(BaseItem item)
        {
            var video = item as Video;

            if (video != null)
            {
                if (video.VideoType == VideoType.BluRay)
                {
                    return GetBlurayInputArgument(video.Path);
                }

                if (video.VideoType == VideoType.Dvd)
                {
                    return GetDvdInputArgument(video.GetPlayableStreamFiles());
                }
            }

            return string.Format("file:\"{0}\"", item.Path);
        }

        /// <summary>
        /// Gets the input argument.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="mount">The mount.</param>
        /// <returns>System.String.</returns>
        public string GetInputArgument(Video item, IIsoMount mount)
        {
            if (item.VideoType == VideoType.Iso && item.IsoType.HasValue)
            {
                if (item.IsoType.Value == IsoType.BluRay)
                {
                    return GetBlurayInputArgument(mount.MountedPath);
                }
                if (item.IsoType.Value == IsoType.Dvd)
                {
                    return GetDvdInputArgument(item.GetPlayableStreamFiles(mount.MountedPath));
                }
            }

            return GetInputArgument(item);
        }

        /// <summary>
        /// Gets the bluray input argument.
        /// </summary>
        /// <param name="blurayRoot">The bluray root.</param>
        /// <returns>System.String.</returns>
        public string GetBlurayInputArgument(string blurayRoot)
        {
            return string.Format("bluray:\"{0}\"", blurayRoot);
        }

        /// <summary>
        /// Gets the DVD input argument.
        /// </summary>
        /// <param name="playableStreamFiles">The playable stream files.</param>
        /// <returns>System.String.</returns>
        public string GetDvdInputArgument(IEnumerable<string> playableStreamFiles)
        {
            // Get all streams
            var streamFilePaths = (playableStreamFiles ?? new string[] { }).ToArray();

            // If there's more than one we'll need to use the concat command
            if (streamFilePaths.Length > 1)
            {
                var files = string.Join("|", streamFilePaths);

                return string.Format("concat:\"{0}\"", files);
            }

            // Determine the input path for video files
            return string.Format("file:\"{0}\"", streamFilePaths[0]);
        }

        /// <summary>
        /// Processes the exited.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        void ProcessExited(object sender, EventArgs e)
        {
            ((Process)sender).Dispose();
        }

        /// <summary>
        /// Provides a non-blocking method to start a process and wait asynchronously for it to exit
        /// </summary>
        /// <param name="process">The process.</param>
        /// <returns>Task{System.Boolean}.</returns>
        private static Task<bool> RunAsync(Process process)
        {
            var tcs = new TaskCompletionSource<bool>();

            process.EnableRaisingEvents = true;
            process.Exited += (sender, args) => tcs.SetResult(true);

            process.Start();

            return tcs.Task;
        }

        /// <summary>
        /// Sets the error mode.
        /// </summary>
        /// <param name="uMode">The u mode.</param>
        /// <returns>ErrorModes.</returns>
        [DllImport("kernel32.dll")]
        static extern ErrorModes SetErrorMode(ErrorModes uMode);

        /// <summary>
        /// Enum ErrorModes
        /// </summary>
        [Flags]
        public enum ErrorModes : uint
        {
            /// <summary>
            /// The SYSTE m_ DEFAULT
            /// </summary>
            SYSTEM_DEFAULT = 0x0,
            /// <summary>
            /// The SE m_ FAILCRITICALERRORS
            /// </summary>
            SEM_FAILCRITICALERRORS = 0x0001,
            /// <summary>
            /// The SE m_ NOALIGNMENTFAULTEXCEPT
            /// </summary>
            SEM_NOALIGNMENTFAULTEXCEPT = 0x0004,
            /// <summary>
            /// The SE m_ NOGPFAULTERRORBOX
            /// </summary>
            SEM_NOGPFAULTERRORBOX = 0x0002,
            /// <summary>
            /// The SE m_ NOOPENFILEERRORBOX
            /// </summary>
            SEM_NOOPENFILEERRORBOX = 0x8000
        }
    }
}
