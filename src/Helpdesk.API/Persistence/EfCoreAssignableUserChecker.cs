using Helpdesk.Modules.Identity.Domain.Entities;
using Helpdesk.Modules.Identity.Domain.Enums;
using Helpdesk.Shared.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.API.Persistence;

internal sealed class EfCoreAssignableUserChecker(DbContext context) : IAssignableUserChecker
{
    public Task<bool> IsAssignableUserAsync(Guid userId, CancellationToken ct = default)
        => context.Set<User>()
            .AnyAsync(u => u.Id == userId &&
                          (u.Role == UserRole.SupportAgent || u.Role == UserRole.Manager), ct);
}
