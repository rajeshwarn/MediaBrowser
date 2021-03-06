﻿using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.IO;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Localization;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Resolvers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.Entities
{
    /// <summary>
    /// Class BaseItem
    /// </summary>
    public abstract class BaseItem : IHasProviderIds
    {
        /// <summary>
        /// The trailer folder name
        /// </summary>
        public const string TrailerFolderName = "trailers";

        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public virtual string Name { get; set; }

        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <value>The id.</value>
        public virtual Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the path.
        /// </summary>
        /// <value>The path.</value>
        public virtual string Path { get; set; }

        /// <summary>
        /// Gets or sets the type of the location.
        /// </summary>
        /// <value>The type of the location.</value>
        public virtual LocationType LocationType
        {
            get
            {
                if (string.IsNullOrEmpty(Path))
                {
                    return LocationType.Virtual;
                }

                return System.IO.Path.IsPathRooted(Path) ? LocationType.FileSystem : LocationType.Remote;
            }
        }

        /// <summary>
        /// This is just a helper for convenience
        /// </summary>
        /// <value>The primary image path.</value>
        [IgnoreDataMember]
        public virtual string PrimaryImagePath
        {
            get { return GetImage(ImageType.Primary); }
            set { SetImage(ImageType.Primary, value); }
        }

        /// <summary>
        /// Gets or sets the images.
        /// </summary>
        /// <value>The images.</value>
        public Dictionary<string, string> Images { get; set; }

        /// <summary>
        /// Gets or sets the date created.
        /// </summary>
        /// <value>The date created.</value>
        public DateTime DateCreated { get; set; }

        /// <summary>
        /// Gets or sets the date modified.
        /// </summary>
        /// <value>The date modified.</value>
        public DateTime DateModified { get; set; }

        /// <summary>
        /// The logger
        /// </summary>
        public static ILogger Logger { get; set; }
        public static ILibraryManager LibraryManager { get; set; }
        public static IServerConfigurationManager ConfigurationManager { get; set; }
        public static IProviderManager ProviderManager { get; set; }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>A <see cref="System.String" /> that represents this instance.</returns>
        public override string ToString()
        {
            return Name;
        }

        /// <summary>
        /// Returns true if this item should not attempt to fetch metadata
        /// </summary>
        /// <value><c>true</c> if [dont fetch meta]; otherwise, <c>false</c>.</value>
        [IgnoreDataMember]
        public virtual bool DontFetchMeta
        {
            get
            {
                if (Path != null)
                {
                    return Path.IndexOf("[dontfetchmeta]", StringComparison.OrdinalIgnoreCase) != -1;
                }

                return false;
            }
        }

        /// <summary>
        /// Determines whether the item has a saved local image of the specified name (jpg or png).
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns><c>true</c> if [has local image] [the specified item]; otherwise, <c>false</c>.</returns>
        /// <exception cref="System.ArgumentNullException">name</exception>
        public bool HasLocalImage(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException("name");
            }

            return ResolveArgs.ContainsMetaFileByName(name + ".jpg") ||
                ResolveArgs.ContainsMetaFileByName(name + ".png");
        }

        /// <summary>
        /// Should be overridden to return the proper folder where metadata lives
        /// </summary>
        /// <value>The meta location.</value>
        [IgnoreDataMember]
        public virtual string MetaLocation
        {
            get
            {
                return Path ?? "";
            }
        }

        /// <summary>
        /// The _provider data
        /// </summary>
        private Dictionary<Guid, BaseProviderInfo> _providerData;
        /// <summary>
        /// Holds persistent data for providers like last refresh date.
        /// Providers can use this to determine if they need to refresh.
        /// The BaseProviderInfo class can be extended to hold anything a provider may need.
        /// Keyed by a unique provider ID.
        /// </summary>
        /// <value>The provider data.</value>
        public Dictionary<Guid, BaseProviderInfo> ProviderData
        {
            get
            {
                return _providerData ?? (_providerData = new Dictionary<Guid, BaseProviderInfo>());
            }
            set
            {
                _providerData = value;
            }
        }

        /// <summary>
        /// The _file system stamp
        /// </summary>
        private Guid? _fileSystemStamp;
        /// <summary>
        /// Gets a directory stamp, in the form of a string, that can be used for
        /// comparison purposes to determine if the file system entries for this item have changed.
        /// </summary>
        /// <value>The file system stamp.</value>
        [IgnoreDataMember]
        public Guid FileSystemStamp
        {
            get
            {
                if (!_fileSystemStamp.HasValue)
                {
                    _fileSystemStamp = GetFileSystemStamp();
                }

                return _fileSystemStamp.Value;
            }
        }

        /// <summary>
        /// Gets the type of the media.
        /// </summary>
        /// <value>The type of the media.</value>
        [IgnoreDataMember]
        public virtual string MediaType
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// Gets a directory stamp, in the form of a string, that can be used for
        /// comparison purposes to determine if the file system entries for this item have changed.
        /// </summary>
        /// <returns>Guid.</returns>
        private Guid GetFileSystemStamp()
        {
            // If there's no path or the item is a file, there's nothing to do
            if (LocationType != LocationType.FileSystem || !ResolveArgs.IsDirectory)
            {
                return Guid.Empty;
            }

            var sb = new StringBuilder();

            // Record the name of each file 
            // Need to sort these because accoring to msdn docs, our i/o methods are not guaranteed in any order
            foreach (var file in ResolveArgs.FileSystemChildren.OrderBy(f => f.cFileName))
            {
                sb.Append(file.cFileName);
            }
            foreach (var file in ResolveArgs.MetadataFiles.OrderBy(f => f.cFileName))
            {
                sb.Append(file.cFileName);
            }

            return sb.ToString().GetMD5();
        }

        /// <summary>
        /// The _resolve args
        /// </summary>
        private ItemResolveArgs _resolveArgs;
        /// <summary>
        /// The _resolve args initialized
        /// </summary>
        private bool _resolveArgsInitialized;
        /// <summary>
        /// The _resolve args sync lock
        /// </summary>
        private object _resolveArgsSyncLock = new object();
        /// <summary>
        /// We attach these to the item so that we only ever have to hit the file system once
        /// (this includes the children of the containing folder)
        /// Use ResolveArgs.FileSystemDictionary to check for the existence of files instead of File.Exists
        /// </summary>
        /// <value>The resolve args.</value>
        [IgnoreDataMember]
        public ItemResolveArgs ResolveArgs
        {
            get
            {
                try
                {
                    LazyInitializer.EnsureInitialized(ref _resolveArgs, ref _resolveArgsInitialized, ref _resolveArgsSyncLock, () => CreateResolveArgs());

                }
                catch (IOException ex)
                {
                    Logger.ErrorException("Error creating resolve args for ", ex, Path);

                    throw;
                }

                return _resolveArgs;
            }
            set
            {
                _resolveArgs = value;
                _resolveArgsInitialized = value != null;

                // Null this out so that it can be lazy loaded again
                _fileSystemStamp = null;
            }
        }

        /// <summary>
        /// Resets the resolve args.
        /// </summary>
        /// <param name="pathInfo">The path info.</param>
        public void ResetResolveArgs(WIN32_FIND_DATA? pathInfo)
        {
            ResolveArgs = CreateResolveArgs(pathInfo);
        }

        /// <summary>
        /// Creates ResolveArgs on demand
        /// </summary>
        /// <param name="pathInfo">The path info.</param>
        /// <returns>ItemResolveArgs.</returns>
        /// <exception cref="System.IO.IOException">Unable to retrieve file system info for  + path</exception>
        protected internal virtual ItemResolveArgs CreateResolveArgs(WIN32_FIND_DATA? pathInfo = null)
        {
            var path = Path;

            // non file-system entries will not have a path
            if (this.LocationType != LocationType.FileSystem || string.IsNullOrEmpty(path))
            {
                return new ItemResolveArgs(ConfigurationManager.ApplicationPaths)
                {
                    FileInfo = new WIN32_FIND_DATA()
                };
            }

            if (UseParentPathToCreateResolveArgs)
            {
                path = System.IO.Path.GetDirectoryName(path);
            }

            pathInfo = pathInfo ?? FileSystem.GetFileData(path);

            if (!pathInfo.HasValue)
            {
                throw new IOException("Unable to retrieve file system info for " + path);
            }

            var args = new ItemResolveArgs(ConfigurationManager.ApplicationPaths)
            {
                FileInfo = pathInfo.Value,
                Path = path,
                Parent = Parent
            };

            // Gather child folder and files

            if (args.IsDirectory)
            {
                // When resolving the root, we need it's grandchildren (children of user views)
                var flattenFolderDepth = args.IsPhysicalRoot ? 2 : 0;

                args.FileSystemDictionary = FileData.GetFilteredFileSystemEntries(args.Path, Logger, flattenFolderDepth: flattenFolderDepth, args: args);
            }

            //update our dates
            EntityResolutionHelper.EnsureDates(this, args);

            return args;
        }

        /// <summary>
        /// Some subclasses will stop resolving at a directory and point their Path to a file within. This will help ensure the on-demand resolve args are identical to the
        /// original ones.
        /// </summary>
        /// <value><c>true</c> if [use parent path to create resolve args]; otherwise, <c>false</c>.</value>
        [IgnoreDataMember]
        protected virtual bool UseParentPathToCreateResolveArgs
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets or sets the name of the forced sort.
        /// </summary>
        /// <value>The name of the forced sort.</value>
        public string ForcedSortName { get; set; }

        private string _sortName;
        /// <summary>
        /// Gets or sets the name of the sort.
        /// </summary>
        /// <value>The name of the sort.</value>
        [IgnoreDataMember]
        public string SortName
        {
            get
            {
                return ForcedSortName ?? _sortName ?? (_sortName = CreateSortName());
            }
        }

        /// <summary>
        /// Creates the name of the sort.
        /// </summary>
        /// <returns>System.String.</returns>
        protected virtual string CreateSortName()
        {
            if (Name == null) return null; //some items may not have name filled in properly

            var sortable = Name.Trim().ToLower();
            sortable = ConfigurationManager.Configuration.SortRemoveCharacters.Aggregate(sortable, (current, search) => current.Replace(search.ToLower(), string.Empty));

            sortable = ConfigurationManager.Configuration.SortReplaceCharacters.Aggregate(sortable, (current, search) => current.Replace(search.ToLower(), " "));

            foreach (var search in ConfigurationManager.Configuration.SortRemoveWords)
            {
                var searchLower = search.ToLower();
                // Remove from beginning if a space follows
                if (sortable.StartsWith(searchLower + " "))
                {
                    sortable = sortable.Remove(0, searchLower.Length + 1);
                }
                // Remove from middle if surrounded by spaces
                sortable = sortable.Replace(" " + searchLower + " ", " ");

                // Remove from end if followed by a space
                if (sortable.EndsWith(" " + searchLower))
                {
                    sortable = sortable.Remove(sortable.Length - (searchLower.Length + 1));
                }
            }
            return sortable;
        }

        /// <summary>
        /// Gets or sets the parent.
        /// </summary>
        /// <value>The parent.</value>
        [IgnoreDataMember]
        public Folder Parent { get; set; }

        /// <summary>
        /// Gets the collection folder parent.
        /// </summary>
        /// <value>The collection folder parent.</value>
        [IgnoreDataMember]
        public Folder CollectionFolder
        {
            get
            {
                if (this is AggregateFolder)
                {
                    return null;
                }

                if (IsFolder)
                {
                    var iCollectionFolder = this as ICollectionFolder;

                    if (iCollectionFolder != null)
                    {
                        return (Folder)this;
                    }
                }

                var parent = Parent;

                while (parent != null)
                {
                    var iCollectionFolder = parent as ICollectionFolder;

                    if (iCollectionFolder != null)
                    {
                        return parent;
                    }

                    parent = parent.Parent;
                }

                return null;
            }
        }

        /// <summary>
        /// When the item first debuted. For movies this could be premiere date, episodes would be first aired
        /// </summary>
        /// <value>The premiere date.</value>
        public DateTime? PremiereDate { get; set; }

        /// <summary>
        /// Gets or sets the display type of the media.
        /// </summary>
        /// <value>The display type of the media.</value>
        public virtual string DisplayMediaType { get; set; }

        /// <summary>
        /// Gets or sets the backdrop image paths.
        /// </summary>
        /// <value>The backdrop image paths.</value>
        public List<string> BackdropImagePaths { get; set; }

        /// <summary>
        /// Gets or sets the screenshot image paths.
        /// </summary>
        /// <value>The screenshot image paths.</value>
        public List<string> ScreenshotImagePaths { get; set; }

        /// <summary>
        /// Gets or sets the official rating.
        /// </summary>
        /// <value>The official rating.</value>
        public virtual string OfficialRating { get; set; }

        /// <summary>
        /// Gets or sets the custom rating.
        /// </summary>
        /// <value>The custom rating.</value>
        public virtual string CustomRating { get; set; }

        /// <summary>
        /// Gets or sets the language.
        /// </summary>
        /// <value>The language.</value>
        public string Language { get; set; }
        /// <summary>
        /// Gets or sets the overview.
        /// </summary>
        /// <value>The overview.</value>
        public string Overview { get; set; }
        /// <summary>
        /// Gets or sets the taglines.
        /// </summary>
        /// <value>The taglines.</value>
        public List<string> Taglines { get; set; }

        /// <summary>
        /// Gets or sets the people.
        /// </summary>
        /// <value>The people.</value>
        public List<PersonInfo> People { get; set; }

        /// <summary>
        /// Override this if you need to combine/collapse person information
        /// </summary>
        /// <value>All people.</value>
        [IgnoreDataMember]
        public virtual IEnumerable<PersonInfo> AllPeople
        {
            get { return People; }
        }

        /// <summary>
        /// Gets or sets the studios.
        /// </summary>
        /// <value>The studios.</value>
        public virtual List<string> Studios { get; set; }

        /// <summary>
        /// Gets or sets the genres.
        /// </summary>
        /// <value>The genres.</value>
        public virtual List<string> Genres { get; set; }

        /// <summary>
        /// Gets or sets the community rating.
        /// </summary>
        /// <value>The community rating.</value>
        public float? CommunityRating { get; set; }
        /// <summary>
        /// Gets or sets the run time ticks.
        /// </summary>
        /// <value>The run time ticks.</value>
        public long? RunTimeTicks { get; set; }

        /// <summary>
        /// Gets or sets the aspect ratio.
        /// </summary>
        /// <value>The aspect ratio.</value>
        public string AspectRatio { get; set; }
        /// <summary>
        /// Gets or sets the production year.
        /// </summary>
        /// <value>The production year.</value>
        public virtual int? ProductionYear { get; set; }

        /// <summary>
        /// If the item is part of a series, this is it's number in the series.
        /// This could be episode number, album track number, etc.
        /// </summary>
        /// <value>The index number.</value>
        public int? IndexNumber { get; set; }

        /// <summary>
        /// For an episode this could be the season number, or for a song this could be the disc number.
        /// </summary>
        /// <value>The parent index number.</value>
        public int? ParentIndexNumber { get; set; }

        /// <summary>
        /// The _local trailers
        /// </summary>
        private List<Video> _localTrailers;
        /// <summary>
        /// The _local trailers initialized
        /// </summary>
        private bool _localTrailersInitialized;
        /// <summary>
        /// The _local trailers sync lock
        /// </summary>
        private object _localTrailersSyncLock = new object();
        /// <summary>
        /// Gets the local trailers.
        /// </summary>
        /// <value>The local trailers.</value>
        [IgnoreDataMember]
        public List<Video> LocalTrailers
        {
            get
            {
                LazyInitializer.EnsureInitialized(ref _localTrailers, ref _localTrailersInitialized, ref _localTrailersSyncLock, LoadLocalTrailers);
                return _localTrailers;
            }
            private set
            {
                _localTrailers = value;

                if (value == null)
                {
                    _localTrailersInitialized = false;
                }
            }
        }

        /// <summary>
        /// Loads local trailers from the file system
        /// </summary>
        /// <returns>List{Video}.</returns>
        private List<Video> LoadLocalTrailers()
        {
            ItemResolveArgs resolveArgs;

            try
            {
                resolveArgs = ResolveArgs;
            }
            catch (IOException ex)
            {
                Logger.ErrorException("Error getting ResolveArgs for {0}", ex, Path);
                return new List<Video> { };
            }

            if (!resolveArgs.IsDirectory)
            {
                return new List<Video> { };
            }

            var folder = resolveArgs.GetFileSystemEntryByName(TrailerFolderName);

            // Path doesn't exist. No biggie
            if (folder == null)
            {
                return new List<Video> { };
            }

            IEnumerable<WIN32_FIND_DATA> files;

            try
            {
                files = FileSystem.GetFiles(folder.Value.Path);
            }
            catch (IOException ex)
            {
                Logger.ErrorException("Error loading trailers for {0}", ex, Name);
                return new List<Video> { };
            }

            return LibraryManager.ResolvePaths<Video>(files, null).Select(video =>
            {
                // Try to retrieve it from the db. If we don't find it, use the resolved version
                var dbItem = Kernel.Instance.ItemRepository.RetrieveItem(video.Id) as Video;

                if (dbItem != null)
                {
                    dbItem.ResolveArgs = video.ResolveArgs;
                    video = dbItem;
                }

                return video;
            }).ToList();
        }

        /// <summary>
        /// Overrides the base implementation to refresh metadata for local trailers
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="forceSave">if set to <c>true</c> [is new item].</param>
        /// <param name="forceRefresh">if set to <c>true</c> [force].</param>
        /// <param name="allowSlowProviders">if set to <c>true</c> [allow slow providers].</param>
        /// <param name="resetResolveArgs">if set to <c>true</c> [reset resolve args].</param>
        /// <returns>true if a provider reports we changed</returns>
        public virtual async Task<bool> RefreshMetadata(CancellationToken cancellationToken, bool forceSave = false, bool forceRefresh = false, bool allowSlowProviders = true, bool resetResolveArgs = true)
        {
            if (resetResolveArgs)
            {
                ResolveArgs = null;
            }

            // Lazy load these again
            LocalTrailers = null;

            // Refresh for the item
            var itemRefreshTask = ProviderManager.ExecuteMetadataProviders(this, cancellationToken, forceRefresh, allowSlowProviders);

            cancellationToken.ThrowIfCancellationRequested();

            // Refresh metadata for local trailers
            var trailerTasks = LocalTrailers.Select(i => i.RefreshMetadata(cancellationToken, forceSave, forceRefresh, allowSlowProviders));

            cancellationToken.ThrowIfCancellationRequested();

            // Await the trailer tasks
            await Task.WhenAll(trailerTasks).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            // Get the result from the item task
            var changed = await itemRefreshTask.ConfigureAwait(false);

            if (changed || forceSave)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await Kernel.Instance.ItemRepository.SaveItem(this, cancellationToken).ConfigureAwait(false);
            }

            return changed;
        }

        /// <summary>
        /// Clear out all metadata properties. Extend for sub-classes.
        /// </summary>
        public virtual void ClearMetaValues()
        {
            Images = null;
            ForcedSortName = null;
            PremiereDate = null;
            BackdropImagePaths = null;
            OfficialRating = null;
            CustomRating = null;
            Overview = null;
            Taglines = null;
            Language = null;
            Studios = null;
            Genres = null;
            CommunityRating = null;
            RunTimeTicks = null;
            AspectRatio = null;
            ProductionYear = null;
            ProviderIds = null;
            DisplayMediaType = GetType().Name;
            ResolveArgs = null;
        }

        /// <summary>
        /// Gets or sets the trailer URL.
        /// </summary>
        /// <value>The trailer URL.</value>
        public List<string> TrailerUrls { get; set; }

        /// <summary>
        /// Gets or sets the provider ids.
        /// </summary>
        /// <value>The provider ids.</value>
        public Dictionary<string, string> ProviderIds { get; set; }

        /// <summary>
        /// Override this to false if class should be ignored for indexing purposes
        /// </summary>
        /// <value><c>true</c> if [include in index]; otherwise, <c>false</c>.</value>
        [IgnoreDataMember]
        public virtual bool IncludeInIndex
        {
            get { return true; }
        }

        /// <summary>
        /// Override this to true if class should be grouped under a container in indicies
        /// The container class should be defined via IndexContainer
        /// </summary>
        /// <value><c>true</c> if [group in index]; otherwise, <c>false</c>.</value>
        [IgnoreDataMember]
        public virtual bool GroupInIndex
        {
            get { return false; }
        }

        /// <summary>
        /// Override this to return the folder that should be used to construct a container
        /// for this item in an index.  GroupInIndex should be true as well.
        /// </summary>
        /// <value>The index container.</value>
        [IgnoreDataMember]
        public virtual Folder IndexContainer
        {
            get { return null; }
        }

        /// <summary>
        /// The _user data
        /// </summary>
        private IEnumerable<UserItemData> _userData;
        /// <summary>
        /// The _user data initialized
        /// </summary>
        private bool _userDataInitialized;
        /// <summary>
        /// The _user data sync lock
        /// </summary>
        private object _userDataSyncLock = new object();
        /// <summary>
        /// Gets the user data.
        /// </summary>
        /// <value>The user data.</value>
        [IgnoreDataMember]
        public IEnumerable<UserItemData> UserData
        {
            get
            {
                // Call ToList to exhaust the stream because we'll be iterating over this multiple times
                LazyInitializer.EnsureInitialized(ref _userData, ref _userDataInitialized, ref _userDataSyncLock, () => Kernel.Instance.UserDataRepository.RetrieveUserData(this).ToList());
                return _userData;
            }
            private set
            {
                _userData = value;

                if (value == null)
                {
                    _userDataInitialized = false;
                }
            }
        }

        /// <summary>
        /// Gets the user data.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="createIfNull">if set to <c>true</c> [create if null].</param>
        /// <returns>UserItemData.</returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public UserItemData GetUserData(User user, bool createIfNull)
        {
            if (user == null)
            {
                throw new ArgumentNullException();
            }

            if (UserData == null)
            {
                if (!createIfNull)
                {
                    return null;
                }

                AddOrUpdateUserData(user, new UserItemData { UserId = user.Id });
            }

            var data = UserData.FirstOrDefault(u => u.UserId == user.Id);

            if (data == null && createIfNull)
            {
                data = new UserItemData { UserId = user.Id };
                AddOrUpdateUserData(user, data);
            }

            return data;
        }

        /// <summary>
        /// Adds the or update user data.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="data">The data.</param>
        /// <exception cref="System.ArgumentNullException"></exception>
        public void AddOrUpdateUserData(User user, UserItemData data)
        {
            if (user == null)
            {
                throw new ArgumentNullException();
            }

            if (data == null)
            {
                throw new ArgumentNullException();
            }

            data.UserId = user.Id;

            if (UserData == null)
            {
                UserData = new[] { data };
            }
            else
            {
                var list = UserData.Where(u => u.UserId != user.Id).ToList();
                list.Add(data);
                UserData = list;
            }
        }

        /// <summary>
        /// The _user data id
        /// </summary>
        protected Guid _userDataId; //cache this so it doesn't have to be re-constructed on every reference
        /// <summary>
        /// Return the id that should be used to key user data for this item.
        /// Default is just this Id but subclasses can use provider Ids for transportability.
        /// </summary>
        /// <value>The user data id.</value>
        [IgnoreDataMember]
        public virtual Guid UserDataId
        {
            get
            {
                return _userDataId == Guid.Empty ? (_userDataId = Id) : _userDataId;
            }
        }

        /// <summary>
        /// Determines if a given user has access to this item
        /// </summary>
        /// <param name="user">The user.</param>
        /// <returns><c>true</c> if [is parental allowed] [the specified user]; otherwise, <c>false</c>.</returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public bool IsParentalAllowed(User user)
        {
            if (user == null)
            {
                throw new ArgumentNullException();
            }

            return user.Configuration.MaxParentalRating == null || Ratings.Level(CustomRating ?? OfficialRating) <= user.Configuration.MaxParentalRating;
        }

        /// <summary>
        /// Determines if this folder should be visible to a given user.
        /// Default is just parental allowed. Can be overridden for more functionality.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <returns><c>true</c> if the specified user is visible; otherwise, <c>false</c>.</returns>
        /// <exception cref="System.ArgumentNullException">user</exception>
        public virtual bool IsVisible(User user)
        {
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            return IsParentalAllowed(user);
        }

        /// <summary>
        /// Finds an item by ID, recursively
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="user">The user.</param>
        /// <returns>BaseItem.</returns>
        /// <exception cref="System.ArgumentNullException">id</exception>
        public virtual BaseItem FindItemById(Guid id, User user)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentNullException("id");
            }

            if (Id == id)
            {
                return this;
            }

            if (LocalTrailers != null)
            {
                return LocalTrailers.FirstOrDefault(i => i.Id == id);
            }

            return null;
        }

        /// <summary>
        /// Finds the particular item by searching through our parents and, if not found there, loading from repo
        /// </summary>
        /// <param name="id">The id.</param>
        /// <returns>BaseItem.</returns>
        /// <exception cref="System.ArgumentException"></exception>
        protected BaseItem FindParentItem(Guid id)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException();
            }

            var parent = Parent;
            while (parent != null && !parent.IsRoot)
            {
                if (parent.Id == id) return parent;
                parent = parent.Parent;
            }

            //not found - load from repo
            return Kernel.Instance.ItemRepository.RetrieveItem(id);
        }

        /// <summary>
        /// Gets a value indicating whether this instance is folder.
        /// </summary>
        /// <value><c>true</c> if this instance is folder; otherwise, <c>false</c>.</value>
        [IgnoreDataMember]
        public virtual bool IsFolder
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Determine if we have changed vs the passed in copy
        /// </summary>
        /// <param name="copy">The copy.</param>
        /// <returns><c>true</c> if the specified copy has changed; otherwise, <c>false</c>.</returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public virtual bool HasChanged(BaseItem copy)
        {
            if (copy == null)
            {
                throw new ArgumentNullException();
            }

            var changed = copy.DateModified != DateModified;
            if (changed)
            {
                Logger.Debug(Name + " changed - original creation: " + DateCreated + " new creation: " + copy.DateCreated + " original modified: " + DateModified + " new modified: " + copy.DateModified);
            }
            return changed;
        }

        /// <summary>
        /// Determines if the item is considered new based on user settings
        /// </summary>
        /// <param name="user">The user.</param>
        /// <returns><c>true</c> if [is recently added] [the specified user]; otherwise, <c>false</c>.</returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public bool IsRecentlyAdded(User user)
        {
            if (user == null)
            {
                throw new ArgumentNullException();
            }

            return (DateTime.UtcNow - DateCreated).TotalDays < ConfigurationManager.Configuration.RecentItemDays;
        }

        /// <summary>
        /// Adds people to the item
        /// </summary>
        /// <param name="people">The people.</param>
        /// <exception cref="System.ArgumentNullException"></exception>
        public void AddPeople(IEnumerable<PersonInfo> people)
        {
            if (people == null)
            {
                throw new ArgumentNullException();
            }

            foreach (var person in people)
            {
                AddPerson(person);
            }
        }

        /// <summary>
        /// Adds a person to the item
        /// </summary>
        /// <param name="person">The person.</param>
        /// <exception cref="System.ArgumentNullException"></exception>
        public void AddPerson(PersonInfo person)
        {
            if (person == null)
            {
                throw new ArgumentNullException();
            }

            if (string.IsNullOrWhiteSpace(person.Name))
            {
                throw new ArgumentNullException();
            }

            if (People == null)
            {
                People = new List<PersonInfo>();
            }

            // Check for dupes based on the combination of Name and Type

            if (!People.Any(p => p.Name.Equals(person.Name, StringComparison.OrdinalIgnoreCase) && p.Type.Equals(person.Type, StringComparison.OrdinalIgnoreCase)))
            {
                People.Add(person);
            }
        }

        /// <summary>
        /// Adds studios to the item
        /// </summary>
        /// <param name="studios">The studios.</param>
        /// <exception cref="System.ArgumentNullException"></exception>
        public void AddStudios(IEnumerable<string> studios)
        {
            if (studios == null)
            {
                throw new ArgumentNullException();
            }

            foreach (var name in studios)
            {
                AddStudio(name);
            }
        }

        /// <summary>
        /// Adds a studio to the item
        /// </summary>
        /// <param name="name">The name.</param>
        /// <exception cref="System.ArgumentNullException"></exception>
        public void AddStudio(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException();
            }

            if (Studios == null)
            {
                Studios = new List<string>();
            }

            if (!Studios.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                Studios.Add(name);
            }
        }

        /// <summary>
        /// Adds a tagline to the item
        /// </summary>
        /// <param name="name">The name.</param>
        /// <exception cref="System.ArgumentNullException"></exception>
        public void AddTagline(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException();
            }

            if (Taglines == null)
            {
                Taglines = new List<string>();
            }

            if (!Taglines.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                Taglines.Add(name);
            }
        }

        /// <summary>
        /// Adds a TrailerUrl to the item
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <exception cref="System.ArgumentNullException"></exception>
        public void AddTrailerUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentNullException();
            }

            if (TrailerUrls == null)
            {
                TrailerUrls = new List<string>();
            }

            if (!TrailerUrls.Contains(url, StringComparer.OrdinalIgnoreCase))
            {
                TrailerUrls.Add(url);
            }
        }

        /// <summary>
        /// Adds a genre to the item
        /// </summary>
        /// <param name="name">The name.</param>
        /// <exception cref="System.ArgumentNullException"></exception>
        public void AddGenre(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException();
            }

            if (Genres == null)
            {
                Genres = new List<string>();
            }

            if (!Genres.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                Genres.Add(name);
            }
        }

        /// <summary>
        /// Adds genres to the item
        /// </summary>
        /// <param name="genres">The genres.</param>
        /// <exception cref="System.ArgumentNullException"></exception>
        public void AddGenres(IEnumerable<string> genres)
        {
            if (genres == null)
            {
                throw new ArgumentNullException();
            }

            foreach (var name in genres)
            {
                AddGenre(name);
            }
        }

        /// <summary>
        /// Marks the item as either played or unplayed
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="wasPlayed">if set to <c>true</c> [was played].</param>
        /// <returns>Task.</returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public virtual Task SetPlayedStatus(User user, bool wasPlayed, IUserManager userManager)
        {
            if (user == null)
            {
                throw new ArgumentNullException();
            }

            var data = GetUserData(user, true);

            if (wasPlayed)
            {
                data.PlayCount = Math.Max(data.PlayCount, 1);

                if (!data.LastPlayedDate.HasValue)
                {
                    data.LastPlayedDate = DateTime.UtcNow;
                }
            }
            else
            {
                //I think it is okay to do this here.
                // if this is only called when a user is manually forcing something to un-played
                // then it probably is what we want to do...
                data.PlayCount = 0;
                data.PlaybackPositionTicks = 0;
                data.LastPlayedDate = null;
            }

            data.Played = wasPlayed;

            return userManager.SaveUserDataForItem(user, this, data);
        }

        /// <summary>
        /// Do whatever refreshing is necessary when the filesystem pertaining to this item has changed.
        /// </summary>
        /// <returns>Task.</returns>
        public virtual Task ChangedExternally()
        {
            return RefreshMetadata(CancellationToken.None);
        }

        /// <summary>
        /// Finds a parent of a given type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>``0.</returns>
        public T FindParent<T>()
            where T : Folder
        {
            var parent = Parent;

            while (parent != null)
            {
                var result = parent as T;
                if (result != null)
                {
                    return result;
                }

                parent = parent.Parent;
            }

            return null;
        }

        /// <summary>
        /// Gets an image
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentException">Backdrops should be accessed using Item.Backdrops</exception>
        public string GetImage(ImageType type)
        {
            if (type == ImageType.Backdrop)
            {
                throw new ArgumentException("Backdrops should be accessed using Item.Backdrops");
            }
            if (type == ImageType.Screenshot)
            {
                throw new ArgumentException("Screenshots should be accessed using Item.Screenshots");
            }

            if (Images == null)
            {
                return null;
            }

            string val;
            Images.TryGetValue(type.ToString(), out val);
            return val;
        }

        /// <summary>
        /// Gets an image
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns><c>true</c> if the specified type has image; otherwise, <c>false</c>.</returns>
        /// <exception cref="System.ArgumentException">Backdrops should be accessed using Item.Backdrops</exception>
        public bool HasImage(ImageType type)
        {
            if (type == ImageType.Backdrop)
            {
                throw new ArgumentException("Backdrops should be accessed using Item.Backdrops");
            }
            if (type == ImageType.Screenshot)
            {
                throw new ArgumentException("Screenshots should be accessed using Item.Screenshots");
            }

            return !string.IsNullOrEmpty(GetImage(type));
        }

        /// <summary>
        /// Sets an image
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="path">The path.</param>
        /// <exception cref="System.ArgumentException">Backdrops should be accessed using Item.Backdrops</exception>
        public void SetImage(ImageType type, string path)
        {
            if (type == ImageType.Backdrop)
            {
                throw new ArgumentException("Backdrops should be accessed using Item.Backdrops");
            }
            if (type == ImageType.Screenshot)
            {
                throw new ArgumentException("Screenshots should be accessed using Item.Screenshots");
            }

            var typeKey = type.ToString();

            // If it's null remove the key from the dictionary
            if (string.IsNullOrEmpty(path))
            {
                if (Images != null)
                {
                    if (Images.ContainsKey(typeKey))
                    {
                        Images.Remove(typeKey);
                    }
                }
            }
            else
            {
                // Ensure it exists
                if (Images == null)
                {
                    Images = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                Images[typeKey] = path;
            }
        }

        /// <summary>
        /// Deletes the image.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>Task.</returns>
        public async Task DeleteImage(ImageType type)
        {
            if (!HasImage(type))
            {
                return;
            }

            // Delete the source file
            File.Delete(GetImage(type));

            // Remove it from the item
            SetImage(type, null);

            // Refresh metadata
            await RefreshMetadata(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
