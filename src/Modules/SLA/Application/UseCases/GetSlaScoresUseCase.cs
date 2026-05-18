using Helpdesk.Modules.SLA.Domain.Interfaces;
using Helpdesk.Shared.Abstractions;

namespace Helpdesk.Modules.SLA.Application.UseCases;

public sealed class GetSlaScoresUseCase(ISlaScoreRepository scoreRepository, IDateTimeProvider clock)
{
    public async Task<SlaScoresResponse> ExecuteAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var currentMonth = await scoreRepository.GetForMonthAsync(now.Year, now.Month, ct);
        var history = await scoreRepository.GetHistoryAsync(12, ct);

        var current = currentMonth is null
            ? new MonthlyScoreDto(now.Year, now.Month, 0, 0, 0)
            : new MonthlyScoreDto(
                currentMonth.Year,
                currentMonth.Month,
                currentMonth.Score,
                currentMonth.TicketsWithinSla,
                currentMonth.TicketsBreached);

        var historicalItems = history
            .Where(s => !(s.Year == now.Year && s.Month == now.Month))
            .Select(s => new MonthlyScoreDto(s.Year, s.Month, s.Score, s.TicketsWithinSla, s.TicketsBreached))
            .ToList();

        return new SlaScoresResponse(current, historicalItems);
    }
}

public sealed record MonthlyScoreDto(
    int Year,
    int Month,
    int Score,
    int TicketsWithinSla,
    int TicketsBreached);

public sealed record SlaScoresResponse(
    MonthlyScoreDto CurrentMonth,
    IReadOnlyList<MonthlyScoreDto> History);
