using E6CarSpa.Api.Services;
using E6CarSpa.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace E6CarSpa.Api.Controllers;

[Authorize]
public class ReportsController(ReportsService reports) : ApiControllerBase
{
    [HttpGet("sales")]
    public async Task<ActionResult<SalesReportDto>> Sales([FromQuery] DateTime from, [FromQuery] DateTime to)
        => await reports.SalesAsync(from, to);

    [HttpGet("gst")]
    public async Task<ActionResult<GstSummaryDto>> Gst([FromQuery] DateTime from, [FromQuery] DateTime to)
        => await reports.GstAsync(from, to);

    [HttpGet("customer")]
    public async Task<ActionResult<CustomerHistoryDto>> Customer([FromQuery] string phone)
    {
        var result = await reports.CustomerAsync(phone);
        return result is null ? NotFound(new { message = "No customer found with that phone." }) : result;
    }
}
