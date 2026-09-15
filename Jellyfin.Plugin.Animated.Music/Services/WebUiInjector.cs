using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Animated.Music.Services
{
    /// <summary>
    /// Injects the web script into Jellyfin Web.
    /// </summary>
    public sealed class WebUiInjector : IHostedService
    {
        private readonly IApplicationPaths _applicationPaths;
        private readonly ILogger<WebUiInjector> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebUiInjector"/> class.
        /// </summary>
        /// <param name="applicationPaths">Application paths.</param>
        /// <param name="logger">Logger.</param>
        public WebUiInjector(IApplicationPaths applicationPaths, ILogger<WebUiInjector> logger)
        {
            _applicationPaths = applicationPaths;
            _logger = logger;
        }

        /// <summary>
        /// Gets how the script was injected.
        /// </summary>
        public static string InjectionStatus { get; private set; } = "Pending";

        /// <summary>
        /// Gets whether the web UI is enabled.
        /// </summary>
        /// <returns>True when enabled.</returns>
        public static bool IsEnabled()
        {
            return Plugin.Instance?.Configuration.EnableWebUi != false;
        }

        /// <summary>
        /// Script tag inserted into index.html.
        /// </summary>
        /// <returns>A script element.</returns>
        public static string ScriptTag()
        {
            var version = Plugin.Instance?.Version.ToString() ?? "2.0.0";
            return $"<script plugin=\"Animated Music\" src=\"../AnimatedMusic/web.js?v={version}\"></script>";
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!IsEnabled())
            {
                TryWriteIndexHtml(strip: true);
                TryUnregisterJavaScriptInjector();
                InjectionStatus = "Disabled";
                _logger.LogInformation("Animated Music web UI disabled");
                return Task.CompletedTask;
            }

            if (TryRegisterFileTransformation())
            {
                TryWriteIndexHtml(strip: true);
                InjectionStatus = "FileTransformation";
                _logger.LogInformation("Animated Music registered with File Transformation");
                return Task.CompletedTask;
            }

            if (TryRegisterJavaScriptInjector())
            {
                TryWriteIndexHtml(strip: true);
                InjectionStatus = "JavaScriptInjector";
                _logger.LogInformation("Animated Music registered with JavaScript Injector");
                return Task.CompletedTask;
            }

            if (TryWriteIndexHtml(strip: false))
            {
                InjectionStatus = "IndexHtml";
                _logger.LogInformation("Injected Animated Music into index.html");
                return Task.CompletedTask;
            }

            InjectionStatus = "Manual";
            _logger.LogWarning(
                "Could not inject Animated Music web.js. Add this before </body> in jellyfin-web/index.html: {Tag}",
                ScriptTag());
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private bool TryRegisterFileTransformation()
        {
            try
            {
                var assembly = FindAssembly(".FileTransformation");
                var pluginInterface = assembly?.GetType("Jellyfin.Plugin.FileTransformation.PluginInterface");
                var registerMethod = pluginInterface?.GetMethod("RegisterTransformation", BindingFlags.Static | BindingFlags.Public);
                if (registerMethod is null)
                {
                    return false;
                }

                var payload = ParseNewtonsoftObject(JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    ["id"] = Plugin.Instance.Id.ToString(),
                    ["fileNamePattern"] = "index.html",
                    ["callbackAssembly"] = typeof(IndexHtmlTransformation).Assembly.FullName ?? string.Empty,
                    ["callbackClass"] = typeof(IndexHtmlTransformation).FullName ?? string.Empty,
                    ["callbackMethod"] = nameof(IndexHtmlTransformation.Transform)
                }));
                if (payload is null)
                {
                    return false;
                }

                registerMethod.Invoke(null, [payload]);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "File Transformation registration failed");
                return false;
            }
        }

        private bool TryRegisterJavaScriptInjector()
        {
            try
            {
                var assembly = FindAssembly("Jellyfin.Plugin.JavaScriptInjector");
                var pluginInterface = assembly?.GetType("Jellyfin.Plugin.JavaScriptInjector.PluginInterface");
                var registerMethod = pluginInterface?.GetMethod("RegisterScript", BindingFlags.Static | BindingFlags.Public);
                if (registerMethod is null || Plugin.Instance is null)
                {
                    return false;
                }

                var version = Plugin.Instance.Version.ToString();
                var loader =
                    "(function(){var s=document.createElement('script');s.src='../AnimatedMusic/web.js?v="
                    + version
                    + "';document.head.appendChild(s);})();";

                var json = WriteInjectorPayload(loader, version);
                var payload = ParseNewtonsoftObject(json);
                if (payload is null)
                {
                    return false;
                }

                var result = registerMethod.Invoke(null, [payload]);
                return result is true;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "JavaScript Injector registration failed");
                return false;
            }
        }

        private void TryUnregisterJavaScriptInjector()
        {
            try
            {
                var assembly = FindAssembly("Jellyfin.Plugin.JavaScriptInjector");
                var pluginInterface = assembly?.GetType("Jellyfin.Plugin.JavaScriptInjector.PluginInterface");
                pluginInterface?.GetMethod("UnregisterAllScriptsFromPlugin", BindingFlags.Static | BindingFlags.Public)
                    ?.Invoke(null, [Plugin.Instance.Id.ToString()]);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "JavaScript Injector unregister failed");
            }
        }

        private bool TryWriteIndexHtml(bool strip)
        {
            var webPath = _applicationPaths.WebPath;
            if (string.IsNullOrEmpty(webPath))
            {
                return false;
            }

            var indexPath = Path.Combine(webPath, "index.html");
            if (!File.Exists(indexPath))
            {
                return false;
            }

            try
            {
                var original = File.ReadAllText(indexPath);
                var updated = strip
                    ? IndexHtmlPatch.Remove(original)
                    : IndexHtmlPatch.Apply(original, ScriptTag());
                if (string.Equals(original, updated, StringComparison.Ordinal))
                {
                    return !strip;
                }

                File.WriteAllText(indexPath, updated);
                return !strip;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not update {Path}", indexPath);
                return false;
            }
        }

        private static string WriteInjectorPayload(string loader, string version)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("id", Plugin.Instance.Id + "-web");
                writer.WriteString("name", "Animated Music");
                writer.WriteString("script", loader);
                writer.WriteBoolean("enabled", true);
                writer.WriteBoolean("requiresAuthentication", true);
                writer.WriteString("pluginId", Plugin.Instance.Id.ToString());
                writer.WriteString("pluginName", Plugin.Instance.Name);
                writer.WriteString("pluginVersion", version);
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static Assembly? FindAssembly(string nameFragment)
        {
            return AssemblyLoadContext.All
                .SelectMany(ctx => ctx.Assemblies)
                .FirstOrDefault(a => a.FullName?.Contains(nameFragment, StringComparison.OrdinalIgnoreCase) == true);
        }

        private static object? ParseNewtonsoftObject(string json)
        {
            foreach (var assembly in AssemblyLoadContext.All.SelectMany(ctx => ctx.Assemblies))
            {
                var type = assembly.GetType("Newtonsoft.Json.Linq.JObject");
                var parse = type?.GetMethod("Parse", [typeof(string)]);
                if (parse is null)
                {
                    continue;
                }

                return parse.Invoke(null, [json]);
            }

            return null;
        }
    }
}
