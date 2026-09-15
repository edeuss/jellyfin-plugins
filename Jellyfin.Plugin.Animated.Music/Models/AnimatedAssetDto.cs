using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Animated.Music.Models
{
    /// <summary>
    /// Describes a single animated asset and its preview.
    /// </summary>
    public class AnimatedAssetDto
    {
        /// <summary>
        /// Gets a value indicating whether the asset exists.
        /// </summary>
        public bool Available { get; init; }

        /// <summary>
        /// Gets the URL of the animated file, if available.
        /// </summary>
        public string? Url { get; init; }

        /// <summary>
        /// Gets the URL of the first-frame preview, if available.
        /// </summary>
        public string? PreviewUrl { get; init; }

        /// <summary>
        /// Gets the MIME type of the animated file.
        /// </summary>
        public string? MimeType { get; init; }

        /// <summary>
        /// Gets the on-disk file name.
        /// </summary>
        public string? FileName { get; init; }

        /// <summary>
        /// Gets the file size in bytes.
        /// </summary>
        public long? FileSize { get; init; }

        /// <summary>
        /// Gets a value indicating whether the file is track-specific rather than album-level.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? TrackSpecific { get; init; }
    }
}
