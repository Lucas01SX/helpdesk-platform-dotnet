using Helpdesk.Modules.Identity.Domain.Enums;

namespace Helpdesk.Modules.Identity.Application.Interfaces;

public interface IJwtTokenService
{
    string GenerateAccessToken(Guid userId, string email, UserRole role, Guid sessionId);
}
