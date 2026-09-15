using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Animated.Music.Services
{
    /// <summary>
    /// Extracts and caches the first frame of an animated file.
    /// </summary>
    public class PreviewFrameExtractor
    {
        private static readonly TimeSpan ExtractTimeout = TimeSpan.FromSeconds(20);
        private readonly IMediaEncoder _mediaEncoder;
        private readonly IApplicationPaths _applicationPaths;
        private readonly ILogger<PreviewFrameExtractor> _logger;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="PreviewFrameExtractor"/> class.
        /// </summary>
        /// <param name="mediaEncoder">The media encoder.</param>
        /// <param name="applicationPaths">Application paths.</param>
        /// <param name="logger">The logger.</param>
        public PreviewFrameExtractor(
            IMediaEncoder mediaEncoder,
            IApplicationPaths applicationPaths,
            ILogger<PreviewFrameExtractor> logger)
        {
            _mediaEncoder = mediaEncoder;
            _applicationPaths = applicationPaths;
            _logger = logger;
        }

        /// <summary>
        /// Returns a JPEG path for the first frame of <paramref name="sourcePath"/>.
        /// </summary>
        /// <param name="sourcePath">The animated file.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The cached JPEG path, or null on failure.</returns>
        public async Task<string?> GetOrCreateAsync(string sourcePath, CancellationToken cancellationToken)
        {
            FileInfo source;
            try
            {
                source = new FileInfo(sourcePath);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Invalid source path for preview extraction");
                return null;
            }

            if (!source.Exists)
            {
                return null;
            }

            var cacheDir = Path.Combine(_applicationPaths.CachePath, "animated-music", "previews");
            Directory.CreateDirectory(cacheDir);

            var key = CacheKey(source);
            var outputPath = Path.Combine(cacheDir, key + ".jpg");
            if (File.Exists(outputPath) && new FileInfo(outputPath).Length > 0)
            {
                return outputPath;
            }

            var gate = _locks.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (File.Exists(outputPath) && new FileInfo(outputPath).Length > 0)
                {
                    return outputPath;
                }

                return await ExtractAsync(source.FullName, outputPath, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task<string?> ExtractAsync(string inputPath, string outputPath, CancellationToken cancellationToken)
        {
            var encoderPath = _mediaEncoder.EncoderPath;
            if (string.IsNullOrEmpty(encoderPath)
                || (Path.IsPathRooted(encoderPath) && !File.Exists(encoderPath)))
            {
                _logger.LogWarning("FFmpeg is not available; cannot extract preview from {Path}", inputPath);
                return null;
            }

            var tempPath = outputPath + ".tmp";
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = encoderPath,
                    RedirectStandardError = true,
                    RedirectStandardOutput = false,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                startInfo.ArgumentList.Add("-hide_banner");
                startInfo.ArgumentList.Add("-loglevel");
                startInfo.ArgumentList.Add("error");
                startInfo.ArgumentList.Add("-y");
                startInfo.ArgumentList.Add("-i");
                startInfo.ArgumentList.Add(inputPath);
                startInfo.ArgumentList.Add("-an");
                startInfo.ArgumentList.Add("-frames:v");
                startInfo.ArgumentList.Add("1");
                startInfo.ArgumentList.Add("-q:v");
                startInfo.ArgumentList.Add("2");
                startInfo.ArgumentList.Add(tempPath);

                using var process = new Process { StartInfo = startInfo };
                if (!process.Start())
                {
                    _logger.LogWarning("Failed to start FFmpeg for {Path}", inputPath);
                    return null;
                }

                var stderrTask = process.StandardError.ReadToEndAsync();
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(ExtractTimeout);

                try
                {
                    await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(entireProcessTree: true);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error stopping FFmpeg after timeout");
                    }

                    _logger.LogWarning("FFmpeg timed out extracting preview from {Path}", inputPath);
                    return null;
                }

                var stderr = await stderrTask.ConfigureAwait(false);
                if (process.ExitCode != 0 || !File.Exists(tempPath) || new FileInfo(tempPath).Length == 0)
                {
                    _logger.LogWarning("FFmpeg failed extracting preview from {Path}: {Error}", inputPath, stderr);
                    return null;
                }

                File.Move(tempPath, outputPath, overwrite: true);
                return outputPath;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting preview from {Path}", inputPath);
                return null;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch (IOException)
                {
                }
            }
        }

        private static string CacheKey(FileInfo source)
        {
            var raw = $"{source.FullName}|{source.LastWriteTimeUtc.Ticks}|{source.Length}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
