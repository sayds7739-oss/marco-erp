using System.Net;
using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace MarcoERP.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var traceId = context.TraceIdentifier;
            _logger.LogError(ex, "Unhandled exception [TraceId: {TraceId}] {Path}",
                traceId, context.Request.Path);
            await HandleExceptionAsync(context, ex, traceId);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception, string traceId)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, message) = exception switch
        {
            ValidationException vex => (
                (int)HttpStatusCode.BadRequest,
                string.Join("; ", vex.Errors.Select(e => e.ErrorMessage))
            ),
            UnauthorizedAccessException => (
                (int)HttpStatusCode.Forbidden,
                "غير مصرح بالوصول."
            ),
            ArgumentNullException anex => (
                (int)HttpStatusCode.BadRequest,
                $"حقل مطلوب مفقود: {anex.ParamName}"
            ),
            ArgumentException aex => (
                (int)HttpStatusCode.BadRequest,
                "قيمة غير صالحة في الطلب."
            ),
            DbUpdateConcurrencyException => (
                (int)HttpStatusCode.Conflict,
                "تم تعديل السجل بواسطة مستخدم آخر. يرجى إعادة المحاولة."
            ),
            DbUpdateException => (
                (int)HttpStatusCode.Conflict,
                "خطأ في قاعدة البيانات — قد يكون السجل قد تم تعديله."
            ),
            OperationCanceledException => (
                408,
                "تم إلغاء الطلب."
            ),
            _ => (
                (int)HttpStatusCode.InternalServerError,
                "حدث خطأ غير متوقع. يرجى المحاولة مرة أخرى."
            )
        };

        context.Response.StatusCode = statusCode;

        var response = new
        {
            success = false,
            errors = new[] { message },
            traceId
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await context.Response.WriteAsync(json);
    }
}
