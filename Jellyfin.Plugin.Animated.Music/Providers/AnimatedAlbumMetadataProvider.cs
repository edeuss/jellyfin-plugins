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

                // Find album-level animated cover
                var animatedCoverPath = FindAnimatedFile(albumPath, "cover-animated");
                var currentAnimatedCover = item.GetProviderId("AnimatedCover");

                if (!string.IsNullOrEmpty(animatedCoverPath))
                {
                    if (currentAnimatedCover != animatedCoverPath)
                    {
                        item.SetProviderId("AnimatedCover", animatedCoverPath);
                        hasChanges = true;
                        _logger.LogDebug("Updated animated cover for album {AlbumName}: {CoverPath}", item.Name, animatedCoverPath);
                    }
                }
                else if (!string.IsNullOrEmpty(currentAnimatedCover))
                {
                    // Remove outdated animated cover reference
                    item.SetProviderId("AnimatedCover", string.Empty);
                    hasChanges = true;
                    _logger.LogDebug("Removed outdated animated cover for album {AlbumName}", item.Name);
                }

                // Find album-level vertical background
                var verticalBackgroundPath = FindAnimatedFile(albumPath, "vertical-background");
                var currentVerticalBackground = item.GetProviderId("VerticalBackground");

                if (!string.IsNullOrEmpty(verticalBackgroundPath))
                {
                    if (currentVerticalBackground != verticalBackgroundPath)
                    {
                        item.SetProviderId("VerticalBackground", verticalBackgroundPath);
                        hasChanges = true;
                        _logger.LogDebug("Updated vertical background for album {AlbumName}: {BackgroundPath}", item.Name, verticalBackgroundPath);
                    }
                }
                else if (!string.IsNullOrEmpty(currentVerticalBackground))
                {
                    // Remove outdated vertical background reference
                    item.SetProviderId("VerticalBackground", string.Empty);
                    hasChanges = true;
                    _logger.LogDebug("Removed outdated vertical background for album {AlbumName}", item.Name);
                }

                // Only return MetadataEdit if we actually made changes
                if (hasChanges)
                {
                    updateType = ItemUpdateType.MetadataEdit;
                    _logger.LogInformation("Updated animated metadata for album {AlbumName} - changes detected. AnimatedCover: {HasCover}, VerticalBackground: {HasBackground}",
                        item.Name, !string.IsNullOrEmpty(animatedCoverPath), !string.IsNullOrEmpty(verticalBackgroundPath));
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
        /// Finds an animated file with the specified pattern in the given directory.
        /// </summary>
        /// <param name="directoryPath">Directory to search in.</param>
        /// <param name="fileNamePattern">Pattern to match (without extension).</param>
        /// <returns>Full path to the found file or null if not found.</returns>
        private string FindAnimatedFile(string directoryPath, string fileNamePattern)
        {
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            {
                return null;
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
                _logger.LogDebug(ex, "Error scanning directory: {DirectoryPath}", directoryPath);
            }

            return null;
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