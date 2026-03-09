using MarcoERP.Application.Interfaces.Accounting;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Accounting;

[Route("api/year-end-closing")]
public class YearEndClosingController : ApiControllerBase
{
    private readonly IYearEndClosingService _service;

    public YearEndClosingController(IYearEndClosingService service)
    {
        _service = service;
    }

    [HttpPost("{fiscalYearId:int}")]
    public async Task<IActionResult> GenerateClosingEntry(int fiscalYearId, CancellationToken ct)
        => FromResult(await _service.GenerateClosingEntryAsync(fiscalYearId, ct));
}
