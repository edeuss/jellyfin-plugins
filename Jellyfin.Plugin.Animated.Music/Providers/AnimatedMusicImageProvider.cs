using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.Animated.Music.Configuration;

namespace Jellyfin.Plugin.Animated.Music.Providers
{
    /// <summary>
    /// Image provider for animated music files.
    /// </summary>
    public class AnimatedMusicImageProvider : ILocalImageProvider, IHasOrder
    {
        private readonly ILogger<AnimatedMusicImageProvider> _logger;
        private readonly IFileSystem _fileSystem;
        private readonly IServerConfigurationManager _serverConfigurationManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="AnimatedMusicImageProvider"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="fileSystem">The file system.</param>
        /// <param name="serverConfigurationManager">The server configuration manager.</param>
        public AnimatedMusicImageProvider(ILogger<AnimatedMusicImageProvider> logger, IFileSystem fileSystem, IServerConfigurationManager serverConfigurationManager)
        {
            _logger = logger;
            _fileSystem = fileSystem;
            _serverConfigurationManager = serverConfigurationManager;
        }

        private PluginConfiguration Configuration => _serverConfigurationManager.GetConfiguration("animatedmusic") as PluginConfiguration ?? new PluginConfiguration();

        /// <inheritdoc />
        public string Name => "Animated Music Image Provider";

        /// <inheritdoc />
        public int Order => 0;

        /// <inheritdoc />
        public bool Supports(BaseItem item)
        {
            return item is MusicAlbum;
        }

        /// <inheritdoc />
        public IEnumerable<LocalImageInfo> GetImages(BaseItem item, IDirectoryService directoryService)
        {
            var images = new List<LocalImageInfo>();

            try
            {
                if (item is not MusicAlbum album)
                {
                    return images;
                }

                var albumPath = album.ContainingFolderPath;
                if (string.IsNullOrEmpty(albumPath) || !Directory.Exists(albumPath))
                {
                    return images;
                }

                var maxFileSizeBytes = Configuration.MaxFileSizeMB * 1024 * 1024;

                // Look for animated cover
                if (Configuration.EnableAnimatedCovers)
                {
                    var animatedCoverPath = FindAnimatedFile(albumPath, Configuration.AnimatedCoverFileName, maxFileSizeBytes);
                    if (!string.IsNullOrEmpty(animatedCoverPath))
                    {
                        var fileInfo = _fileSystem.GetFileInfo(animatedCoverPath);
                        if (fileInfo.Exists)
                        {
                            images.Add(new LocalImageInfo
                            {
                                FileInfo = fileInfo,
                                Type = ImageType.Primary
                            });
                            _logger.LogDebug("Added animated cover image for album: {AlbumPath}", albumPath);
                        }
                    }
                }

                // Look for vertical background
                if (Configuration.EnableVerticalBackgrounds)
                {
                    var verticalBackgroundPath = FindAnimatedFile(albumPath, Configuration.VerticalBackgroundFileName, maxFileSizeBytes);
                    if (!string.IsNullOrEmpty(verticalBackgroundPath))
                    {
                        var fileInfo = _fileSystem.GetFileInfo(verticalBackgroundPath);
                        if (fileInfo.Exists)
                        {
                            images.Add(new LocalImageInfo
                            {
                                FileInfo = fileInfo,
                                Type = ImageType.Backdrop
                            });
                            _logger.LogDebug("Added vertical background image for album: {AlbumPath}", albumPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting animated images for album: {AlbumPath}", item.ContainingFolderPath);
            }

            return images;
        }

        private string FindAnimatedFile(string albumPath, string fileNamePattern, long maxFileSizeBytes)
        {
            if (string.IsNullOrEmpty(albumPath) || !Directory.Exists(albumPath))
            {
                return null;
            }

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

        /// <inheritdoc />
        public Task<LocalImageInfo> GetImage(BaseItem item, string fileName, CancellationToken cancellationToken)
        {
            return Task.FromResult<LocalImageInfo>(null);
        }

        /// <inheritdoc />
        public ImageType GetImageType(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return ImageType.Primary;
            }

            var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

            // Check if it's an animated cover
            if (nameWithoutExtension.Equals(Configuration.AnimatedCoverFileName, StringComparison.OrdinalIgnoreCase))
            {
                return ImageType.Primary;
            }

            // Check if it's a vertical background
            if (nameWithoutExtension.Equals(Configuration.VerticalBackgroundFileName, StringComparison.OrdinalIgnoreCase))
            {
                // Use a custom image type for vertical backgrounds
                return ImageType.Backdrop;
            }

            // Default to primary image type
            return ImageType.Primary;
        }
    }
} 