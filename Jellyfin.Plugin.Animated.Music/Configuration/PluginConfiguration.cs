using System;
using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Animated.Music.Configuration
{
    /// <summary>
    /// Plugin configuration for animated music features.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
        /// </summary>
        public PluginConfiguration()
        {
            // Default configuration values
            EnableAnimatedCovers = true;
            EnableVerticalBackgrounds = true;
            SupportedAnimatedFormats = new[] { ".gif", ".mp4", ".webm", ".mov", ".avi" };
            AnimatedCoverFileName = "cover-animated";
            VerticalBackgroundFileName = "vertical-background";
            MaxFileSizeMB = 50;
        }

        /// <summary>
        /// Gets or sets a value indicating whether animated covers are enabled.
        /// </summary>
        public bool EnableAnimatedCovers { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether vertical backgrounds are enabled.
        /// </summary>
        public bool EnableVerticalBackgrounds { get; set; }

        /// <summary>
        /// Gets or sets the supported animated file formats.
        /// </summary>
        public string[] SupportedAnimatedFormats { get; set; }

        /// <summary>
        /// Gets or sets the filename pattern for animated covers.
        /// </summary>
        public string AnimatedCoverFileName { get; set; }

        /// <summary>
        /// Gets or sets the filename pattern for vertical backgrounds.
        /// </summary>
        public string VerticalBackgroundFileName { get; set; }

        /// <summary>
        /// Gets or sets the maximum file size in MB for animated files.
        /// </summary>
        public int MaxFileSizeMB { get; set; }
    }
} 