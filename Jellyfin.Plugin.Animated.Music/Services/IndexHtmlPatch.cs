using System;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.Animated.Music.Services
{
    /// <summary>
    /// Adds or removes the plugin script tag in index.html.
    /// </summary>
    internal static class IndexHtmlPatch
    {
        private static readonly Regex ScriptTagRegex = new(
            "<script[^>]*plugin=[\"']Animated Music[\"'][^>]*>\\s*</script>\\s*",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string Apply(string html, string scriptTag)
        {
            var stripped = Remove(html);
            if (string.IsNullOrEmpty(stripped))
            {
                return stripped;
            }

            var index = stripped.IndexOf("</body>", StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                index = stripped.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
            }

            if (index < 0)
            {
                return stripped;
            }

            return stripped.Insert(index, scriptTag + "\n");
        }

        public static string Remove(string html)
        {
            if (string.IsNullOrEmpty(html))
            {
                return string.Empty;
            }

            return ScriptTagRegex.Replace(html, string.Empty);
        }
    }
}
