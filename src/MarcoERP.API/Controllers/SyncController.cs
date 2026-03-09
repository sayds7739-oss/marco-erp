using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.DTOs.Sync;
using MarcoERP.Application.Interfaces.Sync;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers;

[ApiController]
[Authorize]
[Route("api/sync")]
public class SyncController : ApiControllerBase
{
    private readonly ISyncService _syncService;

    public SyncController(ISyncService syncService)
    {
        _syncService = syncService;
    }

    /// <summary>
    /// Pull changes from server since the client's last sync version.
    /// POST /api/sync/pull
    /// </summary>
    [HttpPost("pull")]
    public async Task<IActionResult> PullChanges(
        [FromBody] SyncPullRequestDto request, CancellationToken ct)
    {
        var result = await _syncService.PullChangesAsync(request, ct);
        return FromResult(result);
    }

    /// <summary>
    /// Push client-side changes to server.
    /// POST /api/sync/push
    /// </summary>
    [HttpPost("push")]
    public async Task<IActionResult> PushChanges(
        [FromBody] SyncPushRequestDto request, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue("userId"), out var userId))
            return BadRequest(new { success = false, errors = new[] { "سياق المستخدم غير صالح." } });

        var result = await _syncService.PushChangesAsync(request, userId, ct);
        return FromResult(result);
    }

    /// <summary>
    /// Register a device for sync.
    /// POST /api/sync/register-device
    /// </summary>
    [HttpPost("register-device")]
    public async Task<IActionResult> RegisterDevice(
        [FromBody] RegisterDeviceDto request, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue("userId"), out var userId))
            return BadRequest(new { success = false, errors = new[] { "سياق المستخدم غير صالح." } });

        var result = await _syncService.RegisterDeviceAsync(request, userId, ct);
        return FromResult(result);
    }

    /// <summary>
    /// Get sync status for a device.
    /// GET /api/sync/status?deviceId=xxx
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetSyncStatus(
        [FromQuery, Required, StringLength(255, MinimumLength = 1)] string deviceId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return BadRequest(new { success = false, errors = new[] { "DeviceId مطلوب." } });

        var result = await _syncService.GetSyncStatusAsync(deviceId, ct);
        return FromResult(result);
    }
}
