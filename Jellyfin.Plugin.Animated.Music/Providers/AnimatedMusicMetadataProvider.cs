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
        private const string HasAnimatedCoverKey = "AnimatedMusic.HasAnimatedCover";
        private const string HasVerticalBackgroundKey = "AnimatedMusic.HasVerticalBackground";
        private const string HasTrackSpecificBackgroundKey = "AnimatedMusic.HasTrackSpecificVerticalBackground";

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

                // Check for animated cover existence
                var hasAnimatedCover = CheckAnimatedCoverExists(item);
                var currentHasAnimatedCover = bool.TryParse(item.GetProviderId(HasAnimatedCoverKey), out var currentCover) && currentCover;

                if (hasAnimatedCover != currentHasAnimatedCover)
                {
                    item.SetProviderId(HasAnimatedCoverKey, hasAnimatedCover.ToString());
                    hasChanges = true;
                    _logger.LogDebug("Updated animated cover flag for track {TrackName}: {HasCover}", item.Name, hasAnimatedCover);
                }

                // Check for vertical background existence
                var (hasVerticalBackground, isTrackSpecific) = CheckVerticalBackgroundExists(item);
                var currentHasVerticalBackground = bool.TryParse(item.GetProviderId(HasVerticalBackgroundKey), out var currentBackground) && currentBackground;
                var currentIsTrackSpecific = bool.TryParse(item.GetProviderId(HasTrackSpecificBackgroundKey), out var currentTrackSpecific) && currentTrackSpecific;

                if (hasVerticalBackground != currentHasVerticalBackground)
                {
                    item.SetProviderId(HasVerticalBackgroundKey, hasVerticalBackground.ToString());
                    hasChanges = true;
                    _logger.LogDebug("Updated vertical background flag for track {TrackName}: {HasBackground}", item.Name, hasVerticalBackground);
                }

                if (isTrackSpecific != currentIsTrackSpecific)
                {
                    item.SetProviderId(HasTrackSpecificBackgroundKey, isTrackSpecific.ToString());
                    hasChanges = true;
                    _logger.LogDebug("Updated track-specific flag for track {TrackName}: {IsTrackSpecific}", item.Name, isTrackSpecific);
                }

                // Only return MetadataEdit if we actually made changes
                if (hasChanges)
                {
                    updateType = ItemUpdateType.MetadataEdit;
                    _logger.LogInformation("Updated animated metadata flags for track {TrackName} - HasCover: {HasCover}, HasBackground: {HasBackground}, IsTrackSpecific: {IsTrackSpecific}",
                        item.Name, hasAnimatedCover, hasVerticalBackground, isTrackSpecific);
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
        /// Checks if animated cover exists for a track (track-specific or album-level).
        /// </summary>
        /// <param name="track">The audio track.</param>
        /// <returns>True if animated cover exists; otherwise, false.</returns>
        private bool CheckAnimatedCoverExists(Audio track)
        {
            var folderPath = Path.GetDirectoryName(track.Path);
            var fileName = Path.GetFileName(track.Path);

            if (string.IsNullOrEmpty(folderPath) || string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            var album = track.AlbumEntity;
            var albumPath = album?.ContainingFolderPath;
            var trackFileName = Path.GetFileNameWithoutExtension(fileName);
            var trackAnimatedCoverPattern = $"cover-animated-{trackFileName}";

            // Check for track-specific animated cover first
            if (HasAnimatedFile(folderPath, trackAnimatedCoverPattern))
            {
                return true;
            }

            // Fall back to album-level animated cover
            return !string.IsNullOrEmpty(albumPath) && HasAnimatedFile(albumPath, "cover-animated");
        }

        /// <summary>
        /// Checks if vertical background exists for a track (track-specific or album-level).
        /// </summary>
        /// <param name="track">The audio track.</param>
        /// <returns>Tuple containing (hasVerticalBackground, isTrackSpecific).</returns>
        private (bool hasVerticalBackground, bool isTrackSpecific) CheckVerticalBackgroundExists(Audio track)
        {
            var folderPath = Path.GetDirectoryName(track.Path);
            var fileName = Path.GetFileName(track.Path);

            if (string.IsNullOrEmpty(folderPath) || string.IsNullOrEmpty(fileName))
            {
                return (false, false);
            }

            var album = track.AlbumEntity;
            var albumPath = album?.ContainingFolderPath;
            var trackFileName = Path.GetFileNameWithoutExtension(fileName);
            var verticalBackgroundPattern = $"vertical-background-{trackFileName}";

            // Check for track-specific vertical background first
            if (HasAnimatedFile(folderPath, verticalBackgroundPattern))
            {
                return (true, true);
            }

            // Fall back to album-level vertical background
            var hasAlbumBackground = !string.IsNullOrEmpty(albumPath) && HasAnimatedFile(albumPath, "vertical-background");
            return (hasAlbumBackground, false);
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