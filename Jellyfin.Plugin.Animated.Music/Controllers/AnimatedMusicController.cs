using System;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using Jellyfin.Plugin.Animated.Music.Configuration;

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
        private readonly PluginConfiguration _configuration;
        private readonly ILibraryManager _libraryManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="AnimatedMusicController"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="configuration">The plugin configuration.</param>
        /// <param name="libraryManager">The library manager.</param>
        public AnimatedMusicController(ILogger<AnimatedMusicController> logger, PluginConfiguration configuration, ILibraryManager libraryManager)
        {
            _logger = logger;
            _configuration = configuration;
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
            try
            {
                if (!_configuration.EnableAnimatedCovers)
                {
                    return NotFound("Animated covers are disabled");
                }

                var albumPath = GetAlbumPath(albumId);
                if (string.IsNullOrEmpty(albumPath))
                {
                    return NotFound("Album not found");
                }

                var animatedCoverPath = FindAnimatedFile(albumPath, _configuration.AnimatedCoverFileName);
                if (string.IsNullOrEmpty(animatedCoverPath))
                {
                    return NotFound("Animated cover not found");
                }

                var fileInfo = new FileInfo(animatedCoverPath);
                if (!fileInfo.Exists)
                {
                    return NotFound("Animated cover file not found");
                }

                var contentType = GetContentType(fileInfo.Extension);
                var stream = System.IO.File.OpenRead(animatedCoverPath);

                _logger.LogDebug("Serving animated cover for album {AlbumId}: {FilePath}", albumId, animatedCoverPath);

                return File(stream, contentType, fileInfo.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error serving animated cover for album {AlbumId}", albumId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets the vertical background for a music album.
        /// </summary>
        /// <param name="albumId">The album ID.</param>
        /// <returns>The vertical background file.</returns>
        [HttpGet("Album/{albumId}/VerticalBackground")]
        public IActionResult GetVerticalBackground(string albumId)
        {
            try
            {
                if (!_configuration.EnableVerticalBackgrounds)
                {
                    return NotFound("Vertical backgrounds are disabled");
                }

                var albumPath = GetAlbumPath(albumId);
                if (string.IsNullOrEmpty(albumPath))
                {
                    return NotFound("Album not found");
                }

                var verticalBackgroundPath = FindAnimatedFile(albumPath, _configuration.VerticalBackgroundFileName);
                if (string.IsNullOrEmpty(verticalBackgroundPath))
                {
                    return NotFound("Vertical background not found");
                }

                var fileInfo = new FileInfo(verticalBackgroundPath);
                if (!fileInfo.Exists)
                {
                    return NotFound("Vertical background file not found");
                }

                var contentType = GetContentType(fileInfo.Extension);
                var stream = System.IO.File.OpenRead(verticalBackgroundPath);

                _logger.LogDebug("Serving vertical background for album {AlbumId}: {FilePath}", albumId, verticalBackgroundPath);

                return File(stream, contentType, fileInfo.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error serving vertical background for album {AlbumId}", albumId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets information about animated files for a music album.
        /// </summary>
        /// <param name="albumId">The album ID.</param>
        /// <returns>Information about available animated files.</returns>
        [HttpGet("Album/{albumId}/Info")]
        public IActionResult GetAnimatedInfo(string albumId)
        {
            try
            {
                var albumPath = GetAlbumPath(albumId);
                if (string.IsNullOrEmpty(albumPath))
                {
                    return NotFound("Album not found");
                }

                var animatedCoverPath = FindAnimatedFile(albumPath, _configuration.AnimatedCoverFileName);
                var verticalBackgroundPath = FindAnimatedFile(albumPath, _configuration.VerticalBackgroundFileName);

                var info = new
                {
                    AlbumId = albumId,
                    HasAnimatedCover = !string.IsNullOrEmpty(animatedCoverPath) && _configuration.EnableAnimatedCovers,
                    HasVerticalBackground = !string.IsNullOrEmpty(verticalBackgroundPath) && _configuration.EnableVerticalBackgrounds,
                    AnimatedCoverUrl = !string.IsNullOrEmpty(animatedCoverPath) ? $"/AnimatedMusic/Album/{albumId}/AnimatedCover" : null,
                    VerticalBackgroundUrl = !string.IsNullOrEmpty(verticalBackgroundPath) ? $"/AnimatedMusic/Album/{albumId}/VerticalBackground" : null
                };

                return Ok(info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting animated info for album {AlbumId}", albumId);
                return StatusCode(500, "Internal server error");
            }
        }

        private string GetAlbumPath(string albumId)
        {
            try
            {
                if (!Guid.TryParse(albumId, out var guid))
                {
                    return null;
                }

                var item = _libraryManager.GetItemById(guid);
                if (item is not MusicAlbum album)
                {
                    return null;
                }

                return album.ContainingFolderPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting album path for ID {AlbumId}", albumId);
                return null;
            }
        }

        private string FindAnimatedFile(string albumPath, string fileNamePattern)
        {
            if (string.IsNullOrEmpty(albumPath) || !Directory.Exists(albumPath))
            {
                return null;
            }

            var maxFileSizeBytes = _configuration.MaxFileSizeMB * 1024 * 1024;

            foreach (var file in Directory.GetFiles(albumPath))
            {
                var fileInfo = new FileInfo(file);
                var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileInfo.Name);
                
                if (nameWithoutExtension.Equals(fileNamePattern, StringComparison.OrdinalIgnoreCase) &&
                    IsAnimatedFile(fileInfo.Name) &&
                    fileInfo.Length <= maxFileSizeBytes)
                {
                    return file;
                }
            }

            return null;
        }

        private bool IsAnimatedFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return Array.Exists(_configuration.SupportedAnimatedFormats, f => f.Equals(extension, StringComparison.OrdinalIgnoreCase));
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