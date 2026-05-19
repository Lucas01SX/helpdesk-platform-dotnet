using Helpdesk.Modules.Tickets.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Helpdesk.Modules.Tickets.Infrastructure.Storage;

internal sealed class LocalFileStorageService(IConfiguration configuration) : IFileStorageService
{
    private string BasePath => configuration["Storage:LocalPath"]
        ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");

    public async Task<string> SaveAsync(
        Guid ticketId, string fileName, Stream content, CancellationToken ct = default)
    {
        var ticketDir = Path.Combine(BasePath, ticketId.ToString());
        Directory.CreateDirectory(ticketDir);

        var storedName = $"{Guid.NewGuid():N}";
        var fullPath = Path.Combine(ticketDir, storedName);

        await using var fs = File.Create(fullPath);
        await content.CopyToAsync(fs, ct);

        return fullPath;
    }

    public async Task<Stream?> GetAsync(string storagePath, CancellationToken ct = default)
    {
        if (!File.Exists(storagePath))
            return null;

        return await Task.FromResult<Stream?>(File.OpenRead(storagePath));
    }
}
