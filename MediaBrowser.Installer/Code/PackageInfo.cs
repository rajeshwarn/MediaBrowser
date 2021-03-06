﻿using System;
using System.Collections.Generic;

namespace MediaBrowser.Installer.Code
{
    /// <summary>
    /// Class PackageInfo
    /// </summary>
    public class PackageInfo
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public string name { get; set; }

        /// <summary>
        /// Gets or sets the short description.
        /// </summary>
        /// <value>The short description.</value>
        public string shortDescription { get; set; }

        /// <summary>
        /// Gets or sets the overview.
        /// </summary>
        /// <value>The overview.</value>
        public string overview { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is premium.
        /// </summary>
        /// <value><c>true</c> if this instance is premium; otherwise, <c>false</c>.</value>
        public bool isPremium { get; set; }

        /// <summary>
        /// Gets or sets the rich desc URL.
        /// </summary>
        /// <value>The rich desc URL.</value>
        public string richDescUrl { get; set; }

        /// <summary>
        /// Gets or sets the thumb image.
        /// </summary>
        /// <value>The thumb image.</value>
        public string thumbImage { get; set; }

        /// <summary>
        /// Gets or sets the preview image.
        /// </summary>
        /// <value>The preview image.</value>
        public string previewImage { get; set; }

        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        /// <value>The type.</value>
        public PackageType type { get; set; }

        /// <summary>
        /// Gets or sets the target filename.
        /// </summary>
        /// <value>The target filename.</value>
        public string targetFilename { get; set; }

        /// <summary>
        /// Gets or sets the owner.
        /// </summary>
        /// <value>The owner.</value>
        public string owner { get; set; }

        /// <summary>
        /// Gets or sets the category.
        /// </summary>
        /// <value>The category.</value>
        public string category { get; set; }

        /// <summary>
        /// Gets or sets the catalog tile color.
        /// </summary>
        /// <value>The owner.</value>
        public string tileColor { get; set; }

        /// <summary>
        /// Gets or sets the feature id of this package (if premium).
        /// </summary>
        /// <value>The feature id.</value>
        public string featureId { get; set; }

        /// <summary>
        /// Gets or sets the registration info for this package (if premium).
        /// </summary>
        /// <value>The registration info.</value>
        public string regInfo { get; set; }

        /// <summary>
        /// Gets or sets the price for this package (if premium).
        /// </summary>
        /// <value>The price.</value>
        public float price { get; set; }

        /// <summary>
        /// Gets or sets whether or not this package is registered.
        /// </summary>
        /// <value>True if registered.</value>
        public bool isRegistered { get; set; }

        /// <summary>
        /// Gets or sets the expiration date for this package.
        /// </summary>
        /// <value>Expiration Date.</value>
        public DateTime expDate { get; set; }

        /// <summary>
        /// Gets or sets the versions.
        /// </summary>
        /// <value>The versions.</value>
        public List<PackageVersionInfo> versions { get; set; }
    }
}
