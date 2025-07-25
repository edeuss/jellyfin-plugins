using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using MediaBrowser.Model.IO;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.Animated.Music.Tasks
{
    /// <summary>
    /// Scheduled task to scan for animated music content updates.
    /// </summary>
    public class AnimatedMusicScanTask : IScheduledTask
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<AnimatedMusicScanTask> _logger;
        private readonly IFileSystem _fileSystem;

        // Hardcoded configuration values (same as providers)
        private static readonly string[] SupportedAnimatedFormats = { ".gif", ".mp4", ".webm", ".mov", ".avi" };

        /// <summary>
        /// Initializes a new instance of the <see cref="AnimatedMusicScanTask"/> class.
        /// </summary>
        /// <param name="libraryManager">The library manager.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="fileSystem">The file system.</param>
        public AnimatedMusicScanTask(ILibraryManager libraryManager, ILogger<AnimatedMusicScanTask> logger, IFileSystem fileSystem)
        {
            _libraryManager = libraryManager;
            _logger = logger;
            _fileSystem = fileSystem;
        }

        /// <inheritdoc />
        public string Name => "Scan for Animated Covers and backgrounds";

        /// <inheritdoc />
        public string Description => "Scans for new or updated animated covers and vertical backgrounds for music tracks and albums.";

        /// <inheritdoc />
        public string Category => "Animated Music";

        /// <inheritdoc />
        public string Key => "AnimatedMusicScan";

        /// <inheritdoc />
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerDaily,
                    TimeOfDayTicks = TimeSpan.FromHours(3).Ticks // Run at 3 AM daily
                }
            };
        }

        /// <inheritdoc />
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting animated music content scan...");

            try
            {
                var allLibraries = _libraryManager.RootFolder.Children.ToList();
                _logger.LogInformation("Found {Count} total libraries", allLibraries.Count);

                // Log all library properties for debugging
                foreach (var library in allLibraries)
                {
                    _logger.LogInformation("Library: {Name}, MediaType: {MediaType}, ExtraType: {ExtraType}, LocationType: {LocationType}",
                        library.Name, library.MediaType, library.ExtraType, library.LocationType);
                }

                // Improved music library detection
                var musicLibraries = new List<BaseItem>();

                foreach (var library in allLibraries)
                {
                    // Method 1: Check MediaType (most reliable when set correctly)
                    if (library.MediaType == MediaType.Audio)
                    {
                        musicLibraries.Add(library);
                        _logger.LogDebug("Added library '{LibraryName}' as music library (MediaType: Audio)", library.Name);
                        continue;
                    }

                    // Method 2: Check if it contains music content (fallback for Unknown MediaType)
                    if (library.MediaType == MediaType.Unknown)
                    {
                        try
                        {
                            // Check if this library contains any music albums or audio tracks
                            var hasMusicContent = await Task.Run(() => _libraryManager.GetItemList(new InternalItemsQuery
                            {
                                IncludeItemTypes = new[] { BaseItemKind.MusicAlbum, BaseItemKind.Audio },
                                Recursive = true,
                                Parent = library
                            }).Any(), cancellationToken);

                            if (hasMusicContent)
                            {
                                musicLibraries.Add(library);
                                _logger.LogInformation("Added library '{LibraryName}' as music library (MediaType: Unknown, but contains music content)", library.Name);
                            }
                            else
                            {
                                _logger.LogDebug("Library '{LibraryName}' has MediaType Unknown but contains no music content", library.Name);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error checking music content in library '{LibraryName}'", library.Name);
                        }
                    }
                }

                _logger.LogInformation("Found {Count} music libraries", musicLibraries.Count);

                if (musicLibraries.Count == 0)
                {
                    _logger.LogInformation("No music libraries found. Scan completed.");
                    progress?.Report(100);
                    return;
                }

                var totalLibraries = musicLibraries.Count;
                var processedLibraries = 0;
                var foldersToRefresh = new HashSet<string>();

                foreach (var musicLibrary in musicLibraries)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Animated music scan was cancelled.");
                        return;
                    }

                    _logger.LogInformation("Scanning music library: {LibraryName}", musicLibrary.Name);
                    var refreshFolders = await ScanMusicLibraryAsync(musicLibrary, cancellationToken);

                    _logger.LogInformation("Library {LibraryName} returned {FolderCount} folders to refresh",
                        musicLibrary.Name, refreshFolders.Count);

                    foreach (var folder in refreshFolders)
                    {
                        foldersToRefresh.Add(folder);
                    }

                    processedLibraries++;
                    var progressPercentage = (double)processedLibraries / totalLibraries * 100;
                    progress?.Report(progressPercentage);
                }

                _logger.LogInformation("Total folders to refresh after scanning all libraries: {TotalFolders}", foldersToRefresh.Count);

                // Trigger refresh for folders with animated content
                if (foldersToRefresh.Count != 0)
                {
                    _logger.LogInformation("Triggering metadata refresh for tracks in {FolderCount} folders with animated content", foldersToRefresh.Count);

                    foreach (var folder in foldersToRefresh)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        try
                        {
                            // Try to find the album that corresponds to this folder
                            var album = await Task.Run(() => _libraryManager.GetItemList(new InternalItemsQuery
                            {
                                IncludeItemTypes = new[] { BaseItemKind.MusicAlbum },
                                Recursive = true
                            }).Cast<MusicAlbum>().FirstOrDefault(a => a.ContainingFolderPath == folder), cancellationToken);

                            if (album == null)
                            {
                                _logger.LogWarning("Could not find album for folder: {FolderPath}", folder);
                                continue;
                            }

                            _logger.LogDebug("Found album: {AlbumName} for folder: {FolderPath}", album.Name, folder);

                            // Check if album has animated cover before refreshing
                            var hasAlbumAnimatedCover = CheckForAnimatedCover(album.ContainingFolderPath);
                            if (hasAlbumAnimatedCover)
                            {
                                _logger.LogInformation("Album {AlbumName} has animated cover, refreshing metadata", album.Name);
                                try
                                {
                                    await album.RefreshMetadata(new MetadataRefreshOptions(new DirectoryService(_fileSystem))
                                    {
                                        ImageRefreshMode = MetadataRefreshMode.None,
                                        MetadataRefreshMode = MetadataRefreshMode.FullRefresh,
                                        ForceSave = true,
                                        ReplaceAllMetadata = false,
                                        ReplaceAllImages = false
                                    }, cancellationToken);

                                    // Verify album metadata was updated
                                    var albumHasAnimatedCover = bool.TryParse(album.GetProviderId("AnimatedMusic.HasAnimatedCover"), out var albumCover) && albumCover;
                                    _logger.LogInformation("Completed album metadata refresh - Album: {AlbumName} (Cover: {AlbumCover})",
                                        album.Name, albumHasAnimatedCover);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Failed to refresh metadata for album: {AlbumName}", album.Name);
                                }
                            }
                            else
                            {
                                _logger.LogDebug("Album {AlbumName} has no animated cover, skipping metadata refresh", album.Name);
                            }

                            // Get all tracks in this album
                            var tracksInAlbum = _libraryManager.GetItemList(new InternalItemsQuery
                            {
                                IncludeItemTypes = new[] { BaseItemKind.Audio },
                                Recursive = false,
                                Parent = album
                            }).Cast<Audio>();

                            _logger.LogInformation("Found {TrackCount} tracks in album: {AlbumName}", tracksInAlbum.Count(), album.Name);

                            foreach (var track in tracksInAlbum)
                            {
                                if (cancellationToken.IsCancellationRequested) break;

                                try
                                {
                                    var trackFolderPath = Path.GetDirectoryName(track.Path);
                                    var trackFileName = Path.GetFileNameWithoutExtension(track.Path);

                                    // Check if this specific track has vertical background before refreshing
                                    var hasTrackVerticalBackground = HasTrackSpecificVerticalBackground(trackFolderPath, trackFileName);

                                    if (hasTrackVerticalBackground)
                                    {
                                        _logger.LogInformation("Track {TrackName} has vertical background, refreshing metadata", track.Name);

                                        // Force metadata refresh for this specific track
                                        await track.RefreshMetadata(new MetadataRefreshOptions(new DirectoryService(_fileSystem))
                                        {
                                            ImageRefreshMode = MetadataRefreshMode.None,
                                            MetadataRefreshMode = MetadataRefreshMode.FullRefresh,
                                            ForceSave = true,
                                            ReplaceAllMetadata = false,
                                            ReplaceAllImages = false
                                        }, cancellationToken);

                                        // Verify that the provider was called by checking if provider IDs were set
                                        var trackHasVerticalBackground = bool.TryParse(track.GetProviderId("AnimatedMusic.HasVerticalBackground"), out var trackBg) && trackBg;

                                        _logger.LogInformation("Completed track metadata refresh - Track: {TrackName} (Background: {TrackBg})",
                                            track.Name, trackHasVerticalBackground);
                                    }
                                    else
                                    {
                                        _logger.LogDebug("Track {TrackName} has no vertical background, skipping metadata refresh", track.Name);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to refresh metadata for track: {TrackName}", track.Name);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to refresh tracks in folder: {FolderPath}", folder);
                        }
                    }
                }

                _logger.LogInformation("Animated music content scan completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during animated music content scan.");
                throw;
            }
        }

        /// <summary>
        /// Scans a music library for animated content updates.
        /// </summary>
        /// <param name="musicLibrary">The music library to scan.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A list of folder paths that need refreshing.</returns>
        private async Task<List<string>> ScanMusicLibraryAsync(BaseItem musicLibrary, CancellationToken cancellationToken)
        {
            var foldersToRefresh = new List<string>();

            var musicAlbums = await Task.Run(() => _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.MusicAlbum },
                Recursive = true,
                Parent = musicLibrary
            }).Cast<MusicAlbum>().ToList());

            foreach (var album in musicAlbums)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return foldersToRefresh;
                }

                var albumPath = album.ContainingFolderPath;
                if (string.IsNullOrEmpty(albumPath))
                {
                    continue;
                }

                // Check for album-level animated content
                var hasAlbumAnimatedContent = CheckForAnimatedContent(albumPath);
                var hasTrackSpecificContent = false;

                // Check for track-specific animated content
                var albumTracks = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.Audio },
                    Recursive = false,
                    Parent = album
                }).Cast<Audio>().ToList();

                foreach (var track in albumTracks)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return foldersToRefresh;
                    }

                    var trackFolderPath = Path.GetDirectoryName(track.Path);
                    var trackFileName = Path.GetFileNameWithoutExtension(track.Path);

                    if (HasTrackSpecificAnimatedContent(trackFolderPath, trackFileName))
                    {
                        hasTrackSpecificContent = true;
                        _logger.LogDebug("Found track-specific animated content for: {TrackName}", track.Name);
                        break; // Found content in this album, no need to check more tracks
                    }
                }

                // If we found animated content, mark this album folder for refresh
                if (hasAlbumAnimatedContent || hasTrackSpecificContent)
                {
                    if (!foldersToRefresh.Contains(albumPath))
                    {
                        foldersToRefresh.Add(albumPath);
                        _logger.LogDebug("Added album folder for refresh: {AlbumPath} ({AlbumName})", albumPath, album.Name);
                    }
                }
            }

            return foldersToRefresh;
        }

        /// <summary>
        /// Checks if a directory contains animated content files.
        /// </summary>
        /// <param name="directoryPath">The directory path to check.</param>
        /// <returns>True if animated content is found; otherwise, false.</returns>
        private bool CheckForAnimatedContent(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            {
                return false;
            }

            try
            {
                var animatedFiles = Directory.GetFiles(directoryPath)
                    .Where(file =>
                    {
                        var fileName = Path.GetFileName(file);
                        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                        var extension = Path.GetExtension(fileName).ToLowerInvariant();

                        return (nameWithoutExtension.Equals("cover-animated", StringComparison.OrdinalIgnoreCase) ||
                                nameWithoutExtension.Equals("vertical-background", StringComparison.OrdinalIgnoreCase)) &&
                               Array.Exists(SupportedAnimatedFormats, f => f.Equals(extension, StringComparison.OrdinalIgnoreCase));
                    });

                return animatedFiles.Any();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error checking for animated content in directory: {DirectoryPath}", directoryPath);
                return false;
            }
        }

        /// <summary>
        /// Checks if a directory contains track-specific animated content.
        /// </summary>
        /// <param name="directoryPath">The directory path to check.</param>
        /// <param name="trackFileName">The track file name without extension.</param>
        /// <returns>True if track-specific animated content is found; otherwise, false.</returns>
        private bool HasTrackSpecificAnimatedContent(string directoryPath, string trackFileName)
        {
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath) || string.IsNullOrEmpty(trackFileName))
            {
                return false;
            }

            try
            {
                var trackCoverPattern = $"cover-animated-{trackFileName}";
                var trackBackgroundPattern = $"vertical-background-{trackFileName}";

                var trackSpecificFiles = Directory.GetFiles(directoryPath)
                    .Where(file =>
                    {
                        var fileName = Path.GetFileName(file);
                        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                        var extension = Path.GetExtension(fileName).ToLowerInvariant();

                        return (nameWithoutExtension.Equals(trackCoverPattern, StringComparison.OrdinalIgnoreCase) ||
                                nameWithoutExtension.Equals(trackBackgroundPattern, StringComparison.OrdinalIgnoreCase)) &&
                               Array.Exists(SupportedAnimatedFormats, f => f.Equals(extension, StringComparison.OrdinalIgnoreCase));
                    });

                return trackSpecificFiles.Any();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error checking for track-specific animated content in directory: {DirectoryPath}", directoryPath);
                return false;
            }
        }

        /// <summary>
        /// Checks if a directory contains an animated cover file.
        /// </summary>
        /// <param name="directoryPath">The directory path to check.</param>
        /// <returns>True if animated cover is found; otherwise, false.</returns>
        private bool CheckForAnimatedCover(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            {
                return false;
            }

            try
            {
                var animatedCoverFiles = Directory.GetFiles(directoryPath)
                    .Where(file =>
                    {
                        var fileName = Path.GetFileName(file);
                        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                        var extension = Path.GetExtension(fileName).ToLowerInvariant();

                        return nameWithoutExtension.Equals("cover-animated", StringComparison.OrdinalIgnoreCase) &&
                               Array.Exists(SupportedAnimatedFormats, f => f.Equals(extension, StringComparison.OrdinalIgnoreCase));
                    });

                return animatedCoverFiles.Any();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error checking for animated cover in directory: {DirectoryPath}", directoryPath);
                return false;
            }
        }

        /// <summary>
        /// Checks if a directory contains track-specific vertical background.
        /// </summary>
        /// <param name="directoryPath">The directory path to check.</param>
        /// <param name="trackFileName">The track file name without extension.</param>
        /// <returns>True if track-specific vertical background is found; otherwise, false.</returns>
        private bool HasTrackSpecificVerticalBackground(string directoryPath, string trackFileName)
        {
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath) || string.IsNullOrEmpty(trackFileName))
            {
                return false;
            }

            try
            {
                var trackBackgroundPattern = $"vertical-background-{trackFileName}";

                var trackBackgroundFiles = Directory.GetFiles(directoryPath)
                    .Where(file =>
                    {
                        var fileName = Path.GetFileName(file);
                        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                        var extension = Path.GetExtension(fileName).ToLowerInvariant();

                        return nameWithoutExtension.Equals(trackBackgroundPattern, StringComparison.OrdinalIgnoreCase) &&
                               Array.Exists(SupportedAnimatedFormats, f => f.Equals(extension, StringComparison.OrdinalIgnoreCase));
                    });

                return trackBackgroundFiles.Any();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error checking for track-specific vertical background in directory: {DirectoryPath}", directoryPath);
                return false;
            }
        }
    }

    /// <summary>
    /// Simple progress implementation for library operations.
    /// </summary>
    public class SimpleProgress<T> : IProgress<T>
    {
        public void Report(T value)
        {
            // Simple implementation that doesn't report progress
        }
    }
}