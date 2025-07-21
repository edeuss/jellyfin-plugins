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
    /// Metadata provider for adding animated content information to audio tracks.
    /// </summary>
    public class AnimatedMusicMetadataProvider : IMetadataProvider<Audio>, IHasOrder
    {
        private readonly ILogger<AnimatedMusicMetadataProvider> _logger;

        // Hardcoded configuration values (same as controller)
        private static readonly string[] SupportedAnimatedFormats = { ".gif", ".mp4", ".webm", ".mov", ".avi" };

        /// <summary>
        /// Initializes a new instance of the <see cref="AnimatedMusicMetadataProvider"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public AnimatedMusicMetadataProvider(ILogger<AnimatedMusicMetadataProvider> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public string Name => "Animated Music Metadata Provider";

        /// <inheritdoc />
        public int Order => 10; // Lower number = higher priority

        /// <inheritdoc />
        public bool Supports(BaseItem item)
        {
            var supports = item is Audio;
            _logger.LogInformation("AnimatedMusicMetadataProvider.Supports called for item {ItemName} (type: {ItemType}): {Supports}",
                item.Name, item.GetType().Name, supports);
            return supports;
        }

        /// <inheritdoc />
        public Task<ItemUpdateType> FetchAsync(Audio item, MetadataRefreshOptions options, CancellationToken cancellationToken)
        {
            _logger.LogInformation("AnimatedMusicMetadataProvider.FetchAsync called for track {TrackName}", item.Name);
            try
            {
                var updateType = ItemUpdateType.None;
                bool hasChanges = false;

                // Find animated cover
                var animatedCoverPath = FindTrackAnimatedCover(item);
                var currentAnimatedCover = item.GetProviderId("AnimatedCover");

                if (!string.IsNullOrEmpty(animatedCoverPath))
                {
                    if (currentAnimatedCover != animatedCoverPath)
                    {
                        item.SetProviderId("AnimatedCover", animatedCoverPath);
                        hasChanges = true;
                        _logger.LogDebug("Updated animated cover for track {TrackName}: {CoverPath}", item.Name, animatedCoverPath);
                    }
                }
                else if (!string.IsNullOrEmpty(currentAnimatedCover))
                {
                    // Remove outdated animated cover reference
                    item.SetProviderId("AnimatedCover", string.Empty);
                    hasChanges = true;
                    _logger.LogDebug("Removed outdated animated cover for track {TrackName}", item.Name);
                }

                // Find vertical background
                var verticalBackgroundPath = FindTrackVerticalBackground(item);
                var currentVerticalBackground = item.GetProviderId("VerticalBackground");
                var currentTrackSpecific = item.GetProviderId("HasTrackSpecificVerticalBackground");

                if (!string.IsNullOrEmpty(verticalBackgroundPath))
                {
                    // Check if it's track-specific
                    var folderPath = Path.GetDirectoryName(item.Path);
                    var fileName = Path.GetFileName(item.Path);
                    var trackFileName = Path.GetFileNameWithoutExtension(fileName);
                    var trackVerticalBackgroundPattern = $"vertical-background-{trackFileName}";
                    var trackSpecificPath = FindAnimatedFile(folderPath, trackVerticalBackgroundPattern);

                    // Prioritize track-specific path if it exists, otherwise use the general path
                    var finalPath = !string.IsNullOrEmpty(trackSpecificPath) ? trackSpecificPath : verticalBackgroundPath;
                    var isTrackSpecific = !string.IsNullOrEmpty(trackSpecificPath);
                    var trackSpecificString = isTrackSpecific.ToString();

                    if (currentVerticalBackground != finalPath)
                    {
                        item.SetProviderId("VerticalBackground", finalPath);
                        hasChanges = true;
                        _logger.LogDebug("Updated vertical background for track {TrackName}: {BackgroundPath}", item.Name, finalPath);
                    }

                    if (currentTrackSpecific != trackSpecificString)
                    {
                        item.SetProviderId("HasTrackSpecificVerticalBackground", trackSpecificString);
                        hasChanges = true;
                        _logger.LogDebug("Updated track-specific flag for track {TrackName}: {IsTrackSpecific}", item.Name, isTrackSpecific);
                    }
                }
                else if (!string.IsNullOrEmpty(currentVerticalBackground))
                {
                    // Remove outdated vertical background references
                    item.SetProviderId("VerticalBackground", string.Empty);
                    item.SetProviderId("HasTrackSpecificVerticalBackground", string.Empty);
                    hasChanges = true;
                    _logger.LogDebug("Removed outdated vertical background for track {TrackName}", item.Name);
                }

                // Only return MetadataEdit if we actually made changes
                if (hasChanges)
                {
                    updateType = ItemUpdateType.MetadataEdit;
                    _logger.LogInformation("Updated animated metadata for track {TrackName} - changes detected", item.Name);
                }
                else
                {
                    _logger.LogDebug("No changes needed for track {TrackName}", item.Name);
                }

                return Task.FromResult(updateType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching animated metadata for track {TrackId}: {TrackName}", item.Id, item.Name);
                return Task.FromResult(ItemUpdateType.None);
            }
        }

        /// <summary>
        /// Finds animated cover for a track (track-specific or album-level).
        /// </summary>
        /// <param name="track">The audio track.</param>
        /// <returns>Path to animated cover file or null if not found.</returns>
        private string FindTrackAnimatedCover(Audio track)
        {
            var folderPath = Path.GetDirectoryName(track.Path);
            var fileName = Path.GetFileName(track.Path);

            if (string.IsNullOrEmpty(folderPath) || string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            var album = track.AlbumEntity;
            var albumPath = album?.ContainingFolderPath;
            var trackFileName = Path.GetFileNameWithoutExtension(fileName);
            var trackAnimatedCoverPattern = $"cover-animated-{trackFileName}";

            // Check for track-specific animated cover first
            var trackAnimatedCoverPath = FindAnimatedFile(folderPath, trackAnimatedCoverPattern);

            // Fall back to album-level animated cover if no track-specific one found
            var albumAnimatedCoverPath = string.IsNullOrEmpty(trackAnimatedCoverPath) && !string.IsNullOrEmpty(albumPath)
                ? FindAnimatedFile(albumPath, "cover-animated")
                : null;

            return trackAnimatedCoverPath ?? albumAnimatedCoverPath;
        }

        /// <summary>
        /// Finds vertical background for a track (track-specific or album-level).
        /// </summary>
        /// <param name="track">The audio track.</param>
        /// <returns>Path to vertical background file or null if not found.</returns>
        private string FindTrackVerticalBackground(Audio track)
        {
            var folderPath = Path.GetDirectoryName(track.Path);
            var fileName = Path.GetFileName(track.Path);

            if (string.IsNullOrEmpty(folderPath) || string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            var album = track.AlbumEntity;
            var albumPath = album?.ContainingFolderPath;
            var trackFileName = Path.GetFileNameWithoutExtension(fileName);
            var verticalBackgroundPattern = $"vertical-background-{trackFileName}";

            var trackVerticalBackgroundPath = FindAnimatedFile(folderPath, verticalBackgroundPattern);
            var albumVerticalBackgroundPath = string.IsNullOrEmpty(trackVerticalBackgroundPath) && !string.IsNullOrEmpty(albumPath)
                ? FindAnimatedFile(albumPath, "vertical-background")
                : null;

            return trackVerticalBackgroundPath ?? albumVerticalBackgroundPath;
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