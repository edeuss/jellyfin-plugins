namespace Jellyfin.Plugin.Animated.Music.Models
{
    /// <summary>
    /// Plugin availability response.
    /// </summary>
    public class PluginStatusDto
    {
        /// <summary>
        /// Gets the plugin display name.
        /// </summary>
        public string PluginName { get; init; } = "Animated Music";

        /// <summary>
        /// Gets the plugin version.
        /// </summary>
        public string Version { get; init; } = string.Empty;

        /// <summary>
        /// Gets a short status string.
        /// </summary>
        public string Status { get; init; } = "Available";

        /// <summary>
        /// Gets whether the web UI is enabled.
        /// </summary>
        public bool WebUiEnabled { get; init; }

        /// <summary>
        /// Gets how the web script was loaded.
        /// </summary>
        public string InjectionStatus { get; init; } = string.Empty;
    }
}
