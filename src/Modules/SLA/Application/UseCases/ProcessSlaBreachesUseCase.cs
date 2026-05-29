using Helpdesk.Modules.SLA.Domain.Entities;
using Helpdesk.Modules.SLA.Domain.Interfaces;
using Helpdesk.Shared.Abstractions;
using Microsoft.Extensions.Logging;

namespace Helpdesk.Modules.SLA.Application.UseCases;

public sealed class ProcessSlaBreachesUseCase(
    ISlaTicketQueryService ticketQuery,
    ISlaTicketCommandService ticketCommand,
    ISlaScoreRepository scoreRepository,
    IManagerLoadQuery managerQuery,
    IDateTimeProvider clock,
    ILogger<ProcessSlaBreachesUseCase> logger)
{
    private const int AutoCancelHoursAfterAutoAssign = 10;
    private const string AutoCancelReason =
        "No resolution after 10 hours. Please reopen with High priority.";

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        await RunPhaseAsync("ApplyFinalScores",        () => ApplyFinalScoresAsync(now, ct));
        await RunPhaseAsync("DetectNewBreaches",        () => DetectNewBreachesAsync(now, ct));
        await RunPhaseAsync("ApplyUnassignedPenalties", () => ApplyUnassignedPenaltiesAsync(now, ct));
        await RunPhaseAsync("AutoAssignBreached",       () => AutoAssignBreachedTicketsAsync(now, ct));
        await RunPhaseAsync("AutoCancelTimedOut",       () => AutoCancelTimedOutTicketsAsync(now, ct));
    }

    private async Task RunPhaseAsync(string name, Func<Task> phase)
    {
        try { await phase(); }
        catch (Exception ex) { logger.LogError(ex, "SLA phase {Phase} failed.", name); }
    }

    private async Task ApplyFinalScoresAsync(DateTime now, CancellationToken ct)
    {
        var tickets = await ticketQuery.GetForFinalScoringAsync(ct);
        if (tickets.Count == 0) return;

        var score = await GetOrCreateCurrentScoreAsync(now, ct);

        foreach (var ticket in tickets)
        {
            if (!ticket.SlaExcluded)
            {
                if (ticket.UpdatedAt <= ticket.SlaDueAt)
                    score.RecordWithinSla(now);
                else
                {
                    var hoursOverdue = (int)Math.Ceiling((ticket.UpdatedAt - ticket.SlaDueAt).TotalHours);
                    score.RecordBreached(hoursOverdue, now);
                }
            }

            await ticketCommand.MarkSlaScoreAppliedAsync(ticket.Id, ct);
        }

        await scoreRepository.SaveChangesAsync(ct);
    }

    private async Task DetectNewBreachesAsync(DateTime now, CancellationToken ct)
    {
        var tickets = await ticketQuery.GetBreachedActiveAsync(now, ct);
        foreach (var ticket in tickets)
            await ticketCommand.MarkSlaBreachedAsync(ticket.Id, now, ct);
    }

    private async Task ApplyUnassignedPenaltiesAsync(DateTime now, CancellationToken ct)
    {
        var tickets = await ticketQuery.GetUnassignedBreachedAsync(ct);
        if (tickets.Count == 0) return;

        var score = await GetOrCreateCurrentScoreAsync(now, ct);

        foreach (var ticket in tickets)
        {
            var hoursBreached = (now - ticket.SlaDueAt).TotalHours;
            var pendingWindows = (int)(hoursBreached / 2) - ticket.SlaUnassignedPenaltyCount;
            if (pendingWindows <= 0) continue;

            for (var i = 0; i < pendingWindows; i++)
                score.ApplyUnassignedPenalty(now);

            await ticketCommand.IncrementUnassignedPenaltyAsync(ticket.Id, pendingWindows, ct);
        }

        await scoreRepository.SaveChangesAsync(ct);
    }

    private async Task AutoAssignBreachedTicketsAsync(DateTime now, CancellationToken ct)
    {
        var candidates = await ticketQuery.GetCandidatesForAutoAssignAsync(ct);
        if (candidates.Count == 0) return;

        foreach (var ticket in candidates)
        {
            var managerId = await managerQuery.GetManagerWithLowestActiveTicketCountAsync(ct);
            if (managerId is null) continue;
            await ticketCommand.AutoAssignAsync(
                ticket.Id, managerId.Value,
                "Auto-assigned: Manager with lowest active ticket count.", now, ct);
        }
    }

    private async Task AutoCancelTimedOutTicketsAsync(DateTime now, CancellationToken ct)
    {
        var cutoff = now.AddHours(-AutoCancelHoursAfterAutoAssign);
        var tickets = await ticketQuery.GetTimedOutAsync(cutoff, ct);
        foreach (var ticket in tickets)
            await ticketCommand.AutoCancelAsync(ticket.Id, AutoCancelReason, now, ct);
    }

    private async Task<SlaMonthlyScore> GetOrCreateCurrentScoreAsync(DateTime now, CancellationToken ct)
    {
        var score = await scoreRepository.GetForMonthAsync(now.Year, now.Month, ct);
        if (score is not null) return score;

        score = SlaMonthlyScore.Create(now.Year, now.Month, now);
        await scoreRepository.AddAsync(score, ct);
        return score;
    }
}
