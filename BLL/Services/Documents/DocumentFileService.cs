using PRN222_FINAL.DAL.Repositories.Files;

namespace PRN222_FINAL.BLL.Services.Documents;

public interface IDocumentFileService
{
    Task<string?> ReadTextAsync(string storedPath, string uploadsRoot, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(string storedPath, string uploadsRoot, CancellationToken cancellationToken = default);
    void Delete(string storedPath, string uploadsRoot);
}

public sealed class DocumentFileService : IDocumentFileService
{
    private readonly IFileRepository _files;
    public DocumentFileService(IFileRepository files) => _files = files;

    public async Task<string?> ReadTextAsync(string storedPath, string uploadsRoot, CancellationToken cancellationToken = default)
    {
        var path = ValidatePath(storedPath, uploadsRoot);
        return _files.Exists(path) ? await _files.ReadTextAsync(path, cancellationToken) : null;
    }

    public async Task<Stream?> OpenReadAsync(string storedPath, string uploadsRoot, CancellationToken cancellationToken = default)
    {
        var path = ValidatePath(storedPath, uploadsRoot);
        return _files.Exists(path) ? await _files.OpenReadAsync(path, cancellationToken) : null;
    }

    public void Delete(string storedPath, string uploadsRoot)
    {
        var path = ValidatePath(storedPath, uploadsRoot);
        _files.Delete(path);
    }

    private static string ValidatePath(string storedPath, string uploadsRoot)
    {
        var path = Path.GetFullPath(storedPath);
        var root = Path.GetFullPath(uploadsRoot);
        if (!root.EndsWith(Path.DirectorySeparatorChar)) root += Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidOperationException("Document path is outside the configured upload directory.");
        return path;
    }
}
