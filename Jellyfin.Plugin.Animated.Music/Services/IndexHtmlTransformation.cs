using System;
using System.IO;
using System.Reflection;

namespace Jellyfin.Plugin.Animated.Music.Services
{
    /// <summary>
    /// File Transformation hook for index.html.
    /// </summary>
    public static class IndexHtmlTransformation
    {
        private static readonly Type[] StringIndex = [typeof(string)];
        private static readonly BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

        /// <summary>
        /// Called by File Transformation.
        /// </summary>
        /// <param name="payload">Payload with a contents field.</param>
        /// <returns>Updated HTML.</returns>
        public static string Transform(object? payload)
        {
            var contents = GetContents(payload);
            if (!IndexHtmlPatch.LooksLikeIndex(contents))
            {
                contents = ReadIndexFromDisk();
            }

            if (!IndexHtmlPatch.LooksLikeIndex(contents))
            {
                throw new InvalidOperationException("Animated Music will not patch an empty index.html");
            }

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

            return ReadMember(payload, "contents")
                ?? ReadMember(payload, "Contents")
                ?? string.Empty;
        }

        private static string? ReadMember(object payload, string name)
        {
            var type = payload.GetType();
            foreach (var property in type.GetProperties(PublicInstance))
            {
                if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                return ToPlainString(property.GetValue(payload));
            }

            var indexer = type.GetProperty("Item", StringIndex);
            if (indexer is null)
            {
                return null;
            }

            return ToPlainString(indexer.GetValue(payload, [name]));
        }

        private static string? ToPlainString(object? value)
        {
            switch (value)
            {
                case null:
                    return null;
                case string text:
                    return text;
            }

            var type = value.GetType();
            if (string.Equals(type.Name, "JValue", StringComparison.Ordinal)
                || string.Equals(type.BaseType?.Name, "JToken", StringComparison.Ordinal)
                || string.Equals(type.Name, "JToken", StringComparison.Ordinal))
            {
                var token = value.ToString();
                return string.IsNullOrEmpty(token) ? null : token;
            }

            return Convert.ToString(value);
        }

        private static string ReadIndexFromDisk()
        {
            var webPath = WebUiInjector.WebPath;
            if (string.IsNullOrEmpty(webPath))
            {
                return string.Empty;
            }

            var indexPath = Path.Combine(webPath, "index.html");
            if (!File.Exists(indexPath))
            {
                return string.Empty;
            }

            try
            {
                return File.ReadAllText(indexPath);
            }
            catch (IOException)
            {
                return string.Empty;
            }
        }
    }
}
