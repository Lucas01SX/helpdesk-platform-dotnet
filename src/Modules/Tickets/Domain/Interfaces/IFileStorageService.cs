namespace Helpdesk.Modules.Tickets.Domain.Interfaces;

public interface IFileStorageService
{
    string BuildPath(Guid ticketId, string fileName);
    Task SaveAsync(string storagePath, Stream content, CancellationToken ct = default);
    Task<Stream?> GetAsync(string storagePath, CancellationToken ct = default);
}
