using MarcoERP.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult FromResult(ServiceResult result)
    {
        if (result.IsSuccess)
            return Ok(new { success = true });

        var statusCode = ClassifyError(result.Errors);
        return StatusCode(statusCode, new { success = false, errors = result.Errors });
    }

    protected IActionResult FromResult<T>(ServiceResult<T> result)
    {
        if (result.IsSuccess)
            return Ok(new { success = true, data = result.Data });

        var statusCode = ClassifyError(result.Errors);
        return StatusCode(statusCode, new { success = false, errors = result.Errors });
    }

    private static int ClassifyError(IReadOnlyList<string> errors)
    {
        // Authorization errors (from AuthorizationProxy or services)
        var authKeywords = new[] { "غير مصرح", "صلاحية", "تسجيل الدخول", "not authorized", "unauthorized" };
        if (errors.Any(e => authKeywords.Any(k => e.Contains(k, StringComparison.OrdinalIgnoreCase))))
            return 403;

        // Not found errors
        var notFoundKeywords = new[] { "غير موجود", "غير موجودة", "not found", "لم يتم العثور" };
        if (errors.Any(e => notFoundKeywords.Any(k => e.Contains(k, StringComparison.OrdinalIgnoreCase))))
            return 404;

        // Conflict errors (duplicate, already exists)
        var conflictKeywords = new[] { "موجود بالفعل", "مكرر", "already exists", "duplicate", "مستخدم بالفعل" };
        if (errors.Any(e => conflictKeywords.Any(k => e.Contains(k, StringComparison.OrdinalIgnoreCase))))
            return 409;

        // Validation / business rule errors
        return 400;
    }

    /// <summary>
    /// Validates a date range: from must be before to, max span 1095 days (3 years).
    /// Returns null if valid, or a BadRequest result if invalid.
    /// </summary>
    protected IActionResult? ValidateDateRange(DateTime from, DateTime to)
    {
        if (from >= to)
            return BadRequest(new { success = false, errors = new[] { "تاريخ البداية يجب أن يكون قبل تاريخ النهاية" } });
        if ((to - from).TotalDays > 1095)
            return BadRequest(new { success = false, errors = new[] { "الحد الأقصى لنطاق التاريخ 3 سنوات" } });
        return null;
    }
}
