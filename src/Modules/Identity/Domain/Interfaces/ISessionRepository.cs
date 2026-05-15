using Helpdesk.Modules.Identity.Domain.Entities;

namespace Helpdesk.Modules.Identity.Domain.Interfaces;

public interface ISessionRepository
{
    Task<UserSession?> FindByTokenHashAsync(string hash, CancellationToken ct = default);
    Task<IReadOnlyList<UserSession>> FindActiveFamilyAsync(Guid familyId, DateTime now, CancellationToken ct = default);
    Task<IReadOnlyList<UserSession>> FindAllActiveByUserIdAsync(Guid userId, DateTime now, CancellationToken ct = default);
    Task<int> CountActiveAsync(Guid userId, DateTime now, CancellationToken ct = default);
    Task<UserSession?> FindOldestActiveAsync(Guid userId, DateTime now, CancellationToken ct = default);
    Task AddAsync(UserSession session, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
