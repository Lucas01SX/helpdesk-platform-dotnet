using FluentAssertions;
using Helpdesk.Modules.Tickets.Domain.Entities;
using Helpdesk.Modules.Tickets.Domain.Enums;
using Helpdesk.Modules.Tickets.Domain.Errors;
using Helpdesk.Modules.Tickets.Domain.Events;

namespace Helpdesk.Tests.Unit.Tickets;

public sealed class TicketAggregateTests
{
    private static readonly Guid _customerId = Guid.NewGuid();
    private static readonly Guid _agentId = Guid.NewGuid();
    private static readonly Guid _managerId = Guid.NewGuid();
    private static readonly DateTime _now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Ticket CreateTicket(
        TicketPriority priority = TicketPriority.Low,
        TicketCategory category = TicketCategory.Support)
        => Ticket.Create("Support request", "Details here", _customerId, priority, category, _now);

    private static Ticket CreateInProgressTicket(Guid? assigneeId = null)
    {
        var ticket = CreateTicket();
        ticket.Assume(assigneeId ?? _agentId, _now);
        return ticket;
    }

    // ── Group 1: Creation ────────────────────────────────────────────────────

    [Fact]
    public void Create_Should_Set_Open_Status_And_Raise_TicketCreated()
    {
        var ticket = CreateTicket();

        ticket.Status.Should().Be(TicketStatus.Open);
        ticket.CustomerId.Should().Be(_customerId);
        ticket.AssigneeId.Should().BeNull();
        ticket.DomainEvents.Should().ContainSingle(e => e is TicketCreated);
    }

    [Fact]
    public void Create_Should_Set_SlaDeadline_Plus4h_For_Low_Priority()
    {
        var ticket = CreateTicket(TicketPriority.Low);
        ticket.SlaDueAt.Should().Be(_now.AddHours(4));
    }

    [Fact]
    public void Create_Should_Set_SlaDeadline_Plus2h_For_Medium_Priority()
    {
        var ticket = CreateTicket(TicketPriority.Medium);
        ticket.SlaDueAt.Should().Be(_now.AddHours(2));
    }

    [Fact]
    public void Create_Should_Set_SlaDeadline_Plus1h_For_High_Priority()
    {
        var ticket = CreateTicket(TicketPriority.High);
        ticket.SlaDueAt.Should().Be(_now.AddHours(1));
    }

    [Fact]
    public void Create_Should_Initialize_Counters_At_Zero()
    {
        var ticket = CreateTicket();
        ticket.PriorityChangeCount.Should().Be(0);
        ticket.TransferCount.Should().Be(0);
    }

    // ── Group 2: Assume ──────────────────────────────────────────────────────

    [Fact]
    public void Assume_Should_Set_InProgress_And_Raise_Assigned_And_StatusChanged()
    {
        var ticket = CreateTicket();
        var result = ticket.Assume(_agentId, _now);

        result.IsSuccess.Should().BeTrue();
        ticket.Status.Should().Be(TicketStatus.InProgress);
        ticket.AssigneeId.Should().Be(_agentId);
        ticket.DomainEvents.Should().Contain(e => e is TicketAssigned);
        ticket.DomainEvents.Should().Contain(e => e is StatusChanged);
    }

    [Fact]
    public void Assume_Should_Fail_When_Ticket_Is_Already_InProgress()
    {
        var ticket = CreateInProgressTicket();
        var result = ticket.Assume(Guid.NewGuid(), _now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(TicketErrors.CannotAssumeInProgress.Code);
    }

    [Fact]
    public void Assume_Should_Fail_When_Ticket_Is_In_Final_State()
    {
        var ticket = CreateInProgressTicket();
        ticket.Resolve(_agentId, "Resolved.", _now);

        var result = ticket.Assume(Guid.NewGuid(), _now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(TicketErrors.CannotAssumeFromFinalState.Code);
    }

    // ── Group 3: Resolve ─────────────────────────────────────────────────────

    [Fact]
    public void Resolve_Should_Set_Resolved_And_Raise_TicketResolved_And_StatusChanged()
    {
        var ticket = CreateInProgressTicket();
        var result = ticket.Resolve(_agentId, "Issue fixed.", _now);

        result.IsSuccess.Should().BeTrue();
        ticket.Status.Should().Be(TicketStatus.Resolved);
        ticket.DomainEvents.Should().Contain(e => e is TicketResolved);
        ticket.DomainEvents.Should().Contain(e => e is StatusChanged);
    }

    [Fact]
    public void Resolve_Should_Fail_When_Ticket_Has_No_Assignee()
    {
        var ticket = CreateTicket();
        var result = ticket.Resolve(_agentId, "Fixed.", _now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(TicketErrors.CannotResolveNotInProgress.Code);
    }

    [Fact]
    public void Resolve_Should_Fail_When_Actor_Is_Not_The_Assignee()
    {
        var ticket = CreateInProgressTicket(_agentId);
        var result = ticket.Resolve(_managerId, "Fixed.", _now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(TicketErrors.ResolutionRequiresActor.Code);
    }

    [Fact]
    public void Resolve_Should_Fail_When_Description_Is_Empty()
    {
        var ticket = CreateInProgressTicket();
        var result = ticket.Resolve(_agentId, "   ", _now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(TicketErrors.ResolutionDescriptionRequired.Code);
    }

    [Fact]
    public void Resolve_Should_Fail_When_Ticket_Is_In_Final_State()
    {
        var ticket = CreateInProgressTicket();
        ticket.Resolve(_agentId, "Fixed.", _now);

        var result = ticket.Resolve(_agentId, "Fixed again.", _now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(TicketErrors.CannotResolveFromFinalState.Code);
    }

    // ── Group 4: Cancel ──────────────────────────────────────────────────────

    [Fact]
    public void CancelByCustomer_Should_Set_Cancelled_And_Raise_TicketCancelled()
    {
        var ticket = CreateTicket();
        var result = ticket.CancelByCustomer(_customerId, reason: null, _now);

        result.IsSuccess.Should().BeTrue();
        ticket.Status.Should().Be(TicketStatus.Cancelled);
        ticket.DomainEvents.Should().Contain(e => e is TicketCancelled);
    }

    [Fact]
    public void CancelByCustomer_Should_Succeed_From_InProgress_Without_Reason()
    {
        var ticket = CreateInProgressTicket();
        var result = ticket.CancelByCustomer(_customerId, reason: null, _now);

        result.IsSuccess.Should().BeTrue();
        ticket.Status.Should().Be(TicketStatus.Cancelled);
    }

    [Fact]
    public void CancelByCustomer_Should_Fail_When_Ticket_Is_Resolved()
    {
        var ticket = CreateInProgressTicket();
        ticket.Resolve(_agentId, "Fixed.", _now);

        var result = ticket.CancelByCustomer(_customerId, reason: null, _now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(TicketErrors.CannotCancelFinalState.Code);
    }

    [Fact]
    public void CancelByManager_Should_Succeed_With_Reason()
    {
        var ticket = CreateTicket();
        var result = ticket.CancelByManager(_managerId, "Duplicate ticket.", _now);

        result.IsSuccess.Should().BeTrue();
        ticket.Status.Should().Be(TicketStatus.Cancelled);
    }

    [Fact]
    public void CancelByManager_Should_Fail_Without_Reason()
    {
        var ticket = CreateTicket();
        var result = ticket.CancelByManager(_managerId, "   ", _now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(TicketErrors.ManagerCancellationRequiresReason.Code);
    }

    [Fact]
    public void AutoCancel_Should_Set_Cancelled_And_Raise_AutoCancelled()
    {
        var ticket = CreateInProgressTicket();
        var result = ticket.AutoCancel("No resolution after 10h. Please reopen with High priority.", _now);

        result.IsSuccess.Should().BeTrue();
        ticket.Status.Should().Be(TicketStatus.Cancelled);
        ticket.DomainEvents.Should().Contain(e => e is AutoCancelled);
    }

    [Fact]
    public void AutoCancel_Should_Not_Change_Status_When_Ticket_Is_Already_Resolved()
    {
        var ticket = CreateInProgressTicket();
        ticket.Resolve(_agentId, "Fixed.", _now);

        var result = ticket.AutoCancel("No resolution after 10h. Please reopen with High priority.", _now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(TicketErrors.CannotCancelFinalState.Code);
        ticket.Status.Should().Be(TicketStatus.Resolved);
    }

    // ── Group 5: Transfer ────────────────────────────────────────────────────

    [Fact]
    public void Transfer_Should_Change_Assignee_And_Extend_Sla_By_1h()
    {
        var ticket = CreateInProgressTicket(_agentId);
        var deadlineBefore = ticket.SlaDueAt;
        var newAgent = Guid.NewGuid();

        var result = ticket.Transfer(_agentId, newAgent, reason: null, _now);

        result.IsSuccess.Should().BeTrue();
        ticket.AssigneeId.Should().Be(newAgent);
        ticket.SlaDueAt.Should().Be(deadlineBefore.AddHours(1));
        ticket.DomainEvents.Should().Contain(e => e is TicketTransferred);
        ticket.DomainEvents.Should().Contain(e => e is SlaExtended);
    }

    [Fact]
    public void Transfer_Should_Fail_When_Ticket_Is_Open()
    {
        var ticket = CreateTicket();
        var result = ticket.Transfer(_agentId, Guid.NewGuid(), reason: null, _now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(TicketErrors.TransferNotAllowedInCurrentState.Code);
    }

    [Fact]
    public void Transfer_Should_Fail_When_Actor_Is_Not_The_Assignee()
    {
        var ticket = CreateInProgressTicket(_agentId);
        var result = ticket.Transfer(_managerId, Guid.NewGuid(), reason: null, _now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(TicketErrors.TransferRequiresAssignee.Code);
    }

    [Fact]
    public void Transfer_Should_Not_Change_Status()
    {
        var ticket = CreateInProgressTicket(_agentId);
        ticket.Transfer(_agentId, Guid.NewGuid(), reason: null, _now);

        ticket.Status.Should().Be(TicketStatus.InProgress);
    }

    // ── Group 6: Priority Change ─────────────────────────────────────────────

    [Fact]
    public void ChangePriority_Should_Update_Priority_And_Raise_PriorityChanged()
    {
        var ticket = CreateInProgressTicket();
        var result = ticket.ChangePriority(_agentId, TicketPriority.High, _now);

        result.IsSuccess.Should().BeTrue();
        ticket.Priority.Should().Be(TicketPriority.High);
        ticket.DomainEvents.Should().Contain(e => e is PriorityChanged);
    }

    [Fact]
    public void ChangePriority_Should_Fail_When_Ticket_Is_Open()
    {
        var ticket = CreateTicket();
        var result = ticket.ChangePriority(_agentId, TicketPriority.High, _now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(TicketErrors.PriorityChangeOnlyInProgress.Code);
    }

    [Fact]
    public void ChangePriority_Should_Fail_When_Ticket_Is_In_Final_State()
    {
        var ticket = CreateInProgressTicket();
        ticket.Resolve(_agentId, "Fixed.", _now);

        var result = ticket.ChangePriority(_agentId, TicketPriority.High, _now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(TicketErrors.PriorityChangeOnlyInProgress.Code);
    }

    [Fact]
    public void ChangePriority_Should_Fail_When_Actor_Is_Not_The_Assignee()
    {
        var ticket = CreateInProgressTicket(_agentId);
        var result = ticket.ChangePriority(_managerId, TicketPriority.High, _now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(TicketErrors.PriorityChangerMustBeAssignee.Code);
    }

    [Fact]
    public void ChangePriority_Should_Succeed_On_Third_Change()
    {
        var ticket = CreateInProgressTicket();
        ticket.ChangePriority(_agentId, TicketPriority.Medium, _now);
        ticket.ChangePriority(_agentId, TicketPriority.High, _now);
        var result = ticket.ChangePriority(_agentId, TicketPriority.Low, _now);

        result.IsSuccess.Should().BeTrue();
        ticket.PriorityChangeCount.Should().Be(3);
    }

    [Fact]
    public void ChangePriority_Should_Fail_On_Fourth_Change()
    {
        var ticket = CreateInProgressTicket();
        ticket.ChangePriority(_agentId, TicketPriority.Medium, _now);
        ticket.ChangePriority(_agentId, TicketPriority.High, _now);
        ticket.ChangePriority(_agentId, TicketPriority.Low, _now);

        var result = ticket.ChangePriority(_agentId, TicketPriority.Medium, _now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(TicketErrors.MaxPriorityChangesReached.Code);
    }

    [Fact]
    public void ChangePriority_Should_Not_Affect_SlaDueAt()
    {
        var ticket = CreateInProgressTicket();
        var deadlineBefore = ticket.SlaDueAt;

        ticket.ChangePriority(_agentId, TicketPriority.High, _now);

        ticket.SlaDueAt.Should().Be(deadlineBefore);
    }
}
