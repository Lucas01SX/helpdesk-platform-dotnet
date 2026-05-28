using Helpdesk.Modules.Tickets.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Helpdesk.Modules.Tickets.Infrastructure.Storage;

internal sealed class LocalFileStorageService(IConfiguration configuration) : IFileStorageService
{
    private string BasePath => configuration["Storage:LocalPath"]
        ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");

    public string BuildPath(Guid ticketId, string fileName)
    {
        // fileName intentionally ignored — stored name is always a random GUID to prevent
        // user-supplied names from reaching the filesystem.
        var ticketDir = Path.Combine(BasePath, ticketId.ToString());
        return Path.Combine(ticketDir, $"{Guid.NewGuid():N}");
    }

    public async Task SaveAsync(string storagePath, Stream content, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(storagePath)!);
        await using var fs = File.Create(storagePath);
        await content.CopyToAsync(fs, ct);
    }

    public async Task<Stream?> GetAsync(string storagePath, CancellationToken ct = default)
    {
        // Prevent path traversal: reject any path that escapes the configured base directory.
        var resolvedPath = Path.GetFullPath(storagePath);
        var resolvedBase = Path.GetFullPath(BasePath) + Path.DirectorySeparatorChar;
        if (!resolvedPath.StartsWith(resolvedBase, StringComparison.Ordinal))
            return null;

        if (!File.Exists(resolvedPath))
            return null;

        return await Task.FromResult<Stream?>(File.OpenRead(resolvedPath));
    }
}
