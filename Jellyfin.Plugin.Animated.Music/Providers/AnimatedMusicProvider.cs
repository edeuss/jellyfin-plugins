using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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

        /// <summary>
        /// Initializes a new instance of the <see cref="AnimatedMusicProvider"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public AnimatedMusicProvider(ILogger<AnimatedMusicProvider> logger)
        {
            _logger = logger;
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
        public string Name => "Animated Music Provider";

        /// <inheritdoc />
        public int Order => 0;

        /// <inheritdoc />
        public Task<MetadataResult<MusicAlbum>> GetMetadata(ItemInfo info, IDirectoryService directoryService, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<MusicAlbum>();

            try
            {
                var configuration = GetConfiguration();
                
                if (!configuration.EnableAnimatedCovers && !configuration.EnableVerticalBackgrounds)
                {
                    return Task.FromResult(result);
                }

                var albumPath = info.ContainingFolderPath;
                if (string.IsNullOrEmpty(albumPath) || !Directory.Exists(albumPath))
                {
                    _logger.LogDebug("Album path is empty or doesn't exist for album: {AlbumPath}", albumPath);
                    return Task.FromResult(result);
                }

                var hasAnimatedCover = false;
                var hasVerticalBackground = false;

                if (configuration.EnableAnimatedCovers)
                {
                    try
                    {
                        hasAnimatedCover = FindAnimatedFile(albumPath, configuration.AnimatedCoverFileName, configuration.MaxFileSizeMB, configuration.SupportedAnimatedFormats) != null;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "No animated cover found for album: {AlbumPath}", albumPath);
                    }
                }

                if (configuration.EnableVerticalBackgrounds)
                {
                    try
                    {
                        hasVerticalBackground = FindAnimatedFile(albumPath, configuration.VerticalBackgroundFileName, configuration.MaxFileSizeMB, configuration.SupportedAnimatedFormats) != null;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "No vertical background found for album: {AlbumPath}", albumPath);
                    }
                }

                if (hasAnimatedCover || hasVerticalBackground)
                {
                    _logger.LogDebug("Found animated files for album at {AlbumPath}: AnimatedCover={HasAnimatedCover}, VerticalBackground={HasVerticalBackground}", 
                        albumPath, hasAnimatedCover, hasVerticalBackground);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "No animated files found for album: {AlbumPath}", info.ContainingFolderPath);
            }

            return Task.FromResult(result);
        }

        private string FindAnimatedFile(string albumPath, string fileNamePattern, int maxFileSizeMB, string[] supportedFormats)
        {
            if (string.IsNullOrEmpty(albumPath) || !Directory.Exists(albumPath))
            {
                return null;
            }

            try
            {
                var maxFileSizeBytes = maxFileSizeMB * 1024 * 1024;

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
    }
} 