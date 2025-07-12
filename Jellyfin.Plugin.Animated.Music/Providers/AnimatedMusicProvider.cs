using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.Animated.Music.Configuration;

namespace Jellyfin.Plugin.Animated.Music.Providers
{
    /// <summary>
    /// Provider for animated music metadata.
    /// </summary>
    public class AnimatedMusicProvider : ILocalMetadataProvider<MusicAlbum>, IHasOrder
    {
        private readonly ILogger<AnimatedMusicProvider> _logger;
        private readonly IServerConfigurationManager _serverConfigurationManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="AnimatedMusicProvider"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="serverConfigurationManager">The server configuration manager.</param>
        public AnimatedMusicProvider(ILogger<AnimatedMusicProvider> logger, IServerConfigurationManager serverConfigurationManager)
        {
            _logger = logger;
            _serverConfigurationManager = serverConfigurationManager;
        }

        private PluginConfiguration Configuration => _serverConfigurationManager.GetConfiguration("animatedmusic") as PluginConfiguration ?? new PluginConfiguration();

        /// <inheritdoc />
        public string Name => "Animated Music Provider";

        /// <inheritdoc />
        public int Order => 0;

        /// <inheritdoc />
        public Task<MetadataResult<MusicAlbum>> GetMetadata(ItemInfo info, IDirectoryService directoryService, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<MusicAlbum>();

            try
            {
                if (!Configuration.EnableAnimatedCovers && !Configuration.EnableVerticalBackgrounds)
                {
                    return Task.FromResult(result);
                }

                var albumPath = info.ContainingFolderPath;
                if (string.IsNullOrEmpty(albumPath) || !Directory.Exists(albumPath))
                {
                    return Task.FromResult(result);
                }

                var hasAnimatedCover = false;
                var hasVerticalBackground = false;

                if (Configuration.EnableAnimatedCovers)
                {
                    hasAnimatedCover = FindAnimatedFile(albumPath, Configuration.AnimatedCoverFileName) != null;
                }

                if (Configuration.EnableVerticalBackgrounds)
                {
                    hasVerticalBackground = FindAnimatedFile(albumPath, Configuration.VerticalBackgroundFileName) != null;
                }

                if (hasAnimatedCover || hasVerticalBackground)
                {
                    _logger.LogDebug("Found animated files for album at {AlbumPath}: AnimatedCover={HasAnimatedCover}, VerticalBackground={HasVerticalBackground}", 
                        albumPath, hasAnimatedCover, hasVerticalBackground);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing animated music metadata for {AlbumPath}", info.ContainingFolderPath);
            }

            return Task.FromResult(result);
        }

        private string FindAnimatedFile(string albumPath, string fileNamePattern)
        {
            if (string.IsNullOrEmpty(albumPath) || !Directory.Exists(albumPath))
            {
                return null;
            }

            var maxFileSizeBytes = Configuration.MaxFileSizeMB * 1024 * 1024;

            foreach (var file in Directory.GetFiles(albumPath))
            {
                var fileInfo = new FileInfo(file);
                var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileInfo.Name);
                
                if (nameWithoutExtension.Equals(fileNamePattern, StringComparison.OrdinalIgnoreCase) &&
                    IsAnimatedFile(fileInfo.Name) &&
                    fileInfo.Length <= maxFileSizeBytes)
                {
                    return file;
                }
            }

            return null;
        }

        private bool IsAnimatedFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return Array.Exists(Configuration.SupportedAnimatedFormats, f => f.Equals(extension, StringComparison.OrdinalIgnoreCase));
        }
    }
} 