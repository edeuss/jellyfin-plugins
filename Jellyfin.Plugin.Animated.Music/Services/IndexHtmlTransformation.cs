using System;

namespace Jellyfin.Plugin.Animated.Music.Services
{
    /// <summary>
    /// File Transformation hook for index.html.
    /// </summary>
    public static class IndexHtmlTransformation
    {
        private static readonly Type[] StringIndex = [typeof(string)];

        /// <summary>
        /// Called by File Transformation.
        /// </summary>
        /// <param name="payload">Payload with a Contents property.</param>
        /// <returns>Updated HTML.</returns>
        public static string Transform(object? payload)
        {
            var contents = GetContents(payload);
            if (!WebUiInjector.IsEnabled())
            {
                return IndexHtmlPatch.Remove(contents);
            }

            return IndexHtmlPatch.Apply(contents, WebUiInjector.ScriptTag());
        }

        private static string GetContents(object? payload)
        {
            if (payload is null)
            {
                return string.Empty;
            }

            if (payload is string html)
            {
                return html;
            }

            var type = payload.GetType();
            var property = type.GetProperty("Contents") ?? type.GetProperty("contents");
            if (property is not null)
            {
                return property.GetValue(payload) as string ?? string.Empty;
            }

            var indexer = type.GetProperty("Item", StringIndex);
            if (indexer is null)
            {
                return string.Empty;
            }

            return indexer.GetValue(payload, ["contents"]) as string
                ?? indexer.GetValue(payload, ["Contents"]) as string
                ?? string.Empty;
        }
    }
}
