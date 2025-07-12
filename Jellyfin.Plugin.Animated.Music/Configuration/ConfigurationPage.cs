using System;
using System.IO;
using System.Reflection;
using MediaBrowser.Controller.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Animated.Music.Configuration
{
    /// <summary>
    /// Configuration page controller for the animated music plugin.
    /// </summary>
    [ApiController]
    [Route("Plugins/AnimatedMusic")]
    public class ConfigurationPage : ControllerBase
    {
        private readonly IServerConfigurationManager _serverConfigurationManager;
        private readonly ILogger<ConfigurationPage> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationPage"/> class.
        /// </summary>
        /// <param name="serverConfigurationManager">The server configuration manager.</param>
        /// <param name="logger">The logger.</param>
        public ConfigurationPage(IServerConfigurationManager serverConfigurationManager, ILogger<ConfigurationPage> logger)
        {
            _serverConfigurationManager = serverConfigurationManager;
            _logger = logger;
        }

        /// <summary>
        /// Gets the plugin configuration.
        /// </summary>
        /// <returns>The plugin configuration.</returns>
        [HttpGet("Configuration")]
        public ActionResult<PluginConfiguration> GetConfiguration()
        {
            return Ok(GetPluginConfiguration());
        }

        /// <summary>
        /// Updates the plugin configuration.
        /// </summary>
        /// <param name="configuration">The new configuration.</param>
        /// <returns>The updated configuration.</returns>
        [HttpPost("Configuration")]
        public ActionResult<PluginConfiguration> UpdateConfiguration([FromBody] PluginConfiguration configuration)
        {
            if (configuration == null)
            {
                return BadRequest("Configuration cannot be null");
            }

            var currentConfig = GetPluginConfiguration();
            
            // Update configuration
            currentConfig.EnableAnimatedCovers = configuration.EnableAnimatedCovers;
            currentConfig.EnableVerticalBackgrounds = configuration.EnableVerticalBackgrounds;
            currentConfig.SupportedAnimatedFormats = configuration.SupportedAnimatedFormats;
            currentConfig.AnimatedCoverFileName = configuration.AnimatedCoverFileName;
            currentConfig.VerticalBackgroundFileName = configuration.VerticalBackgroundFileName;
            currentConfig.MaxFileSizeMB = configuration.MaxFileSizeMB;

            // Save configuration
            _serverConfigurationManager.SaveConfiguration("animatedmusic", currentConfig);
            
            _logger.LogInformation("Animated Music plugin configuration updated");
            
            return Ok(currentConfig);
        }

        private PluginConfiguration GetPluginConfiguration()
        {
            return _serverConfigurationManager.GetConfiguration("animatedmusic") as PluginConfiguration 
                   ?? new PluginConfiguration();
        }

        /// <summary>
        /// Serves the configuration page HTML.
        /// </summary>
        /// <returns>The configuration page HTML.</returns>
        [HttpGet("ConfigurationPage")]
        public IActionResult GetConfigurationPage()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "Jellyfin.Plugin.Animated.Music.Configuration.configPage.html";
                
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    _logger.LogError("Could not find embedded resource: {ResourceName}", resourceName);
                    return NotFound("Configuration page not found");
                }
                
                using var reader = new StreamReader(stream);
                var htmlContent = reader.ReadToEnd();
                
                return Content(htmlContent, "text/html");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error serving configuration page");
                return StatusCode(500, "Internal server error");
            }
        }
    }
} 