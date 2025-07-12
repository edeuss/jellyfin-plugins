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
        private readonly ILibraryManager _libraryManager;

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
                var configuration = GetConfiguration();
                
                if (!configuration.EnableAnimatedCovers)
                {
                    return NotFound("Animated covers are disabled");
                }

                var albumPath = GetAlbumPath(albumId);
                if (string.IsNullOrEmpty(albumPath))
                {
                    return NotFound("Album not found");
                }

                var animatedCoverPath = FindAnimatedFile(albumPath, configuration.AnimatedCoverFileName, configuration);
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
                var configuration = GetConfiguration();
                
                if (!configuration.EnableVerticalBackgrounds)
                {
                    return NotFound("Vertical backgrounds are disabled");
                }

                var albumPath = GetAlbumPath(albumId);
                if (string.IsNullOrEmpty(albumPath))
                {
                    return NotFound("Album not found");
                }

                var verticalBackgroundPath = FindAnimatedFile(albumPath, configuration.VerticalBackgroundFileName, configuration);
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
                var configuration = GetConfiguration();
                
                var albumPath = GetAlbumPath(albumId);
                if (string.IsNullOrEmpty(albumPath))
                {
                    return NotFound("Album not found");
                }

                var animatedCoverPath = FindAnimatedFile(albumPath, configuration.AnimatedCoverFileName, configuration);
                var verticalBackgroundPath = FindAnimatedFile(albumPath, configuration.VerticalBackgroundFileName, configuration);

                var info = new
                {
                    AlbumId = albumId,
                    HasAnimatedCover = !string.IsNullOrEmpty(animatedCoverPath) && configuration.EnableAnimatedCovers,
                    HasVerticalBackground = !string.IsNullOrEmpty(verticalBackgroundPath) && configuration.EnableVerticalBackgrounds,
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

        private string FindAnimatedFile(string albumPath, string fileNamePattern, PluginConfiguration configuration)
        {
            if (string.IsNullOrEmpty(albumPath) || !Directory.Exists(albumPath))
            {
                return null;
            }

            var maxFileSizeBytes = configuration.MaxFileSizeMB * 1024 * 1024;

            try
            {
                foreach (var file in Directory.GetFiles(albumPath))
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileInfo.Name);
                        
                        if (nameWithoutExtension.Equals(fileNamePattern, StringComparison.OrdinalIgnoreCase) &&
                            IsAnimatedFile(fileInfo.Name, configuration) &&
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

        private bool IsAnimatedFile(string fileName, PluginConfiguration configuration)
        {
            if (string.IsNullOrEmpty(fileName) || configuration.SupportedAnimatedFormats == null || configuration.SupportedAnimatedFormats.Length == 0)
            {
                return false;
            }

            try
            {
                var extension = Path.GetExtension(fileName).ToLowerInvariant();
                return Array.Exists(configuration.SupportedAnimatedFormats, f => f.Equals(extension, StringComparison.OrdinalIgnoreCase));
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