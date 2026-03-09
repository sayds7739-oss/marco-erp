using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace MarcoERP.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Simple health check endpoint - returns 200 OK if API is running
    /// Use this to verify network connectivity from mobile app
    /// </summary>
    [HttpGet]
    public IActionResult Get()
    {
        _logger.LogInformation("Health check requested from {IpAddress}", 
            HttpContext.Connection.RemoteIpAddress);

        return Ok(new
        {
            success = true,
            message = "MarcoERP API is running",
            timestamp = DateTime.UtcNow,
            version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0"
        });
    }

    /// <summary>
    /// Ping endpoint for quick connectivity tests
    /// </summary>
    [HttpGet("ping")]
    public IActionResult Ping()
    {
        _logger.LogInformation("Ping requested from {IpAddress}", 
            HttpContext.Connection.RemoteIpAddress);
            
        return Ok(new { success = true, message = "pong" });
    }

}
