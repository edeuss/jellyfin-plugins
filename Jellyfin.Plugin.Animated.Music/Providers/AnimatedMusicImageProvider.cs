using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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

        /// <summary>
        /// Initializes a new instance of the <see cref="AnimatedMusicImageProvider"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="fileSystem">The file system.</param>
        public AnimatedMusicImageProvider(ILogger<AnimatedMusicImageProvider> logger, IFileSystem fileSystem)
        {
            _logger = logger;
            _fileSystem = fileSystem;
        }

        private PluginConfiguration GetConfiguration()
        {
            try
            {
                return Plugin.Instance?.Configuration ?? new PluginConfiguration();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get plugin configuration, using defaults");
                return new PluginConfiguration();
            }
        }

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
                    _logger.LogDebug("Album path is empty or doesn't exist for album: {AlbumName}", album.Name);
                    return images;
                }

                var configuration = GetConfiguration();
                var maxFileSizeBytes = configuration.MaxFileSizeMB * 1024 * 1024;

                // Look for animated cover
                if (configuration.EnableAnimatedCovers)
                {
                    try
                    {
                        var animatedCoverPath = FindAnimatedFile(albumPath, configuration.AnimatedCoverFileName, maxFileSizeBytes, configuration.SupportedAnimatedFormats);
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
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "No animated cover found for album: {AlbumPath}", albumPath);
                    }
                }

                // Look for vertical background
                if (configuration.EnableVerticalBackgrounds)
                {
                    try
                    {
                        var verticalBackgroundPath = FindAnimatedFile(albumPath, configuration.VerticalBackgroundFileName, maxFileSizeBytes, configuration.SupportedAnimatedFormats);
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
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "No vertical background found for album: {AlbumPath}", albumPath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "No animated images found for album: {AlbumPath}", item.ContainingFolderPath);
            }

            return images;
        }

        private string FindAnimatedFile(string albumPath, string fileNamePattern, long maxFileSizeBytes, string[] supportedFormats)
        {
            if (string.IsNullOrEmpty(albumPath) || !Directory.Exists(albumPath))
            {
                return null;
            }

            try
            {
                foreach (var file in Directory.GetFiles(albumPath))
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileInfo.Name);
                        
                        if (nameWithoutExtension.Equals(fileNamePattern, StringComparison.OrdinalIgnoreCase) &&
                            IsAnimatedFile(fileInfo.Name, supportedFormats) &&
                            fileInfo.Length <= maxFileSizeBytes)
                        {
                            return file;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error checking file: {FilePath}", file);
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error scanning directory: {AlbumPath}", albumPath);
            }

            return null;
        }

        private bool IsAnimatedFile(string fileName, string[] supportedFormats)
        {
            if (string.IsNullOrEmpty(fileName) || supportedFormats == null || supportedFormats.Length == 0)
            {
                return false;
            }

            try
            {
                var extension = Path.GetExtension(fileName).ToLowerInvariant();
                return Array.Exists(supportedFormats, f => f.Equals(extension, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error checking file extension for: {FileName}", fileName);
                return false;
            }
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

            try
            {
                var configuration = GetConfiguration();
                var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

                // Check if it's an animated cover
                if (nameWithoutExtension.Equals(configuration.AnimatedCoverFileName, StringComparison.OrdinalIgnoreCase))
                {
                    return ImageType.Primary;
                }

                // Check if it's a vertical background
                if (nameWithoutExtension.Equals(configuration.VerticalBackgroundFileName, StringComparison.OrdinalIgnoreCase))
                {
                    return ImageType.Backdrop;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error determining image type for: {FileName}", fileName);
            }

            // Default to primary image type
            return ImageType.Primary;
        }
    }
} 