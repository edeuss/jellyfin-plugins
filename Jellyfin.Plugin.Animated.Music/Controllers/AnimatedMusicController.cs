using System;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.Animated.Music.Controllers
{
    /// <summary>
    /// Controller for serving animated music files.
    /// </summary>
    [ApiController]
    [Route("AnimatedMusic")]
    public class AnimatedMusicController : ControllerBase
    {
        private readonly ILogger<AnimatedMusicController> _logger;
        private readonly ILibraryManager _libraryManager;

        // Hardcoded configuration values
        private static readonly string[] SupportedAnimatedFormats = { ".gif", ".mp4", ".webm", ".mov", ".avi" };

        /// <summary>
        /// Initializes a new instance of the <see cref="AnimatedMusicController"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="libraryManager">The library manager.</param>
        public AnimatedMusicController(ILogger<AnimatedMusicController> logger, ILibraryManager libraryManager)
        {
            _logger = logger;
            _libraryManager = libraryManager;
        }

        /// <summary>
        /// Gets the animated cover for a music album.
        /// </summary>
        /// <param name="albumId">The album ID.</param>
        /// <returns>The animated cover file.</returns>
        [HttpGet("Album/{albumId}/AnimatedCover")]
        public IActionResult GetAnimatedCover(string albumId)
        {
            return ExecuteWithErrorHandling(albumId, "animated cover for album", () =>
            {
                var album = GetValidatedAlbum(albumId);
                var filePath = FindAnimatedFile(album.ContainingFolderPath, "cover-animated");
                return ServeAnimatedFile(filePath, $"animated cover for album {albumId}");
            });
        }

        /// <summary>
        /// Gets the vertical background for a music album.
        /// </summary>
        /// <param name="albumId">The album ID.</param>
        /// <returns>The vertical background file.</returns>
        [HttpGet("Album/{albumId}/VerticalBackground")]
        public IActionResult GetVerticalBackground(string albumId)
        {
            return ExecuteWithErrorHandling(albumId, "vertical background for album", () =>
            {
                var album = GetValidatedAlbum(albumId);
                var filePath = FindAnimatedFile(album.ContainingFolderPath, "vertical-background");
                return ServeAnimatedFile(filePath, $"vertical background for album {albumId}");
            });
        }

        /// <summary>
        /// Gets the vertical background for a music track.
        /// </summary>
        /// <param name="trackId">The track ID.</param>
        /// <returns>The vertical background file.</returns>
        [HttpGet("Track/{trackId}/VerticalBackground")]
        public IActionResult GetTrackVerticalBackground(string trackId)
        {
            return ExecuteWithErrorHandling(trackId, "vertical background for track", () =>
            {
                var track = GetValidatedTrack(trackId);
                var filePath = FindTrackVerticalBackground(track);
                return ServeAnimatedFile(filePath, $"track vertical background for track {trackId}");
            });
        }

        /// <summary>
        /// Gets the animated cover for the album containing the specified track.
        /// </summary>
        /// <param name="trackId">The track ID.</param>
        /// <returns>The animated cover file for the track's album.</returns>
        [HttpGet("Track/{trackId}/AnimatedCover")]
        public IActionResult GetTrackAnimatedCover(string trackId)
        {
            return ExecuteWithErrorHandling(trackId, "animated cover for track", () =>
            {
                var track = GetValidatedTrack(trackId);
                var filePath = FindTrackAnimatedCover(track);
                return ServeAnimatedFile(filePath, $"animated cover for track {trackId}");
            });
        }

        /// <summary>
        /// Gets information about animated files for a music album.
        /// </summary>
        /// <param name="albumId">The album ID.</param>
        /// <returns>Information about available animated files.</returns>
        [HttpGet("Album/{albumId}/Info")]
        public IActionResult GetAnimatedInfo(string albumId)
        {
            return ExecuteWithErrorHandling(albumId, "animated info for album", () =>
            {
                var album = GetValidatedAlbum(albumId);
                var albumPath = album.ContainingFolderPath;

                var animatedCoverPath = FindAnimatedFile(albumPath, "cover-animated");
                var verticalBackgroundPath = FindAnimatedFile(albumPath, "vertical-background");

                var info = new
                {
                    AlbumId = albumId,
                    HasAnimatedCover = !string.IsNullOrEmpty(animatedCoverPath),
                    HasVerticalBackground = !string.IsNullOrEmpty(verticalBackgroundPath),
                    AnimatedCoverUrl = !string.IsNullOrEmpty(animatedCoverPath) ? $"/AnimatedMusic/Album/{albumId}/AnimatedCover" : null,
                    VerticalBackgroundUrl = !string.IsNullOrEmpty(verticalBackgroundPath) ? $"/AnimatedMusic/Album/{albumId}/VerticalBackground" : null
                };

                return Ok(info);
            });
        }

        /// <summary>
        /// Gets information about animated files for a music track.
        /// </summary>
        /// <param name="trackId">The track ID.</param>
        /// <returns>Information about available animated files.</returns>
        [HttpGet("Track/{trackId}/Info")]
        public IActionResult GetTrackAnimatedInfo(string trackId)
        {
            return ExecuteWithErrorHandling(trackId, "animated info for track", () =>
            {
                var track = GetValidatedTrack(trackId);
                var folderPath = Path.GetDirectoryName(track.Path);
                var fileName = Path.GetFileName(track.Path);

                if (string.IsNullOrEmpty(folderPath) || string.IsNullOrEmpty(fileName))
                {
                    return NotFound("Track path not found");
                }

                var album = track.AlbumEntity;
                var albumPath = album?.ContainingFolderPath;
                var trackFileName = Path.GetFileNameWithoutExtension(fileName);
                var verticalBackgroundPattern = $"vertical-background-{trackFileName}";

                var trackVerticalBackgroundPath = FindAnimatedFile(folderPath, verticalBackgroundPattern);
                var albumVerticalBackgroundPath = string.IsNullOrEmpty(trackVerticalBackgroundPath) && !string.IsNullOrEmpty(albumPath)
                    ? FindAnimatedFile(albumPath, "vertical-background")
                    : null;

                var verticalBackgroundPath = trackVerticalBackgroundPath ?? albumVerticalBackgroundPath;
                var animatedCoverPath = !string.IsNullOrEmpty(albumPath) ? FindAnimatedFile(albumPath, "cover-animated") : null;

                var info = new
                {
                    TrackId = trackId,
                    TrackFileName = trackFileName,
                    HasAnimatedCover = !string.IsNullOrEmpty(animatedCoverPath),
                    HasVerticalBackground = !string.IsNullOrEmpty(verticalBackgroundPath),
                    HasTrackSpecificVerticalBackground = !string.IsNullOrEmpty(trackVerticalBackgroundPath),
                    AnimatedCoverUrl = !string.IsNullOrEmpty(animatedCoverPath) ? $"/AnimatedMusic/Track/{trackId}/AnimatedCover" : null,
                    VerticalBackgroundUrl = !string.IsNullOrEmpty(verticalBackgroundPath) ? $"/AnimatedMusic/Track/{trackId}/VerticalBackground" : null
                };

                return Ok(info);
            });
        }

        /// <summary>
        /// Gets animated metadata stored in track properties.
        /// </summary>
        /// <param name="trackId">The track ID.</param>
        /// <returns>Animated metadata information.</returns>
        [HttpGet("Track/{trackId}/Metadata")]
        public IActionResult GetTrackAnimatedMetadata(string trackId)
        {
            return ExecuteWithErrorHandling(trackId, "animated metadata for track", () =>
            {
                var track = GetValidatedTrack(trackId);

                var metadata = new
                {
                    TrackId = trackId,
                    TrackName = track.Name,
                    HasAnimatedCover = track.HasProviderId("AnimatedCover"),
                    HasVerticalBackground = track.HasProviderId("VerticalBackground"),
                    HasTrackSpecificVerticalBackground = bool.TryParse(track.GetProviderId("HasTrackSpecificVerticalBackground"), out var hasTrackSpecific) && hasTrackSpecific,
                    AnimatedCover = track.GetProviderId("AnimatedCover"),
                    VerticalBackground = track.GetProviderId("VerticalBackground"),
                    LastMetadataRefresh = track.DateLastRefreshed
                };

                return Ok(metadata);
            });
        }

        // Private helper methods to reduce duplication

        private IActionResult ExecuteWithErrorHandling(string id, string operation, Func<IActionResult> action)
        {
            try
            {
                return action();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error serving {Operation} for ID {Id}", operation, id);
                return StatusCode(500, "Internal server error");
            }
        }

        private MusicAlbum GetValidatedAlbum(string albumId)
        {
            if (!Guid.TryParse(albumId, out var guid))
            {
                throw new ArgumentException("Invalid album ID");
            }

            var item = _libraryManager.GetItemById(guid);
            if (item is not MusicAlbum album)
            {
                throw new ArgumentException("Album not found");
            }

            return album;
        }

        private Audio GetValidatedTrack(string trackId)
        {
            if (!Guid.TryParse(trackId, out var guid))
            {
                _logger.LogWarning("Invalid track ID format: {TrackId}", trackId);
                throw new ArgumentException("Invalid track ID");
            }

            var item = _libraryManager.GetItemById(guid);
            if (item == null)
            {
                _logger.LogWarning("Track not found in library: {TrackId}", trackId);
                throw new ArgumentException("Track not found");
            }

            if (item is not Audio track)
            {
                _logger.LogWarning("Item found but is not an Audio track. Item type: {ItemType}, Item name: {ItemName}",
                    item.GetType().Name, item.Name);
                throw new ArgumentException("Track not found");
            }

            return track;
        }

        private string FindTrackAnimatedCover(Audio track)
        {
            var folderPath = Path.GetDirectoryName(track.Path);
            var fileName = Path.GetFileName(track.Path);

            if (string.IsNullOrEmpty(folderPath) || string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException("Track path not found");
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

        private string FindTrackVerticalBackground(Audio track)
        {
            var folderPath = Path.GetDirectoryName(track.Path);
            var fileName = Path.GetFileName(track.Path);

            if (string.IsNullOrEmpty(folderPath) || string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException("Track path not found");
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

        private IActionResult ServeAnimatedFile(string filePath, string logContext)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return NotFound("Animated file not found");
            }

            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                return NotFound("Animated file not found");
            }

            var contentType = GetContentType(fileInfo.Extension);
            var stream = System.IO.File.OpenRead(filePath);

            _logger.LogDebug("Serving {LogContext}: {FilePath}", logContext, filePath);

            return File(stream, contentType);
        }

        private string FindAnimatedFile(string albumPath, string fileNamePattern)
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
                _logger.LogDebug(ex, "Error scanning directory: {AlbumPath}", albumPath);
            }

            return null;
        }

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

        private string GetContentType(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".gif" => "image/gif",
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                ".mov" => "video/quicktime",
                ".avi" => "video/x-msvideo",
                _ => "application/octet-stream"
            };
        }
    }
}