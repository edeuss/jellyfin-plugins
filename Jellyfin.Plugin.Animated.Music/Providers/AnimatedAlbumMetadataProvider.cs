using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Animated.Music.Providers
{
    /// <summary>
    /// Metadata provider for adding animated content information to music albums.
    /// </summary>
    public class AnimatedAlbumMetadataProvider : IMetadataProvider<MusicAlbum>, IHasOrder
    {
        private readonly ILogger<AnimatedAlbumMetadataProvider> _logger;

        // Hardcoded configuration values
        private static readonly string[] SupportedAnimatedFormats = { ".gif", ".mp4", ".webm", ".mov", ".avi" };

        // Provider-specific metadata keys
        private const string HasAnimatedCoverKey = "AnimatedMusic.HasAnimatedCover";
        private const string HasVerticalBackgroundKey = "AnimatedMusic.HasVerticalBackground";

        /// <summary>
        /// Initializes a new instance of the <see cref="AnimatedAlbumMetadataProvider"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public AnimatedAlbumMetadataProvider(ILogger<AnimatedAlbumMetadataProvider> logger)
        {
            _logger = logger;
            _logger.LogInformation("AnimatedAlbumMetadataProvider constructor called - provider initialized");
        }

        /// <inheritdoc />
        public string Name => "Animated Album Metadata Provider";

        /// <inheritdoc />
        public int Order => 100;

        /// <inheritdoc />
        public bool Supports(BaseItem item)
        {
            var supports = item is MusicAlbum;
            _logger.LogInformation("AnimatedAlbumMetadataProvider.Supports called for item {ItemName} (ID: {ItemId}, type: {ItemType}): {Supports}",
                item.Name, item.Id, item.GetType().Name, supports);
            return supports;
        }

        /// <inheritdoc />
        public Task<ItemUpdateType> FetchAsync(MusicAlbum item, MetadataRefreshOptions options, CancellationToken cancellationToken)
        {
            _logger.LogInformation("AnimatedAlbumMetadataProvider.FetchAsync called for album {AlbumName} (ID: {AlbumId})", item.Name, item.Id);
            try
            {
                var updateType = ItemUpdateType.None;
                bool hasChanges = false;

                var albumPath = item.ContainingFolderPath;
                if (string.IsNullOrEmpty(albumPath))
                {
                    _logger.LogWarning("Album {AlbumName} has no containing folder path", item.Name);
                    return Task.FromResult(updateType);
                }

                // Check for animated cover existence
                var hasAnimatedCover = HasAnimatedFile(albumPath, "cover-animated");
                var currentHasAnimatedCover = bool.TryParse(item.GetProviderId(HasAnimatedCoverKey), out var currentCover) && currentCover;

                if (hasAnimatedCover != currentHasAnimatedCover)
                {
                    item.SetProviderId(HasAnimatedCoverKey, hasAnimatedCover.ToString());
                    hasChanges = true;
                    _logger.LogDebug("Updated animated cover flag for album {AlbumName}: {HasCover}", item.Name, hasAnimatedCover);
                }

                // Check for vertical background existence
                var hasVerticalBackground = HasAnimatedFile(albumPath, "vertical-background");
                var currentHasVerticalBackground = bool.TryParse(item.GetProviderId(HasVerticalBackgroundKey), out var currentBackground) && currentBackground;

                if (hasVerticalBackground != currentHasVerticalBackground)
                {
                    item.SetProviderId(HasVerticalBackgroundKey, hasVerticalBackground.ToString());
                    hasChanges = true;
                    _logger.LogDebug("Updated vertical background flag for album {AlbumName}: {HasBackground}", item.Name, hasVerticalBackground);
                }

                // Only return MetadataDownload if we actually made changes
                if (hasChanges)
                {
                    updateType = ItemUpdateType.MetadataDownload;
                    _logger.LogInformation("Updated animated metadata flags for album {AlbumName} - HasCover: {HasCover}, HasBackground: {HasBackground}",
                        item.Name, hasAnimatedCover, hasVerticalBackground);
                }
                else
                {
                    _logger.LogDebug("No changes needed for album {AlbumName}", item.Name);
                }

                return Task.FromResult(updateType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching animated metadata for album {AlbumId}: {AlbumName}", item.Id, item.Name);
                return Task.FromResult(ItemUpdateType.None);
            }
        }

        /// <summary>
        /// Checks if an animated file with the specified pattern exists in the given directory.
        /// </summary>
        /// <param name="directoryPath">Directory to search in.</param>
        /// <param name="fileNamePattern">Pattern to match (without extension).</param>
        /// <returns>True if the file exists; otherwise, false.</returns>
        private bool HasAnimatedFile(string directoryPath, string fileNamePattern)
        {
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            {
                return false;
            }

            try
            {
                foreach (var file in Directory.GetFiles(directoryPath))
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileInfo.Name);

                        if (nameWithoutExtension.Equals(fileNamePattern, StringComparison.OrdinalIgnoreCase) &&
                            IsAnimatedFile(fileInfo.Name))
                        {
                            return true;
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
                _logger.LogDebug(ex, "Error scanning directory: {DirectoryPath}", directoryPath);
            }

            return false;
        }

        /// <summary>
        /// Checks if a file is an animated file based on its extension.
        /// </summary>
        /// <param name="fileName">The file name to check.</param>
        /// <returns>True if the file is an animated file; otherwise, false.</returns>
        private bool IsAnimatedFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            try
            {
                var extension = Path.GetExtension(fileName).ToLowerInvariant();
                return Array.Exists(SupportedAnimatedFormats, f => f.Equals(extension, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error checking file extension for: {FileName}", fileName);
                return false;
            }
        }
    }
}