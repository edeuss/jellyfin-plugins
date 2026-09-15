using System;

namespace Jellyfin.Plugin.Animated.Music.Models
{
    /// <summary>
    /// Animated assets available for a music album.
    /// </summary>
    public class AlbumAnimatedInfoDto
    {
        /// <summary>
        /// Gets the album ID.
        /// </summary>
        public Guid AlbumId { get; init; }

        /// <summary>
        /// Gets the square animated cover.
        /// </summary>
        public AnimatedAssetDto Cover { get; init; } = new();

        /// <summary>
        /// Gets the tall animated cover.
        /// </summary>
        public AnimatedAssetDto TallCover { get; init; } = new();

        /// <summary>
        /// Gets the vertical background video.
        /// </summary>
        public AnimatedAssetDto VerticalBackground { get; init; } = new();
    }
}
