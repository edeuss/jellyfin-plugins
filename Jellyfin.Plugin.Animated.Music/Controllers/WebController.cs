using System;
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Animated.Music.Controllers
{
    /// <summary>
    /// Controller for serving web components.
    /// </summary>
    [ApiController]
    [Route("Plugins/AnimatedMusic/Web")]
    public class WebController : ControllerBase
    {
        private readonly ILogger<WebController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebController"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public WebController(ILogger<WebController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Serves the animated cover component JavaScript file.
        /// </summary>
        /// <returns>The JavaScript file.</returns>
        [HttpGet("AnimatedCoverComponent.js")]
        public IActionResult GetAnimatedCoverComponent()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "Jellyfin.Plugin.Animated.Music.Web.AnimatedCoverComponent.js";
                
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    _logger.LogError("Could not find embedded resource: {ResourceName}", resourceName);
                    return NotFound("JavaScript file not found");
                }
                
                using var reader = new StreamReader(stream);
                var scriptContent = reader.ReadToEnd();
                
                return Content(scriptContent, "application/javascript");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error serving animated cover component");
                return StatusCode(500, "Internal server error");
            }
        }


    }
} 