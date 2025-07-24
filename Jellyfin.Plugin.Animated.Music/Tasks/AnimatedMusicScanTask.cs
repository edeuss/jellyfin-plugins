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
        public string Name => "Scan for Animated Music Updates";

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

                foreach (var library in allLibraries)
                {
                    _logger.LogInformation("Library: {Name}, ExtraType: {ExtraType}, LocationType: {LocationType}",
                        library.Name, library.ExtraType, library.LocationType);
                }

                // Get all music libraries using the correct approach
                var musicLibraries = _libraryManager.RootFolder.Children
                    .Where(library => library.MediaType == MediaType.Audio)
                    .ToList();

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

                    foreach (var folder in refreshFolders)
                    {
                        foldersToRefresh.Add(folder);
                    }

                    processedLibraries++;
                    var progressPercentage = (double)processedLibraries / totalLibraries * 100;
                    progress?.Report(progressPercentage);
                }

                // Trigger refresh for folders with animated content
                if (foldersToRefresh.Any())
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
                            _logger.LogDebug("Refreshing tracks in folder: {FolderPath}", folder);

                            // Get all tracks in this folder and refresh their metadata
                            var tracksInFolder = _libraryManager.GetItemList(new InternalItemsQuery
                            {
                                IncludeItemTypes = new[] { BaseItemKind.Audio },
                                Recursive = true,
                                Path = folder
                            }).Cast<Audio>();

                            foreach (var track in tracksInFolder)
                            {
                                if (cancellationToken.IsCancellationRequested) break;

                                // Force metadata refresh for this specific track
                                await track.RefreshMetadata(new MetadataRefreshOptions(new DirectoryService(_fileSystem))
                                {
                                    ImageRefreshMode = MetadataRefreshMode.FullRefresh,
                                    MetadataRefreshMode = MetadataRefreshMode.FullRefresh,
                                    ForceSave = true
                                }, cancellationToken);

                                _logger.LogDebug("Refreshed metadata for track: {TrackName}", track.Name);
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