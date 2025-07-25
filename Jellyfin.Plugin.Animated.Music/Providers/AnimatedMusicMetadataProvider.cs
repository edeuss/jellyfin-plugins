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

        // Provider-specific metadata keys
        private const string AnimatedCoverKey = "AnimatedMusic.AnimatedCover";
        private const string VerticalBackgroundKey = "AnimatedMusic.VerticalBackground";
        private const string TrackSpecificBackgroundKey = "AnimatedMusic.HasTrackSpecificVerticalBackground";

        /// <summary>
        /// Initializes a new instance of the <see cref="AnimatedMusicMetadataProvider"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public AnimatedMusicMetadataProvider(ILogger<AnimatedMusicMetadataProvider> logger)
        {
            _logger = logger;
            _logger.LogInformation("AnimatedMusicMetadataProvider constructor called - provider initialized");
        }

        /// <inheritdoc />
        public string Name => "Animated Track Metadata Provider";

        /// <inheritdoc />
        public int Order => 10;

        /// <inheritdoc />
        public bool Supports(BaseItem item)
        {
            var supports = item is Audio;
            _logger.LogInformation("AnimatedMusicMetadataProvider.Supports called for item {ItemName} (ID: {ItemId}, type: {ItemType}): {Supports}",
                item.Name, item.Id, item.GetType().Name, supports);
            return supports;
        }

        /// <inheritdoc />
        public Task<ItemUpdateType> FetchAsync(Audio item, MetadataRefreshOptions options, CancellationToken cancellationToken)
        {
            _logger.LogInformation("=== AnimatedMusicMetadataProvider.FetchAsync STARTED for track {TrackName} (ID: {TrackId}) ===", item.Name, item.Id);
            _logger.LogInformation("MetadataRefreshOptions - MetadataRefreshMode: {MetadataMode}, ImageRefreshMode: {ImageMode}, ReplaceAllMetadata: {ReplaceAll}, ForceSave: {ForceSave}",
                options.MetadataRefreshMode, options.ImageRefreshMode, options.ReplaceAllMetadata, options.ForceSave);
            try
            {
                var updateType = ItemUpdateType.None;
                bool hasChanges = false;

                // Find animated cover
                var animatedCoverPath = FindTrackAnimatedCover(item);
                var currentAnimatedCover = item.GetProviderId(AnimatedCoverKey);

                // Validate and update animated cover
                if (!string.IsNullOrEmpty(animatedCoverPath))
                {
                    if (IsValidAnimatedFile(animatedCoverPath))
                    {
                        if (currentAnimatedCover != animatedCoverPath)
                        {
                            item.SetProviderId(AnimatedCoverKey, animatedCoverPath);
                            hasChanges = true;
                            _logger.LogDebug("Updated animated cover for track {TrackName}: {CoverPath}", item.Name, animatedCoverPath);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Found animated cover file but it's invalid: {CoverPath}", animatedCoverPath);
                        if (!string.IsNullOrEmpty(currentAnimatedCover))
                        {
                            item.SetProviderId(AnimatedCoverKey, string.Empty);
                            hasChanges = true;
                            _logger.LogDebug("Removed invalid animated cover reference for track {TrackName}", item.Name);
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(currentAnimatedCover))
                {
                    // Remove outdated animated cover reference
                    item.SetProviderId(AnimatedCoverKey, string.Empty);
                    hasChanges = true;
                    _logger.LogDebug("Removed outdated animated cover for track {TrackName}", item.Name);
                }

                // Clean up invalid existing references
                if (!string.IsNullOrEmpty(currentAnimatedCover) && !IsValidAnimatedFile(currentAnimatedCover))
                {
                    item.SetProviderId(AnimatedCoverKey, string.Empty);
                    hasChanges = true;
                    _logger.LogDebug("Removed invalid animated cover reference for track {TrackName}", item.Name);
                }

                // Find vertical background
                var verticalBackgroundPath = FindTrackVerticalBackground(item);
                var currentVerticalBackground = item.GetProviderId(VerticalBackgroundKey);
                var currentTrackSpecific = item.GetProviderId(TrackSpecificBackgroundKey);

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

                    if (IsValidAnimatedFile(finalPath))
                    {
                        if (currentVerticalBackground != finalPath)
                        {
                            item.SetProviderId(VerticalBackgroundKey, finalPath);
                            hasChanges = true;
                            _logger.LogDebug("Updated vertical background for track {TrackName}: {BackgroundPath}", item.Name, finalPath);
                        }

                        if (currentTrackSpecific != trackSpecificString)
                        {
                            item.SetProviderId(TrackSpecificBackgroundKey, trackSpecificString);
                            hasChanges = true;
                            _logger.LogDebug("Updated track-specific flag for track {TrackName}: {IsTrackSpecific}", item.Name, isTrackSpecific);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Found vertical background file but it's invalid: {BackgroundPath}", finalPath);
                        if (!string.IsNullOrEmpty(currentVerticalBackground))
                        {
                            item.SetProviderId(VerticalBackgroundKey, string.Empty);
                            item.SetProviderId(TrackSpecificBackgroundKey, string.Empty);
                            hasChanges = true;
                            _logger.LogDebug("Removed invalid vertical background reference for track {TrackName}", item.Name);
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(currentVerticalBackground))
                {
                    // Remove outdated vertical background references
                    item.SetProviderId(VerticalBackgroundKey, string.Empty);
                    item.SetProviderId(TrackSpecificBackgroundKey, string.Empty);
                    hasChanges = true;
                    _logger.LogDebug("Removed outdated vertical background for track {TrackName}", item.Name);
                }

                // Clean up invalid existing references
                if (!string.IsNullOrEmpty(currentVerticalBackground) && !IsValidAnimatedFile(currentVerticalBackground))
                {
                    item.SetProviderId(VerticalBackgroundKey, string.Empty);
                    item.SetProviderId(TrackSpecificBackgroundKey, string.Empty);
                    hasChanges = true;
                    _logger.LogDebug("Removed invalid vertical background reference for track {TrackName}", item.Name);
                }

                // Only return MetadataEdit if we actually made changes
                if (hasChanges)
                {
                    updateType = ItemUpdateType.MetadataEdit;
                    _logger.LogInformation("Updated animated metadata for track {TrackName} - changes detected. AnimatedCover: {HasCover}, VerticalBackground: {HasBackground}",
                        item.Name, !string.IsNullOrEmpty(animatedCoverPath), !string.IsNullOrEmpty(verticalBackgroundPath));
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
        /// Validates if a file path points to a valid animated file.
        /// </summary>
        /// <param name="filePath">The file path to validate.</param>
        /// <returns>True if the file is a valid animated file; otherwise, false.</returns>
        private bool IsValidAnimatedFile(string filePath)
        {
            return !string.IsNullOrEmpty(filePath) &&
                   File.Exists(filePath) &&
                   IsAnimatedFile(Path.GetFileName(filePath));
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