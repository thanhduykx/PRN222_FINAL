namespace PRN222_FINAL.DAL.Repositories.Files;

public interface IFileRepository
{
    Task<long> SaveAsync(string path, Stream content, CancellationToken cancellationToken = default);
    Task WriteTextAsync(string path, string content, CancellationToken cancellationToken = default);
    Task<string> ReadTextAsync(string path, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default);
    bool Exists(string path);
    void Delete(string path);
}
