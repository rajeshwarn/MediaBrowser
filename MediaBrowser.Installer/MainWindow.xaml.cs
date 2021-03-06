﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;
using Ionic.Zip;
using MediaBrowser.Installer.Code;
using Microsoft.Win32;
using ServiceStack.Text;

namespace MediaBrowser.Installer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        protected PackageVersionClass PackageClass = PackageVersionClass.Release;
        protected Version RequestedVersion = new Version(4,0,0,0);
        protected Version ActualVersion;
        protected string PackageName = "MBServer";
        protected string RootSuffix = "-Server";
        protected string TargetExe = "MediaBrowser.ServerApplication.exe";
        protected string FriendlyName = "Media Browser Server";
        protected string Archive = null;
        protected bool InstallPismo = true;
        protected string RootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "MediaBrowser-Server");

        protected bool SystemClosing = false;

        protected string TempLocation = Path.Combine(Path.GetTempPath(), "MediaBrowser");

        protected WebClient MainClient = new WebClient();

        public MainWindow()
        {
            try
            {
                GetArgs();
                InitializeComponent();
                DoInstall(Archive);
            }
            catch (Exception e)
            {
                MessageBox.Show("Error: " + e.Message + " \n\n" + e.StackTrace);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!SystemClosing && MessageBox.Show("Cancel Installation - Are you sure?", "Cancel", MessageBoxButton.YesNo) == MessageBoxResult.No)
            {
                e.Cancel = true;
            }
            if (MainClient.IsBusy)
            {
                MainClient.CancelAsync();
                while (MainClient.IsBusy)
                {
                    // wait to finish
                }
            }
            MainClient.Dispose();
            ClearTempLocation(TempLocation);
            base.OnClosing(e);
        }

        protected void SystemClose(string message = null)
        {
            if (message != null)
            {
                MessageBox.Show(message, "Error");
            }
            SystemClosing = true;
            this.Close();
        }

        protected void GetArgs()
        {
            //cmd line args should be name/value pairs like: product=server archive="c:\.." caller=34552
            var cmdArgs = Environment.GetCommandLineArgs();
            var args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in cmdArgs)
            {
                var nameValue = pair.Split('=');
                if (nameValue.Length == 2)
                {
                    args[nameValue[0]] = nameValue[1];
                }
            }
            Archive = args.GetValueOrDefault("archive", null);
            if (args.GetValueOrDefault("pismo","true") == "false") InstallPismo = false;

            var product = args.GetValueOrDefault("product", null) ?? ConfigurationManager.AppSettings["product"] ?? "server";
            PackageClass = (PackageVersionClass) Enum.Parse(typeof (PackageVersionClass), args.GetValueOrDefault("class", null) ?? ConfigurationManager.AppSettings["class"] ?? "Release");
            RequestedVersion = new Version(args.GetValueOrDefault("version", "4.0"));

            var callerId = args.GetValueOrDefault("caller", null);
            if (callerId != null)
            {
                // Wait for our caller to exit
                try
                {
                    var process = Process.GetProcessById(Convert.ToInt32(callerId));
                    process.WaitForExit();
                }
                catch (ArgumentException)
                {
                    // wasn't running
                }
            }

            //MessageBox.Show(string.Format("Called with args: product: {0} archive: {1} caller: {2}", product, Archive, callerId));
            
            switch (product.ToLower())
            {
                case "mbt":
                    PackageName = "MBTheater";
                    RootSuffix = "-Theater";
                    TargetExe = "MediaBrowser.UI.exe";
                    FriendlyName = "Media Browser Theater";
                    break;

                default:
                    PackageName = "MBServer";
                    RootSuffix = "-Server";
                    TargetExe = "MediaBrowser.ServerApplication.exe";
                    FriendlyName = "Media Browser Server";
                    break;
            }

            RootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "MediaBrowser" + RootSuffix);

        }

        /// <summary>
        /// Execute the install process
        /// </summary>
        /// <returns></returns>
        protected async Task DoInstall(string archive)
        {
            lblStatus.Text = string.Format("Installing {0}...", FriendlyName);

            // Determine Package version
            var version = archive == null ? await GetPackageVersion() : null;
            ActualVersion = version != null ? version.version : new Version(3,0);

            // Now try and shut down the server if that is what we are installing and it is running
            var procs = Process.GetProcessesByName("mediabrowser.serverapplication");
            var server = procs.Length > 0 ? procs[0] : null;
            if (PackageName == "MBServer" && server != null)
            {
                lblStatus.Text = "Shutting Down Media Browser Server...";
                using (var client = new WebClient())
                {
                    try
                    {
                        client.UploadString("http://localhost:8096/mediabrowser/System/Shutdown", "");
                        try
                        {
                            server.WaitForExit();
                        }
                        catch (ArgumentException)
                        {
                            // already gone
                        }
                    }
                    catch (WebException e)
                    {
                        if (e.Status == WebExceptionStatus.Timeout || e.Message.StartsWith("Unable to connect",StringComparison.OrdinalIgnoreCase)) return; // just wasn't running

                        MessageBox.Show("Error shutting down server. Please be sure it is not running before hitting OK.\n\n" + e.Status + "\n\n" + e.Message);
                    }
                }
            }
            else
            {
                if (PackageName == "MBTheater")
                {
                    // Uninstalling MBT - shut it down if it is running
                    var processes = Process.GetProcessesByName("mediabrowser.ui");
                    if (processes.Length > 0)
                    {
                        lblStatus.Text = "Shutting Down Media Browser Theater...";
                        try
                        {
                            processes[0].Kill();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Unable to shutdown Media Browser Theater.  Please ensure it is not running before hitting OK.\n\n" + ex.Message, "Error");
                        }
                    }
                    
                }
            }

            // Download if we don't already have it
            if (archive == null)
            {
                lblStatus.Text = string.Format("Downloading {0} (version {1})...", FriendlyName, version.versionStr);
                try
                {
                    archive = await DownloadPackage(version);
                }
                catch (Exception e)
                {
                    SystemClose("Error Downloading Package - " + e.GetType().FullName + "\n\n" + e.Message);
                    return;
                }
            }

            if (archive == null) return;  //we canceled or had an error that was already reported

            // Extract
            lblStatus.Text = "Extracting Package...";
            try 
            {
                ExtractPackage(archive);
                // We're done with it so delete it (this is necessary for update operations)
                try
                {
                    File.Delete(archive);
                }
                catch (FileNotFoundException)
                {
                }
                catch (Exception e)
                {

                    SystemClose("Error Removing Archive - " + e.GetType().FullName + "\n\n" + e.Message);
                    return;
                }
            }
            catch (Exception e)
            {
                SystemClose("Error Extracting - " + e.GetType().FullName + "\n\n" + e.Message);
                return;
            }

            // Create shortcut
            lblStatus.Text = "Creating Shortcuts...";
            var fullPath = Path.Combine(RootPath, "System", TargetExe);
            try
            {
                CreateShortcuts(fullPath);
            }
            catch (Exception e)
            {
                SystemClose("Error Creating Shortcut - "+e.GetType().FullName+"\n\n"+e.Message);
                return;
            }

            // Install Pismo
            if (InstallPismo)
            {
                lblStatus.Text = "Installing ISO Support...";
                try
                {
                    PismoInstall();
                }
                catch (Exception e)
                {
                    SystemClose("Error Installing Pismo - "+e.GetType().FullName+"\n\n"+e.Message);
                    return;
                }
            }

            // Now delete the pismo install files
            Directory.Delete(Path.Combine(RootPath, "Pismo"), true);

            // And run
            lblStatus.Text = string.Format("Starting {0}...", FriendlyName);
            try
            {
                Process.Start(fullPath);
            }
            catch (Exception e)
            {
                SystemClose("Error Executing - "+fullPath+ " "+e.GetType().FullName+"\n\n"+e.Message);
                return;
            }

            SystemClose();

        }

        private void PismoInstall()
        {
            // Kick off the Pismo installer and wait for it to end
            var pismo = new Process();
            pismo.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            pismo.StartInfo.FileName = Path.Combine(RootPath, "Pismo", "pfminst.exe");
            pismo.StartInfo.Arguments = "install";
            pismo.Start();
            pismo.WaitForExit();

        }

        protected async Task<PackageVersionInfo> GetPackageVersion()
        {
            try
            {
                // get the package information for the server
                var json = await MainClient.DownloadStringTaskAsync("http://www.mb3admin.com/admin/service/package/retrieveAll?name=" + PackageName);
                var packages = JsonSerializer.DeserializeFromString<List<PackageInfo>>(json);

                var version = packages[0].versions.Where(v => v.classification <= PackageClass).OrderByDescending(v => v.version).FirstOrDefault(v => v.version <= RequestedVersion);
                if (version == null)
                {
                    SystemClose("Could not locate download package.  Aborting.");
                    return null;
                }
                return version;
            }
            catch (Exception e)
            {
                SystemClose(e.GetType().FullName + "\n\n" + e.Message);
            }

            return null;
        }

        /// <summary>
        /// Download our specified package to an archive in a temp location
        /// </summary>
        /// <returns>The fully qualified name of the downloaded package</returns>
        protected async Task<string> DownloadPackage(PackageVersionInfo version)
        {
            var success = false;
            var retryCount = 0;
            var archiveFile = Path.Combine(PrepareTempLocation(), version.targetFilename);

            try
            {
                while (!success && retryCount < 3)
                {

                    // setup download progress and download the package
                    MainClient.DownloadProgressChanged += DownloadProgressChanged;
                    try
                    {
                        await MainClient.DownloadFileTaskAsync(version.sourceUrl, archiveFile);
                        success = true;
                    }
                    catch (WebException e)
                    {
                        if (e.Status == WebExceptionStatus.RequestCanceled)
                        {
                            return null;
                        }
                        if (retryCount < 3 && (e.Status == WebExceptionStatus.Timeout || e.Status == WebExceptionStatus.ConnectFailure || e.Status == WebExceptionStatus.ProtocolError))
                        {
                            Thread.Sleep(500); //wait just a sec
                            PrepareTempLocation(); //clear this out
                            retryCount++;
                        }
                        else
                        {
                            throw;
                        }
                    }
                }

                return archiveFile;
            }
            catch (Exception e)
            {
                SystemClose(e.GetType().FullName + "\n\n" + e.Message);
            }
            return "";

        }

        void DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            rectProgress.Width = (this.Width * e.ProgressPercentage)/100f;
        }

        /// <summary>
        /// Extract the provided archive to our program root
        /// It is assumed the archive is a zip file relative to that root (with all necessary sub-folders)
        /// </summary>
        /// <param name="archive"></param>
        protected void ExtractPackage(string archive)
        {
            // Delete old content of system
            var systemDir = Path.Combine(RootPath, "system");
            if (Directory.Exists(systemDir))
            {
                try
                {
                    Directory.Delete(systemDir, true);
                }
                catch
                {
                    // we tried...
                }
            }

            // And extract
            var retryCount = 0;
            var success = false;
            while (!success && retryCount < 3)
            {
                try
                {
                    using (var fileStream = File.OpenRead(archive))
                    {
                        using (var zipFile = ZipFile.Read(fileStream))
                        {
                            zipFile.ExtractAll(RootPath, ExtractExistingFileAction.OverwriteSilently);
                            success = true;
                        }
                    }
                }
                catch
                {
                    if (retryCount < 3)
                    {
                        Thread.Sleep(250);
                        retryCount++;
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Create a shortcut in the current user's start menu
        ///  Only do current user to avoid need for admin elevation
        /// </summary>
        /// <param name="targetExe"></param>
        protected void CreateShortcuts(string targetExe)
        {
            // get path to all users start menu
            var startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),"Media Browser 3");
            if (!Directory.Exists(startMenu)) Directory.CreateDirectory(startMenu);
            var product = new ShellShortcut(Path.Combine(startMenu, FriendlyName+".lnk")) {Path = targetExe, Description = "Run " + FriendlyName};
            product.Save();

            if (PackageName == "MBServer")
            {
                var path = Path.Combine(startMenu, "MB Dashboard.lnk");
                var dashboard = new ShellShortcut(path) 
                {Path = @"http://localhost:8096/mediabrowser/dashboard/dashboard.html", Description = "Open the Media Browser Server Dashboard (configuration)"};
                dashboard.Save();
                
            }
            CreateUninstaller(Path.Combine(Path.GetDirectoryName(targetExe) ?? "", "MediaBrowser.Uninstaller.exe")+ " "+ (PackageName == "MBServer" ? "server" : "mbt"), targetExe);

        }

        /// <summary>
        /// Create uninstall entry in add/remove
        /// </summary>
        /// <param name="uninstallPath"></param>
        /// <param name="targetExe"></param>
        private void CreateUninstaller(string uninstallPath, string targetExe)
        {
            using (var parent = Registry.CurrentUser.OpenSubKey(
                         @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", true))
            {
                if (parent == null)
                {
                    MessageBox.Show("Uninstall registry key not found.");
                    return;
                }
                try
                {
                    RegistryKey key = null;

                    try
                    {
                        const string guidText = "{4E76DB4E-1BB9-4A7B-860C-7940779CF7A0}";
                        key = parent.OpenSubKey(guidText, true) ??
                              parent.CreateSubKey(guidText);

                        if (key == null)
                        {
                            MessageBox.Show(String.Format("Unable to create uninstaller entry'{0}\\{1}'", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", guidText));
                            return;
                        }

                        key.SetValue("DisplayName", FriendlyName);
                        key.SetValue("ApplicationVersion", ActualVersion);
                        key.SetValue("Publisher", "Media Browser Team");
                        key.SetValue("DisplayIcon", targetExe);
                        key.SetValue("DisplayVersion", ActualVersion.ToString(2));
                        key.SetValue("URLInfoAbout", "http://www.mediabrowser3.com");
                        key.SetValue("Contact", "http://community.mediabrowser.tv");
                        key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                        key.SetValue("UninstallString", uninstallPath);
                    }
                    finally
                    {
                        if (key != null)
                        {
                            key.Close();
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("An error occurred writing uninstall information to the registry.");
                }
            }
        }        
        
        /// <summary>
        /// Prepare a temporary location to download to
        /// </summary>
        /// <returns>The path to the temporary location</returns>
        protected string PrepareTempLocation()
        {
            ClearTempLocation(TempLocation);
            Directory.CreateDirectory(TempLocation);
            return TempLocation;
        }

        /// <summary>
        /// Clear out (delete recursively) the supplied temp location
        /// </summary>
        /// <param name="location"></param>
        protected void ClearTempLocation(string location)
        {
            if (Directory.Exists(location))
            {
                Directory.Delete(location, true);
            }
        }

    }
}
