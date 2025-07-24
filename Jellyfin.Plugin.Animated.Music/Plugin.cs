using System;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Jellyfin.Plugin.Animated.Music.Providers;
using Jellyfin.Plugin.Animated.Music.Tasks;

namespace Jellyfin.Plugin.Animated.Music
{
    /// <summary>
    /// Main plugin class for Jellyfin.Plugin.Animated.Music.
    /// </summary>
    public class Plugin : BasePlugin<PluginConfiguration>, IHasPluginConfiguration
    {
        /// <summary>
        /// Gets the plugin instance.
        /// </summary>
        public static Plugin Instance { get; private set; } = null!;

        /// <summary>
        /// Initializes a new instance of the <see cref="Plugin"/> class.
        /// </summary>
        /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
        /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
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
        public void RegisterServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<IScheduledTask, AnimatedMusicScanTask>();
            serviceCollection.AddTransient<IMetadataProvider<MusicAlbum>, AnimatedAlbumMetadataProvider>();
            serviceCollection.AddTransient<IMetadataProvider<Audio>, AnimatedMusicMetadataProvider>();
        }
    }

    /// <summary>
    /// Plugin configuration.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        // Empty configuration for now
    }
}