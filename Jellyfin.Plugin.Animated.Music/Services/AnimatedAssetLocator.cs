using System;
using System.IO;
using MediaBrowser.Controller.Entities.Audio;

namespace Jellyfin.Plugin.Animated.Music.Services
{
    /// <summary>
    /// Finds animated sidecar files next to albums and tracks.
    /// </summary>
    public class AnimatedAssetLocator
    {
        private static readonly string[] AnimatedExtensions = { ".mp4", ".webm", ".gif", ".mov", ".avi" };
        private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

        /// <summary>
        /// Finds an album-level sidecar.
        /// </summary>
        /// <param name="album">The album.</param>
        /// <param name="kind">The asset kind.</param>
        /// <returns>The located file, or null.</returns>
        public LocatedAsset? FindAlbumAsset(MusicAlbum album, AnimatedAssetKind kind)
        {
            return FindInFolder(album.ContainingFolderPath, BaseName(kind), PreviewBaseName(kind), trackSpecific: false);
        }

        /// <summary>
        /// Finds a track-specific sidecar, then falls back to the album.
        /// </summary>
        /// <param name="track">The track.</param>
        /// <param name="kind">The asset kind.</param>
        /// <returns>The located file, or null.</returns>
        public LocatedAsset? FindTrackAsset(Audio track, AnimatedAssetKind kind)
        {
            var folder = Path.GetDirectoryName(track.Path);
            var stem = Path.GetFileNameWithoutExtension(track.Path);
            if (!string.IsNullOrEmpty(folder) && !string.IsNullOrEmpty(stem))
            {
                var trackAsset = FindInFolder(
                    folder,
                    $"{BaseName(kind)}-{stem}",
                    $"{PreviewBaseName(kind)}-{stem}",
                    trackSpecific: true);
                if (trackAsset is not null)
                {
                    return trackAsset;
                }
            }

            var album = track.FindParent<MusicAlbum>();
            return album is null ? null : FindAlbumAsset(album, kind);
        }

        private static string BaseName(AnimatedAssetKind kind)
        {
            return kind switch
            {
                AnimatedAssetKind.Cover => "cover-animated",
                AnimatedAssetKind.TallCover => "cover-animated-tall",
                AnimatedAssetKind.VerticalBackground => "vertical-background",
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }

        private static string PreviewBaseName(AnimatedAssetKind kind)
        {
            return kind switch
            {
                AnimatedAssetKind.Cover => "cover-animated-preview",
                AnimatedAssetKind.TallCover => "cover-animated-tall-preview",
                AnimatedAssetKind.VerticalBackground => "vertical-background-preview",
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }

        private static LocatedAsset? FindInFolder(string? folder, string baseName, string previewBaseName, bool trackSpecific)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return null;
            }

            var path = FindByBaseName(folder, baseName, AnimatedExtensions);
            if (path is null)
            {
                return null;
            }

            var sidecar = FindByBaseName(folder, previewBaseName, ImageExtensions);
            return new LocatedAsset(path, trackSpecific, sidecar);
        }

        private static string? FindByBaseName(string folder, string baseName, string[] extensions)
        {
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(folder, baseName + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            try
            {
                foreach (var file in Directory.EnumerateFiles(folder))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (!name.Equals(baseName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var extension = Path.GetExtension(file);
                    foreach (var allowed in extensions)
                    {
                        if (extension.Equals(allowed, StringComparison.OrdinalIgnoreCase))
                        {
                            return file;
                        }
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return null;
        }
    }
}
