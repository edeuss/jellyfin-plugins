using System;
using System.Collections.Generic;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Animated.Music
{
    /// <summary>
    /// Main plugin class for Jellyfin.Plugin.Animated.Music.
    /// </summary>
    public class Plugin : BasePlugin
    {
        /// <summary>
        /// Gets the plugin instance.
        /// </summary>
        public static Plugin Instance { get; private set; } = null!;

        /// <summary>
        /// Initializes a new instance of the <see cref="Plugin"/> class.
        /// </summary>
        /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
        public Plugin(IApplicationPaths applicationPaths)
            : base()
        {
            Instance = this;
        }

        /// <inheritdoc />
        public override string Name => "Animated Music";

        /// <inheritdoc />
        public override Guid Id => Guid.Parse("d5861930-8da6-499c-b7dd-235c60703f64");

        /// <inheritdoc />
        public override string Description => "Adds animated cover and vertical video background support for music albums";
    }
} 