using Helpdesk.Modules.SLA.Domain.Interfaces;
using Helpdesk.Shared.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.API.Controllers;

[ApiController]
[Route("api/sla")]
[Authorize(Roles = "SupportAgent,Manager")]
public sealed class SlaController(
    ISlaScoreRepository scoreRepository,
    IDateTimeProvider clock) : ControllerBase
{
    // GET /api/sla/scores
    [HttpGet("scores")]
    public async Task<IActionResult> GetScores(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var currentMonth = await scoreRepository.GetForMonthAsync(now.Year, now.Month, ct);
        var history = await scoreRepository.GetHistoryAsync(12, ct);

        var response = new
        {
            currentMonth = currentMonth is null
                ? new { year = now.Year, month = now.Month, score = 0, ticketsWithinSla = 0, ticketsBreached = 0 }
                : (object)new
                {
                    year = currentMonth.Year,
                    month = currentMonth.Month,
                    score = currentMonth.Score,
                    ticketsWithinSla = currentMonth.TicketsWithinSla,
                    ticketsBreached = currentMonth.TicketsBreached
                },
            history = history
                .Where(s => !(s.Year == now.Year && s.Month == now.Month))
                .Select(s => new
                {
                    year = s.Year,
                    month = s.Month,
                    score = s.Score,
                    ticketsWithinSla = s.TicketsWithinSla,
                    ticketsBreached = s.TicketsBreached
                })
        };

        return Ok(new { data = response });
    }
}
