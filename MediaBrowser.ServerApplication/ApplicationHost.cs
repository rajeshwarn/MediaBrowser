﻿using System.Diagnostics;
using System.Net.Cache;
using System.Net.Http;
using System.Net.Sockets;
using MediaBrowser.Api;
using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Constants;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Common.Implementations;
using MediaBrowser.Common.Implementations.ScheduledTasks;
using MediaBrowser.Common.IO;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.IO;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Localization;
using MediaBrowser.Controller.MediaInfo;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Resolvers;
using MediaBrowser.Controller.Sorting;
using MediaBrowser.Controller.Updates;
using MediaBrowser.Controller.Weather;
using MediaBrowser.IsoMounter;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.System;
using MediaBrowser.Model.Updates;
using MediaBrowser.Server.Implementations;
using MediaBrowser.Server.Implementations.BdInfo;
using MediaBrowser.Server.Implementations.Configuration;
using MediaBrowser.Server.Implementations.HttpServer;
using MediaBrowser.Server.Implementations.IO;
using MediaBrowser.Server.Implementations.Library;
using MediaBrowser.Server.Implementations.Providers;
using MediaBrowser.Server.Implementations.ServerManager;
using MediaBrowser.Server.Implementations.Udp;
using MediaBrowser.Server.Implementations.Updates;
using MediaBrowser.Server.Implementations.WebSocket;
using MediaBrowser.ServerApplication.Implementations;
using MediaBrowser.WebDashboard.Api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.ServerApplication
{
    /// <summary>
    /// Class CompositionRoot
    /// </summary>
    public class ApplicationHost : BaseApplicationHost<ServerApplicationPaths>, IServerApplicationHost
    {
        private const int UdpServerPort = 7359;

        /// <summary>
        /// Gets the server kernel.
        /// </summary>
        /// <value>The server kernel.</value>
        protected Kernel ServerKernel { get; set; }

        /// <summary>
        /// Gets the server configuration manager.
        /// </summary>
        /// <value>The server configuration manager.</value>
        public IServerConfigurationManager ServerConfigurationManager
        {
            get { return (IServerConfigurationManager)ConfigurationManager; }
        }

        /// <summary>
        /// Gets the name of the log file prefix.
        /// </summary>
        /// <value>The name of the log file prefix.</value>
        protected override string LogFilePrefixName
        {
            get { return "Server"; }
        }

        /// <summary>
        /// Gets the configuration manager.
        /// </summary>
        /// <returns>IConfigurationManager.</returns>
        protected override IConfigurationManager GetConfigurationManager()
        {
            return new ServerConfigurationManager(ApplicationPaths, LogManager, XmlSerializer);
        }

        /// <summary>
        /// Gets or sets the installation manager.
        /// </summary>
        /// <value>The installation manager.</value>
        private IInstallationManager InstallationManager { get; set; }
        /// <summary>
        /// Gets or sets the server manager.
        /// </summary>
        /// <value>The server manager.</value>
        private IServerManager ServerManager { get; set; }
        /// <summary>
        /// Gets or sets the user manager.
        /// </summary>
        /// <value>The user manager.</value>
        public IUserManager UserManager { get; set; }
        /// <summary>
        /// Gets or sets the library manager.
        /// </summary>
        /// <value>The library manager.</value>
        internal ILibraryManager LibraryManager { get; set; }
        /// <summary>
        /// Gets or sets the directory watchers.
        /// </summary>
        /// <value>The directory watchers.</value>
        private IDirectoryWatchers DirectoryWatchers { get; set; }
        /// <summary>
        /// Gets or sets the provider manager.
        /// </summary>
        /// <value>The provider manager.</value>
        private IProviderManager ProviderManager { get; set; }
        /// <summary>
        /// Gets or sets the zip client.
        /// </summary>
        /// <value>The zip client.</value>
        private IZipClient ZipClient { get; set; }
        /// <summary>
        /// Gets or sets the HTTP server.
        /// </summary>
        /// <value>The HTTP server.</value>
        private IHttpServer HttpServer { get; set; }

        /// <summary>
        /// Gets or sets the UDP server.
        /// </summary>
        /// <value>The UDP server.</value>
        private UdpServer UdpServer { get; set; }

        /// <summary>
        /// The full path to our startmenu shortcut
        /// </summary>
        protected override string ProductShortcutPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Media Browser 3", "Media Browser Server.lnk"); }
        }

        /// <summary>
        /// Runs the startup tasks.
        /// </summary>
        /// <returns>Task.</returns>
        protected override async Task RunStartupTasks()
        {
            // Do these before allowing the base method to run, which will invoke startup scheduled tasks
            await ServerKernel.LoadRepositories(ServerConfigurationManager).ConfigureAwait(false);

            await base.RunStartupTasks().ConfigureAwait(false);

            DirectoryWatchers.Start();

            Parallel.ForEach(GetExports<IServerEntryPoint>(), entryPoint => entryPoint.Run());
        }

        /// <summary>
        /// Registers resources that classes will depend on
        /// </summary>
        /// <returns>Task.</returns>
        protected override async Task RegisterResources()
        {
            ServerKernel = new Kernel(ServerConfigurationManager);

            await base.RegisterResources().ConfigureAwait(false);

            RegisterSingleInstance<IHttpResultFactory>(new HttpResultFactory(LogManager));

            RegisterSingleInstance<IServerApplicationHost>(this);
            RegisterSingleInstance<IServerApplicationPaths>(ApplicationPaths);

            RegisterSingleInstance(ServerKernel);
            RegisterSingleInstance(ServerConfigurationManager);

            RegisterSingleInstance<IWebSocketServer>(() => new AlchemyServer(Logger));

            RegisterSingleInstance<IIsoManager>(new PismoIsoManager(Logger));
            RegisterSingleInstance<IBlurayExaminer>(new BdInfoExaminer());

            ZipClient = new DotNetZipClient();
            RegisterSingleInstance(ZipClient);

            HttpServer = ServerFactory.CreateServer(this, ProtobufSerializer, Logger, "Media Browser", "index.html");
            RegisterSingleInstance(HttpServer, false);

            ServerManager = new ServerManager(this, JsonSerializer, Logger, ServerConfigurationManager, ServerKernel);
            RegisterSingleInstance(ServerManager);

            UserManager = new UserManager(ServerKernel, Logger, ServerConfigurationManager);
            RegisterSingleInstance(UserManager);

            LibraryManager = new LibraryManager(ServerKernel, Logger, TaskManager, UserManager, ServerConfigurationManager);
            RegisterSingleInstance(LibraryManager);

            InstallationManager = new InstallationManager(HttpClient, PackageManager, JsonSerializer, Logger, this);
            RegisterSingleInstance(InstallationManager);

            DirectoryWatchers = new DirectoryWatchers(LogManager, TaskManager, LibraryManager, ServerConfigurationManager);
            RegisterSingleInstance(DirectoryWatchers);

            ProviderManager = new ProviderManager(HttpClient, ServerConfigurationManager, DirectoryWatchers, LogManager);
            RegisterSingleInstance(ProviderManager);

            RegisterSingleInstance<ILibrarySearchEngine>(() => new LuceneSearchEngine());
            
            SetKernelProperties();
            SetStaticProperties();
        }

        /// <summary>
        /// Sets the kernel properties.
        /// </summary>
        private void SetKernelProperties()
        {
            ServerKernel.FFMpegManager = new FFMpegManager(ServerKernel, ZipClient, JsonSerializer, ProtobufSerializer, LogManager, ApplicationPaths);
            ServerKernel.ImageManager = new ImageManager(ServerKernel, ProtobufSerializer, LogManager.GetLogger("ImageManager"), ApplicationPaths);

            Parallel.Invoke(
                () => ServerKernel.UserDataRepositories = GetExports<IUserDataRepository>(),
                () => ServerKernel.UserRepositories = GetExports<IUserRepository>(),
                () => ServerKernel.DisplayPreferencesRepositories = GetExports<IDisplayPreferencesRepository>(),
                () => ServerKernel.ItemRepositories = GetExports<IItemRepository>(),
                () => ServerKernel.WeatherProviders = GetExports<IWeatherProvider>(),
                () => ServerKernel.ImageEnhancers = GetExports<IImageEnhancer>().OrderBy(e => e.Priority).ToArray(),
                () => ServerKernel.StringFiles = GetExports<LocalizedStringData>()
                );
        }

        /// <summary>
        /// Dirty hacks
        /// </summary>
        private void SetStaticProperties()
        {
            // For now there's no real way to inject these properly
            BaseItem.Logger = LogManager.GetLogger("BaseItem");
            BaseItem.ConfigurationManager = ServerConfigurationManager;
            BaseItem.LibraryManager = LibraryManager;
            BaseItem.ProviderManager = ProviderManager;
            User.XmlSerializer = XmlSerializer;
            User.UserManager = UserManager;
            Ratings.ConfigurationManager = ServerConfigurationManager;
            LocalizedStrings.ApplicationPaths = ApplicationPaths;
        }

        /// <summary>
        /// Finds the parts.
        /// </summary>
        protected override void FindParts()
        {
            base.FindParts();

            if (IsFirstRun)
            {
                RegisterServerWithAdministratorAccess();
            }

            Parallel.Invoke(

                () =>
                {
                    HttpServer.Init(GetExports<IRestfulService>(false));

                    ServerManager.AddWebSocketListeners(GetExports<IWebSocketListener>(false));
                    ServerManager.Start();
                },

                () => LibraryManager.AddParts(GetExports<IResolverIgnoreRule>(), GetExports<IVirtualFolderCreator>(), GetExports<IItemResolver>(), GetExports<IIntroProvider>(), GetExports<IBaseItemComparer>()),

                () => ProviderManager.AddMetadataProviders(GetExports<BaseMetadataProvider>().OrderBy(e => e.Priority).ToArray())
                );

            UdpServer = new UdpServer(Logger, NetworkManager, ServerConfigurationManager);

            try
            {
                UdpServer.Start(UdpServerPort);
            }
            catch (SocketException ex)
            {
                Logger.ErrorException("Failed to start UDP Server", ex);
            }
        }

        /// <summary>
        /// Restarts this instance.
        /// </summary>
        public override void Restart()
        {
            App.Instance.Restart();
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance can self update.
        /// </summary>
        /// <value><c>true</c> if this instance can self update; otherwise, <c>false</c>.</value>
        public override bool CanSelfUpdate
        {
            get { return ConfigurationManager.CommonConfiguration.EnableAutoUpdate; }
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="dispose"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected override void Dispose(bool dispose)
        {
            if (dispose)
            {
                if (UdpServer != null)
                {
                    UdpServer.Dispose();
                }
            }

            base.Dispose(dispose);
        }

        /// <summary>
        /// Checks for update.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="progress">The progress.</param>
        /// <returns>Task{CheckForUpdateResult}.</returns>
        public async override Task<CheckForUpdateResult> CheckForApplicationUpdate(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var availablePackages = await PackageManager.GetAvailablePackages(CancellationToken.None).ConfigureAwait(false);
            var version = InstallationManager.GetLatestCompatibleVersion(availablePackages, Constants.MBServerPkgName, ConfigurationManager.CommonConfiguration.SystemUpdateLevel);

            return version != null ? new CheckForUpdateResult { AvailableVersion = version.version, IsUpdateAvailable = version.version > ApplicationVersion, Package = version } :
                       new CheckForUpdateResult { AvailableVersion = ApplicationVersion, IsUpdateAvailable = false };
        }

        /// <summary>
        /// Gets the composable part assemblies.
        /// </summary>
        /// <returns>IEnumerable{Assembly}.</returns>
        protected override IEnumerable<Assembly> GetComposablePartAssemblies()
        {
            // Gets all plugin assemblies by first reading all bytes of the .dll and calling Assembly.Load against that
            // This will prevent the .dll file from getting locked, and allow us to replace it when needed
            foreach (var pluginAssembly in Directory
                .EnumerateFiles(ApplicationPaths.PluginsPath, "*.dll", SearchOption.TopDirectoryOnly)
                .Select(LoadAssembly).Where(a => a != null))
            {
                yield return pluginAssembly;
            }

            // Include composable parts in the Api assembly 
            yield return typeof(ApiEntryPoint).Assembly;

            // Include composable parts in the Dashboard assembly 
            yield return typeof(DashboardInfo).Assembly;

            // Include composable parts in the Model assembly 
            yield return typeof(SystemInfo).Assembly;

            // Include composable parts in the Common assembly 
            yield return typeof(IApplicationHost).Assembly;

            // Include composable parts in the Controller assembly 
            yield return typeof(Kernel).Assembly;

            // Common implementations
            yield return typeof(TaskManager).Assembly;

            // Server implementations
            yield return typeof(ServerApplicationPaths).Assembly;

            // Include composable parts in the running assembly
            yield return GetType().Assembly;
        }

        private readonly Guid _systemId = Environment.MachineName.GetMD5();

        /// <summary>
        /// Gets the system status.
        /// </summary>
        /// <returns>SystemInfo.</returns>
        public virtual SystemInfo GetSystemInfo()
        {
            return new SystemInfo
            {
                HasPendingRestart = HasPendingRestart,
                Version = ApplicationVersion.ToString(),
                IsNetworkDeployed = CanSelfUpdate,
                WebSocketPortNumber = ServerManager.WebSocketPortNumber,
                SupportsNativeWebSocket = ServerManager.SupportsNativeWebSocket,
                FailedPluginAssemblies = FailedAssemblies.ToArray(),
                InProgressInstallations = InstallationManager.CurrentInstallations.Select(i => i.Item1).ToArray(),
                CompletedInstallations = InstallationManager.CompletedInstallations.ToArray(),
                Id = _systemId
            };
        }

        /// <summary>
        /// Shuts down.
        /// </summary>
        public override void Shutdown()
        {
            App.Instance.Dispatcher.Invoke(App.Instance.Shutdown);
        }			        
        
        /// <summary>
        /// Registers the server with administrator access.
        /// </summary>
        private void RegisterServerWithAdministratorAccess()
        {
            Logger.Info("Requesting administrative access to authorize http server");

            // Create a temp file path to extract the bat file to
            var tmpFile = Path.Combine(ConfigurationManager.CommonApplicationPaths.TempDirectory, Guid.NewGuid() + ".bat");

            // Extract the bat file
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MediaBrowser.ServerApplication.RegisterServer.bat"))
            {
                using (var fileStream = File.Create(tmpFile))
                {
                    stream.CopyTo(fileStream);
                }
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = tmpFile,

                Arguments = string.Format("{0} {1} {2} {3}", ServerConfigurationManager.Configuration.HttpServerPortNumber,
                ServerKernel.HttpServerUrlPrefix,
                UdpServerPort,
                ServerConfigurationManager.Configuration.LegacyWebSocketPortNumber),

                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                Verb = "runas",
                ErrorDialog = false
            };

            using (var process = Process.Start(startInfo))
            {
                process.WaitForExit();
            }
        }
    }
}
