using System.Collections.Generic;

namespace Jellyfin.Plugin.Animated.Music.Models
{
    /// <summary>
    /// Batch cover lookup result.
    /// </summary>
    public class AnimatedLookupResponseDto
    {
        /// <summary>
        /// Gets the lookup results.
        /// </summary>
        public IReadOnlyList<AnimatedLookupItemDto> Items { get; init; } = [];
    }
}
