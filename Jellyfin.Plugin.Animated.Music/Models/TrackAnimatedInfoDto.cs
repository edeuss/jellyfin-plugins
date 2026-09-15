using System;

namespace Jellyfin.Plugin.Animated.Music.Models
{
    /// <summary>
    /// Animated assets available for a music track.
    /// </summary>
    public class TrackAnimatedInfoDto
    {
        /// <summary>
        /// Gets the track ID.
        /// </summary>
        public Guid TrackId { get; init; }

        /// <summary>
        /// Gets the parent album ID, if known.
        /// </summary>
        public Guid? AlbumId { get; init; }

        /// <summary>
        /// Gets the track file name without extension.
        /// </summary>
        public string? TrackFileName { get; init; }

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
