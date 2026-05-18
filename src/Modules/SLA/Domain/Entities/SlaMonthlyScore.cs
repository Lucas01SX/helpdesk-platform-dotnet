namespace Helpdesk.Modules.SLA.Domain.Entities;

public sealed class SlaMonthlyScore
{
    private const int ScoreFloor = -100;

    public Guid Id { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }
    public int Score { get; private set; }
    public int TicketsWithinSla { get; private set; }
    public int TicketsBreached { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private SlaMonthlyScore() { }

    public static SlaMonthlyScore Create(int year, int month, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        Year = year,
        Month = month,
        Score = 0,
        TicketsWithinSla = 0,
        TicketsBreached = 0,
        UpdatedAt = now
    };

    public void RecordWithinSla(DateTime now)
    {
        Score += 100;
        TicketsWithinSla++;
        UpdatedAt = now;
    }

    public void RecordBreached(int hoursOverdue, DateTime now)
    {
        Score = Math.Max(ScoreFloor, Score - hoursOverdue * 10);
        TicketsBreached++;
        UpdatedAt = now;
    }

    public void ApplyUnassignedPenalty(DateTime now)
    {
        Score = Math.Max(ScoreFloor, Score - 5);
        UpdatedAt = now;
    }
}
