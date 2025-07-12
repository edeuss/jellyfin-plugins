using System;
using System.Collections.Generic;
using MediaBrowser.Common.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.Animated.Music.Configuration;

namespace Jellyfin.Plugin.Animated.Music
{
    /// <summary>
    /// Main plugin class for Jellyfin.Plugin.Animated.Music.
    /// </summary>
    public class Plugin : BasePlugin
    {
        public static Plugin Instance { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Plugin"/> class.
        /// </summary>
        public Plugin()
        {
            Instance = this;
        }

        /// <inheritdoc />
        public override string Name => "Animated Music";

        /// <inheritdoc />
        public override Guid Id => Guid.Parse("d5861930-8da6-499c-b7dd-235c60703f64");

        /// <inheritdoc />
        public override string Description => "Adds animated cover and vertical video background support for music albums";

        /// <inheritdoc />
        public new string Version => "1.2.8";
    }
} 