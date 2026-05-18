namespace Helpdesk.Modules.Tickets.Domain.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveAsync(Guid ticketId, string fileName, Stream content, CancellationToken ct = default);
    Task<Stream?> GetAsync(string storagePath, CancellationToken ct = default);
}
