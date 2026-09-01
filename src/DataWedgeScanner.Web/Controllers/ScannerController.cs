using DataWedgeScanner.Web.Contracts;
using DataWedgeScanner.Web.Scanner;
using Microsoft.AspNetCore.Mvc;

namespace DataWedgeScanner.Web.Controllers;

/// <summary>REST view of the TCP scanner listener's health for non-browser clients.</summary>
[ApiController]
[Route("api/scanner")]
public sealed class ScannerController : ControllerBase
{
    private readonly ScannerListenerStatus _status;

    public ScannerController(ScannerListenerStatus status)
    {
        _status = status;
    }

    [HttpGet("status")]
    public ActionResult<ScannerStatusResponse> GetStatus()
    {
        return Ok(ScannerStatusResponse.FromStatus(_status));
    }
}
