using System;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Animated.Music.Models;
using Jellyfin.Plugin.Animated.Music.Services;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Jellyfin.Plugin.Animated.Music.Controllers
{
    /// <summary>
    /// REST API for animated music covers and vertical backgrounds.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("AnimatedMusic")]
    public class AnimatedMusicController : ControllerBase
    {
        private readonly ILibraryManager _libraryManager;
        private readonly AnimatedAssetLocator _locator;
        private readonly PreviewFrameExtractor _previewExtractor;

        /// <summary>
        /// Initializes a new instance of the <see cref="AnimatedMusicController"/> class.
        /// </summary>
        /// <param name="libraryManager">The library manager.</param>
        /// <param name="locator">The asset locator.</param>
        /// <param name="previewExtractor">The preview extractor.</param>
        public AnimatedMusicController(
            ILibraryManager libraryManager,
            AnimatedAssetLocator locator,
            PreviewFrameExtractor previewExtractor)
        {
            _libraryManager = libraryManager;
            _locator = locator;
            _previewExtractor = previewExtractor;
        }

        /// <summary>
        /// Returns plugin status.
        /// </summary>
        /// <returns>Plugin status.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(PluginStatusDto), StatusCodes.Status200OK)]
        public ActionResult<PluginStatusDto> GetStatus()
        {
            return new PluginStatusDto
            {
                PluginName = "Animated Music",
                Version = Plugin.Instance.Version.ToString(),
                Status = "Available"
            };
        }

        /// <summary>
        /// Gets animated assets for an album.
        /// </summary>
        /// <param name="albumId">The album ID.</param>
        /// <returns>Album asset info.</returns>
        [HttpGet("Albums/{albumId:guid}")]
        [ProducesResponseType(typeof(AlbumAnimatedInfoDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<AlbumAnimatedInfoDto> GetAlbum(Guid albumId)
        {
            var album = GetAlbumOrNull(albumId);
            if (album is null)
            {
                return NotFound();
            }

            var cover = _locator.FindAlbumAsset(album, AnimatedAssetKind.Cover);
            var tall = _locator.FindAlbumAsset(album, AnimatedAssetKind.TallCover);
            var background = _locator.FindAlbumAsset(album, AnimatedAssetKind.VerticalBackground);
            var prefix = $"/AnimatedMusic/Albums/{albumId}";

            return new AlbumAnimatedInfoDto
            {
                AlbumId = albumId,
                Cover = ToDto(cover, $"{prefix}/Cover", $"{prefix}/Cover/Preview"),
                TallCover = ToDto(tall, $"{prefix}/TallCover", $"{prefix}/TallCover/Preview"),
                VerticalBackground = ToDto(background, $"{prefix}/VerticalBackground", $"{prefix}/VerticalBackground/Preview")
            };
        }

        /// <summary>
        /// Gets the album animated cover.
        /// </summary>
        /// <param name="albumId">The album ID.</param>
        /// <returns>The cover file.</returns>
        [HttpGet("Albums/{albumId:guid}/Cover")]
        [HttpHead("Albums/{albumId:guid}/Cover")]
        public IActionResult GetAlbumCover(Guid albumId)
        {
            return ServeAlbumAsset(albumId, AnimatedAssetKind.Cover);
        }

        /// <summary>
        /// Gets a first-frame preview of the album animated cover.
        /// </summary>
        /// <param name="albumId">The album ID.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The preview image.</returns>
        [HttpGet("Albums/{albumId:guid}/Cover/Preview")]
        [HttpHead("Albums/{albumId:guid}/Cover/Preview")]
        public Task<IActionResult> GetAlbumCoverPreview(Guid albumId, CancellationToken cancellationToken)
        {
            return ServeAlbumPreview(albumId, AnimatedAssetKind.Cover, cancellationToken);
        }

        /// <summary>
        /// Gets the album tall animated cover.
        /// </summary>
        /// <param name="albumId">The album ID.</param>
        /// <returns>The tall cover file.</returns>
        [HttpGet("Albums/{albumId:guid}/TallCover")]
        [HttpHead("Albums/{albumId:guid}/TallCover")]
        public IActionResult GetAlbumTallCover(Guid albumId)
        {
            return ServeAlbumAsset(albumId, AnimatedAssetKind.TallCover);
        }

        /// <summary>
        /// Gets a first-frame preview of the album tall animated cover.
        /// </summary>
        /// <param name="albumId">The album ID.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The preview image.</returns>
        [HttpGet("Albums/{albumId:guid}/TallCover/Preview")]
        [HttpHead("Albums/{albumId:guid}/TallCover/Preview")]
        public Task<IActionResult> GetAlbumTallCoverPreview(Guid albumId, CancellationToken cancellationToken)
        {
            return ServeAlbumPreview(albumId, AnimatedAssetKind.TallCover, cancellationToken);
        }

        /// <summary>
        /// Gets the album vertical background.
        /// </summary>
        /// <param name="albumId">The album ID.</param>
        /// <returns>The background file.</returns>
        [HttpGet("Albums/{albumId:guid}/VerticalBackground")]
        [HttpHead("Albums/{albumId:guid}/VerticalBackground")]
        public IActionResult GetAlbumVerticalBackground(Guid albumId)
        {
            return ServeAlbumAsset(albumId, AnimatedAssetKind.VerticalBackground);
        }

        /// <summary>
        /// Gets a first-frame preview of the album vertical background.
        /// </summary>
        /// <param name="albumId">The album ID.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The preview image.</returns>
        [HttpGet("Albums/{albumId:guid}/VerticalBackground/Preview")]
        [HttpHead("Albums/{albumId:guid}/VerticalBackground/Preview")]
        public Task<IActionResult> GetAlbumVerticalBackgroundPreview(Guid albumId, CancellationToken cancellationToken)
        {
            return ServeAlbumPreview(albumId, AnimatedAssetKind.VerticalBackground, cancellationToken);
        }

        /// <summary>
        /// Gets animated assets for a track.
        /// </summary>
        /// <param name="trackId">The track ID.</param>
        /// <returns>Track asset info.</returns>
        [HttpGet("Tracks/{trackId:guid}")]
        [ProducesResponseType(typeof(TrackAnimatedInfoDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<TrackAnimatedInfoDto> GetTrack(Guid trackId)
        {
            var track = GetTrackOrNull(trackId);
            if (track is null)
            {
                return NotFound();
            }

            var album = track.FindParent<MusicAlbum>();
            var cover = _locator.FindTrackAsset(track, AnimatedAssetKind.Cover);
            var tall = _locator.FindTrackAsset(track, AnimatedAssetKind.TallCover);
            var background = _locator.FindTrackAsset(track, AnimatedAssetKind.VerticalBackground);
            var prefix = $"/AnimatedMusic/Tracks/{trackId}";

            return new TrackAnimatedInfoDto
            {
                TrackId = trackId,
                AlbumId = album?.Id,
                TrackFileName = Path.GetFileNameWithoutExtension(track.Path),
                Cover = ToDto(cover, $"{prefix}/Cover", $"{prefix}/Cover/Preview", includeTrackSpecific: true),
                TallCover = ToDto(tall, $"{prefix}/TallCover", $"{prefix}/TallCover/Preview", includeTrackSpecific: true),
                VerticalBackground = ToDto(background, $"{prefix}/VerticalBackground", $"{prefix}/VerticalBackground/Preview", includeTrackSpecific: true)
            };
        }

        /// <summary>
        /// Gets the track animated cover.
        /// </summary>
        /// <param name="trackId">The track ID.</param>
        /// <returns>The cover file.</returns>
        [HttpGet("Tracks/{trackId:guid}/Cover")]
        [HttpHead("Tracks/{trackId:guid}/Cover")]
        public IActionResult GetTrackCover(Guid trackId)
        {
            return ServeTrackAsset(trackId, AnimatedAssetKind.Cover);
        }

        /// <summary>
        /// Gets a first-frame preview of the track animated cover.
        /// </summary>
        /// <param name="trackId">The track ID.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The preview image.</returns>
        [HttpGet("Tracks/{trackId:guid}/Cover/Preview")]
        [HttpHead("Tracks/{trackId:guid}/Cover/Preview")]
        public Task<IActionResult> GetTrackCoverPreview(Guid trackId, CancellationToken cancellationToken)
        {
            return ServeTrackPreview(trackId, AnimatedAssetKind.Cover, cancellationToken);
        }

        /// <summary>
        /// Gets the track tall animated cover.
        /// </summary>
        /// <param name="trackId">The track ID.</param>
        /// <returns>The tall cover file.</returns>
        [HttpGet("Tracks/{trackId:guid}/TallCover")]
        [HttpHead("Tracks/{trackId:guid}/TallCover")]
        public IActionResult GetTrackTallCover(Guid trackId)
        {
            return ServeTrackAsset(trackId, AnimatedAssetKind.TallCover);
        }

        /// <summary>
        /// Gets a first-frame preview of the track tall animated cover.
        /// </summary>
        /// <param name="trackId">The track ID.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The preview image.</returns>
        [HttpGet("Tracks/{trackId:guid}/TallCover/Preview")]
        [HttpHead("Tracks/{trackId:guid}/TallCover/Preview")]
        public Task<IActionResult> GetTrackTallCoverPreview(Guid trackId, CancellationToken cancellationToken)
        {
            return ServeTrackPreview(trackId, AnimatedAssetKind.TallCover, cancellationToken);
        }

        /// <summary>
        /// Gets the track vertical background.
        /// </summary>
        /// <param name="trackId">The track ID.</param>
        /// <returns>The background file.</returns>
        [HttpGet("Tracks/{trackId:guid}/VerticalBackground")]
        [HttpHead("Tracks/{trackId:guid}/VerticalBackground")]
        public IActionResult GetTrackVerticalBackground(Guid trackId)
        {
            return ServeTrackAsset(trackId, AnimatedAssetKind.VerticalBackground);
        }

        /// <summary>
        /// Gets a first-frame preview of the track vertical background.
        /// </summary>
        /// <param name="trackId">The track ID.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The preview image.</returns>
        [HttpGet("Tracks/{trackId:guid}/VerticalBackground/Preview")]
        [HttpHead("Tracks/{trackId:guid}/VerticalBackground/Preview")]
        public Task<IActionResult> GetTrackVerticalBackgroundPreview(Guid trackId, CancellationToken cancellationToken)
        {
            return ServeTrackPreview(trackId, AnimatedAssetKind.VerticalBackground, cancellationToken);
        }

        private IActionResult ServeAlbumAsset(Guid albumId, AnimatedAssetKind kind)
        {
            var album = GetAlbumOrNull(albumId);
            if (album is null)
            {
                return NotFound();
            }

            var asset = _locator.FindAlbumAsset(album, kind);
            return ServeAsset(asset, album.ContainingFolderPath);
        }

        private async Task<IActionResult> ServeAlbumPreview(Guid albumId, AnimatedAssetKind kind, CancellationToken cancellationToken)
        {
            var album = GetAlbumOrNull(albumId);
            if (album is null)
            {
                return NotFound();
            }

            var asset = _locator.FindAlbumAsset(album, kind);
            return await ServePreview(asset, cancellationToken, album.ContainingFolderPath).ConfigureAwait(false);
        }

        private IActionResult ServeTrackAsset(Guid trackId, AnimatedAssetKind kind)
        {
            var track = GetTrackOrNull(trackId);
            if (track is null)
            {
                return NotFound();
            }

            var asset = _locator.FindTrackAsset(track, kind);
            return ServeAsset(asset, TrackRoots(track));
        }

        private async Task<IActionResult> ServeTrackPreview(Guid trackId, AnimatedAssetKind kind, CancellationToken cancellationToken)
        {
            var track = GetTrackOrNull(trackId);
            if (track is null)
            {
                return NotFound();
            }

            var asset = _locator.FindTrackAsset(track, kind);
            return await ServePreview(asset, cancellationToken, TrackRoots(track)).ConfigureAwait(false);
        }

        private IActionResult ServeAsset(LocatedAsset? asset, params string?[] roots)
        {
            if (asset is null || !IsPathUnderRoots(asset.Path, roots))
            {
                return NotFound();
            }

            return ServeFile(asset.Path, asset.MimeType);
        }

        private async Task<IActionResult> ServePreview(LocatedAsset? asset, CancellationToken cancellationToken, params string?[] roots)
        {
            if (asset is null || !IsPathUnderRoots(asset.Path, roots))
            {
                return NotFound();
            }

            var sidecar = asset.SidecarPreviewPath;
            if (!string.IsNullOrEmpty(sidecar) && IsPathUnderRoots(sidecar, roots) && System.IO.File.Exists(sidecar))
            {
                return ServeFile(sidecar, MimeTypes.GetMimeType(sidecar, "image/jpeg"));
            }

            var previewPath = await _previewExtractor.GetOrCreateAsync(asset.Path, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(previewPath))
            {
                return NotFound();
            }

            return ServeFile(previewPath, "image/jpeg");
        }

        private IActionResult ServeFile(string path, string contentType)
        {
            var fileInfo = new FileInfo(path);
            if (!fileInfo.Exists)
            {
                return NotFound();
            }

            var etag = new EntityTagHeaderValue($"\"{fileInfo.LastWriteTimeUtc.Ticks:x}-{fileInfo.Length:x}\"");
            if (Request.Headers.IfNoneMatch.Count > 0)
            {
                foreach (var candidate in Request.GetTypedHeaders().IfNoneMatch)
                {
                    if (!candidate.Equals(EntityTagHeaderValue.Any) && candidate.Equals(etag))
                    {
                        return StatusCode(StatusCodes.Status304NotModified);
                    }
                }
            }

            Response.Headers.CacheControl = "private, max-age=86400";
            Response.GetTypedHeaders().LastModified = fileInfo.LastWriteTimeUtc;
            Response.GetTypedHeaders().ETag = etag;

            return PhysicalFile(fileInfo.FullName, contentType, enableRangeProcessing: true);
        }

        private MusicAlbum? GetAlbumOrNull(Guid albumId)
        {
            var userId = GetRequestUserId();
            if (userId == Guid.Empty)
            {
                return null;
            }

            return _libraryManager.GetItemById<MusicAlbum>(albumId, userId);
        }

        private Audio? GetTrackOrNull(Guid trackId)
        {
            var userId = GetRequestUserId();
            if (userId == Guid.Empty)
            {
                return null;
            }

            return _libraryManager.GetItemById<Audio>(trackId, userId);
        }

        private Guid GetRequestUserId()
        {
            foreach (var claim in User.Claims)
            {
                if (!IsUserIdClaim(claim.Type))
                {
                    continue;
                }

                if (Guid.TryParse(claim.Value, out var id) && id != Guid.Empty)
                {
                    return id;
                }
            }

            return Guid.Empty;
        }

        private static bool IsUserIdClaim(string type)
        {
            return type == "UserId"
                || type == "sub"
                || type == ClaimTypes.NameIdentifier;
        }

        private static AnimatedAssetDto ToDto(LocatedAsset? asset, string url, string previewUrl, bool includeTrackSpecific = false)
        {
            if (asset is null)
            {
                return new AnimatedAssetDto { Available = false };
            }

            return new AnimatedAssetDto
            {
                Available = true,
                Url = url,
                PreviewUrl = previewUrl,
                MimeType = asset.MimeType,
                FileName = asset.FileName,
                FileSize = asset.FileSize,
                TrackSpecific = includeTrackSpecific ? asset.TrackSpecific : null
            };
        }

        private static string?[] TrackRoots(Audio track)
        {
            var trackDir = Path.GetDirectoryName(track.Path);
            var albumDir = track.FindParent<MusicAlbum>()?.ContainingFolderPath;
            return new[] { trackDir, albumDir };
        }

        private static bool IsPathUnderRoots(string filePath, params string?[] roots)
        {
            string full;
            try
            {
                full = Path.GetFullPath(filePath);
            }
            catch (Exception)
            {
                return false;
            }

            foreach (var root in roots)
            {
                if (string.IsNullOrEmpty(root))
                {
                    continue;
                }

                string fullRoot;
                try
                {
                    fullRoot = Path.GetFullPath(root);
                }
                catch (Exception)
                {
                    continue;
                }

                var prefix = fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                if (full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
