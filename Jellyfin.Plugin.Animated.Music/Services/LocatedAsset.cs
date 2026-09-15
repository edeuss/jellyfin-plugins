using System.IO;
using MediaBrowser.Model.Net;

namespace Jellyfin.Plugin.Animated.Music.Services
{
    /// <summary>
    /// Kind of animated sidecar to look up.
    /// </summary>
    public enum AnimatedAssetKind
    {
        Cover,
        TallCover,
        VerticalBackground
    }

    /// <summary>
    /// A sidecar file found next to album or track media.
    /// </summary>
    public sealed class LocatedAsset
    {
        public LocatedAsset(string path, bool trackSpecific, string? sidecarPreviewPath)
        {
            Path = path;
            TrackSpecific = trackSpecific;
            SidecarPreviewPath = sidecarPreviewPath;
            FileName = System.IO.Path.GetFileName(path);
            FileSize = new FileInfo(path).Length;
            MimeType = MimeTypes.GetMimeType(path, "application/octet-stream");
        }

        public string Path { get; }

        public string FileName { get; }

        public long FileSize { get; }

        public string MimeType { get; }

        public bool TrackSpecific { get; }

        public string? SidecarPreviewPath { get; }
    }
}
