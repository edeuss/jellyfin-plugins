using System;

namespace Jellyfin.Plugin.Animated.Music.Models
{
    /// <summary>
    /// Cover availability for one album or track.
    /// </summary>
    public class AnimatedLookupItemDto
    {
        /// <summary>
        /// Gets the album or track ID.
        /// </summary>
        public Guid ItemId { get; init; }

        /// <summary>
        /// Gets the square animated cover.
        /// </summary>
        public AnimatedAssetDto Cover { get; init; } = new();
    }
}
