using System;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MarcoERP.Domain.Entities.Sync;
using MarcoERP.Persistence;

namespace MarcoERP.API.Middleware;

/// <summary>
/// Middleware that intercepts requests with an "Idempotency-Key" header.
/// If the key was already processed by the same user, the cached response is returned.
/// Otherwise, the request is processed normally and the response is cached.
/// Only applies to sync push endpoints.
/// </summary>
public class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private const int MaxCachedResponseBytes = 1_048_576; // 1 MB

    public IdempotencyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only apply to sync push endpoint
        if (!context.Request.Path.StartsWithSegments("/api/sync/push", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var idempotencyKey = context.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            await _next(context);
            return;
        }

        // Extract the authenticated user's ID for user-scoped idempotency
        int? userId = null;
        if (int.TryParse(context.User?.FindFirstValue("userId"), out var uid))
            userId = uid;

        var db = context.RequestServices.GetRequiredService<MarcoDbContext>();
        var logger = context.RequestServices.GetRequiredService<ILogger<IdempotencyMiddleware>>();

        // Check for existing processed request (user-scoped)
        var existing = await db.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey
                                   && r.UserId == userId
                                   && r.ExpiresAt > DateTime.UtcNow);

        if (existing != null && existing.ResponseBody != null)
        {
            // Replay cached response
            context.Response.StatusCode = existing.ResponseStatusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(existing.ResponseBody);
            return;
        }

        // Read request body for audit
        context.Request.EnableBuffering();
        string requestBody;
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            requestBody = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }

        // Create idempotency record (without response yet) — handle race condition
        IdempotencyRecord record;
        if (existing == null)
        {
            record = new IdempotencyRecord(idempotencyKey, context.Request.Path, requestBody, userId);
            db.IdempotencyRecords.Add(record);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Lost the race — another thread created the record; reload it
                db.Entry(record).State = EntityState.Detached;
                var raceRecord = await db.IdempotencyRecords
                    .FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey
                                           && r.UserId == userId
                                           && r.ExpiresAt > DateTime.UtcNow);

                if (raceRecord?.ResponseBody != null)
                {
                    context.Response.StatusCode = raceRecord.ResponseStatusCode;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(raceRecord.ResponseBody);
                    return;
                }

                if (raceRecord == null)
                {
                    // Record expired or something unexpected; proceed without idempotency
                    await _next(context);
                    return;
                }
            }
        }
        else
        {
            record = existing;
        }

        // Capture the response
        var originalBody = context.Response.Body;
        using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        await _next(context);

        // Read the response and cache it (with size limit)
        memoryStream.Position = 0;
        if (memoryStream.Length <= MaxCachedResponseBytes)
        {
            var responseBody = await new StreamReader(memoryStream).ReadToEndAsync();
            record.SetResponse(context.Response.StatusCode, responseBody);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to cache idempotency response for key {Key}", idempotencyKey);
            }
        }
        else
        {
            logger.LogWarning("Response too large to cache for idempotency key {Key} ({Size} bytes)",
                idempotencyKey, memoryStream.Length);
        }

        // Copy response to original stream
        memoryStream.Position = 0;
        await memoryStream.CopyToAsync(originalBody);
        context.Response.Body = originalBody;
    }
}
