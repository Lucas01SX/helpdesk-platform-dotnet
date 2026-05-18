using Helpdesk.Modules.Tickets.Domain.Enums;
using Helpdesk.Modules.Tickets.Domain.Errors;
using Helpdesk.Modules.Tickets.Domain.Events;
using Helpdesk.Shared.Domain;
using Helpdesk.Shared.Results;

namespace Helpdesk.Modules.Tickets.Domain.Entities;

public sealed class Ticket
{
    private const int MaxPriorityChanges = 3;
    private const int SlaTransferExtensionHours = 1;

    private readonly List<IDomainEvent> _domainEvents = [];

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public TicketStatus Status { get; private set; }
    public TicketPriority Priority { get; private set; }
    public TicketCategory Category { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid? AssigneeId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime SlaDueAt { get; private set; }
    public int PriorityChangeCount { get; private set; }
    public int TransferCount { get; private set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private Ticket() { }

    public static Ticket Create(
        string title,
        string description,
        Guid customerId,
        TicketPriority priority,
        TicketCategory category,
        DateTime now)
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            Status = TicketStatus.Open,
            Priority = priority,
            Category = category,
            CustomerId = customerId,
            AssigneeId = null,
            CreatedAt = now,
            UpdatedAt = now,
            SlaDueAt = ComputeSlaDue(priority, now),
            PriorityChangeCount = 0,
            TransferCount = 0
        };

        ticket._domainEvents.Add(new TicketCreated(ticket.Id, customerId, priority, category, now));
        return ticket;
    }

    public Result Assume(Guid agentId, DateTime now)
    {
        if (Status == TicketStatus.InProgress)
            return TicketErrors.CannotAssumeInProgress;

        if (Status is TicketStatus.Resolved or TicketStatus.Cancelled)
            return TicketErrors.CannotAssumeFromFinalState;

        var from = Status;
        AssigneeId = agentId;
        Status = TicketStatus.InProgress;
        UpdatedAt = now;

        _domainEvents.Add(new TicketAssigned(Id, agentId, now));
        _domainEvents.Add(new StatusChanged(Id, from, TicketStatus.InProgress, now));
        return Result.Ok();
    }

    public Result Resolve(Guid actorId, string resolutionDescription, DateTime now)
    {
        if (Status is TicketStatus.Resolved or TicketStatus.Cancelled)
            return TicketErrors.CannotResolveFromFinalState;

        if (Status != TicketStatus.InProgress)
            return TicketErrors.CannotResolveNotInProgress;

        if (AssigneeId is null)
            return TicketErrors.ResolutionRequiresAssignee;

        if (AssigneeId != actorId)
            return TicketErrors.ResolutionRequiresActor;

        if (string.IsNullOrWhiteSpace(resolutionDescription))
            return TicketErrors.ResolutionDescriptionRequired;

        var from = Status;
        Status = TicketStatus.Resolved;
        UpdatedAt = now;

        _domainEvents.Add(new TicketResolved(Id, actorId, resolutionDescription, now));
        _domainEvents.Add(new StatusChanged(Id, from, TicketStatus.Resolved, now));
        return Result.Ok();
    }

    public Result CancelByCustomer(Guid customerId, string? reason, DateTime now)
    {
        if (Status is TicketStatus.Resolved or TicketStatus.Cancelled)
            return TicketErrors.CannotCancelFinalState;

        var from = Status;
        Status = TicketStatus.Cancelled;
        UpdatedAt = now;

        _domainEvents.Add(new TicketCancelled(Id, customerId, reason, false, now));
        _domainEvents.Add(new StatusChanged(Id, from, TicketStatus.Cancelled, now));
        return Result.Ok();
    }

    public Result CancelByManager(Guid managerId, string? reason, DateTime now)
    {
        if (Status is TicketStatus.Resolved or TicketStatus.Cancelled)
            return TicketErrors.CannotCancelFinalState;

        if (string.IsNullOrWhiteSpace(reason))
            return TicketErrors.ManagerCancellationRequiresReason;

        var from = Status;
        Status = TicketStatus.Cancelled;
        UpdatedAt = now;

        _domainEvents.Add(new TicketCancelled(Id, managerId, reason, false, now));
        _domainEvents.Add(new StatusChanged(Id, from, TicketStatus.Cancelled, now));
        return Result.Ok();
    }

    public Result AutoCancel(string systemReason, DateTime now)
    {
        var from = Status;
        Status = TicketStatus.Cancelled;
        UpdatedAt = now;

        _domainEvents.Add(new AutoCancelled(Id, systemReason, now));
        _domainEvents.Add(new StatusChanged(Id, from, TicketStatus.Cancelled, now));
        return Result.Ok();
    }

    public Result Transfer(Guid actorId, Guid newAssigneeId, string? reason, DateTime now)
    {
        if (Status != TicketStatus.InProgress)
            return TicketErrors.TransferNotAllowedInCurrentState;

        if (AssigneeId != actorId)
            return TicketErrors.TransferRequiresAssignee;

        var fromAssigneeId = AssigneeId!.Value;
        var oldDeadline = SlaDueAt;

        AssigneeId = newAssigneeId;
        SlaDueAt = SlaDueAt.AddHours(SlaTransferExtensionHours);
        TransferCount++;
        UpdatedAt = now;

        _domainEvents.Add(new TicketTransferred(Id, fromAssigneeId, newAssigneeId, reason, now));
        _domainEvents.Add(new SlaExtended(Id, oldDeadline, SlaDueAt, now));
        return Result.Ok();
    }

    public Result ChangePriority(Guid actorId, TicketPriority newPriority, DateTime now)
    {
        if (Status != TicketStatus.InProgress)
            return TicketErrors.PriorityChangeOnlyInProgress;

        if (AssigneeId != actorId)
            return TicketErrors.PriorityChangerMustBeAssignee;

        if (PriorityChangeCount >= MaxPriorityChanges)
            return TicketErrors.MaxPriorityChangesReached;

        var from = Priority;
        Priority = newPriority;
        PriorityChangeCount++;
        UpdatedAt = now;

        _domainEvents.Add(new PriorityChanged(Id, from, newPriority, actorId, now));
        return Result.Ok();
    }

    private static DateTime ComputeSlaDue(TicketPriority priority, DateTime now) => priority switch
    {
        TicketPriority.High => now.AddHours(1),
        TicketPriority.Medium => now.AddHours(2),
        _ => now.AddHours(4)
    };
}
