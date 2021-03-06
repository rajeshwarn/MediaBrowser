﻿using MediaBrowser.Controller.IO;
using MediaBrowser.Controller.Library;
using System;
using System.IO;
using System.Linq;
using MediaBrowser.Controller.Resolvers;

namespace MediaBrowser.Server.Implementations.Library.Resolvers.Audio
{
    /// <summary>
    /// Class AudioResolver
    /// </summary>
    public class AudioResolver : ItemResolver<Controller.Entities.Audio.Audio>
    {
        /// <summary>
        /// Gets the priority.
        /// </summary>
        /// <value>The priority.</value>
        public override ResolverPriority Priority
        {
            get { return ResolverPriority.Last; }
        }

        /// <summary>
        /// Resolves the specified args.
        /// </summary>
        /// <param name="args">The args.</param>
        /// <returns>Entities.Audio.Audio.</returns>
        protected override Controller.Entities.Audio.Audio Resolve(ItemResolveArgs args)
        {
            // Return audio if the path is a file and has a matching extension

            if (!args.IsDirectory)
            {
                if (IsAudioFile(args))
                {
                    return new Controller.Entities.Audio.Audio();
                }
            }

            return null;
        }

        /// <summary>
        /// The audio file extensions
        /// </summary>
        public static readonly string[] AudioFileExtensions = new[] { 
            ".mp3",
            ".flac",
            ".wma",
            ".aac",
            ".acc",
            ".m4a",
            ".m4b",
            ".wav",
            ".ape",
            ".ogg",
            ".oga"
            };

        /// <summary>
        /// Determines whether [is audio file] [the specified args].
        /// </summary>
        /// <param name="args">The args.</param>
        /// <returns><c>true</c> if [is audio file] [the specified args]; otherwise, <c>false</c>.</returns>
        public static bool IsAudioFile(ItemResolveArgs args)
        {
            return AudioFileExtensions.Contains(Path.GetExtension(args.Path), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether [is audio file] [the specified file].
        /// </summary>
        /// <param name="file">The file.</param>
        /// <returns><c>true</c> if [is audio file] [the specified file]; otherwise, <c>false</c>.</returns>
        public static bool IsAudioFile(WIN32_FIND_DATA file)
        {
            return AudioFileExtensions.Contains(Path.GetExtension(file.Path), StringComparer.OrdinalIgnoreCase);
        }
    }
}
