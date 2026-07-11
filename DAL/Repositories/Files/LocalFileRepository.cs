using System.Text;

namespace PRN222_FINAL.DAL.Repositories.Files;

public sealed class LocalFileRepository : IFileRepository
{
    public async Task<long> SaveAsync(string path, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);
        EnsureDirectory(path);
        await using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
        await content.CopyToAsync(output, cancellationToken);
        return output.Length;
    }

    public async Task WriteTextAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        EnsureDirectory(path);
        await File.WriteAllTextAsync(path, content ?? string.Empty, Encoding.UTF8, cancellationToken);
    }

    public Task<string> ReadTextAsync(string path, CancellationToken cancellationToken = default) =>
        File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken);

    public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        return Task.FromResult(stream);
    }

    public bool Exists(string path) => File.Exists(path);
    public void Delete(string path) { if (File.Exists(path)) File.Delete(path); }

    private static void EnsureDirectory(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
    }
}
