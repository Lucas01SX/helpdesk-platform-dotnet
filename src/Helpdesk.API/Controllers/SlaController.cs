using Helpdesk.Modules.SLA.Application.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.API.Controllers;

[Route("api/sla")]
[Authorize(Roles = "SupportAgent,Manager")]
public sealed class SlaController(GetSlaScoresUseCase getSlaScores) : ApiControllerBase
{
    // GET /api/sla/scores
    [HttpGet("scores")]
    public async Task<IActionResult> GetScores(CancellationToken ct)
    {
        var result = await getSlaScores.ExecuteAsync(ct);
        return Ok(Success(result));
    }
}
