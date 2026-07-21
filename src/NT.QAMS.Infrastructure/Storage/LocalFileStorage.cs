using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using NT.QAMS.Application.Abstractions;

namespace NT.QAMS.Infrastructure.Storage;

/// <summary>
/// Content-addressed local-filesystem storage: {root}/{tenant}/{sha256}.
/// Streams to a temp file while hashing, then moves into place — identical
/// content lands on the same path (natural dedupe), and a crash never leaves a
/// partial object at a final key. S3/MinIO adapter replaces this per the
/// architecture without touching callers.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IConfiguration configuration)
    {
        _root = configuration["FileStorage:RootPath"]
            ?? Path.Combine(AppContext.BaseDirectory, "data", "files");
        Directory.CreateDirectory(_root);
    }

    public async Task<(string Sha256, long SizeBytes, string StorageKey)> SaveAsync(
        Guid tenantId, Stream content, CancellationToken cancellationToken)
    {
        var tenantDir = Path.Combine(_root, tenantId.ToString("N"));
        Directory.CreateDirectory(tenantDir);

        var tempPath = Path.Combine(tenantDir, $".upload-{Guid.NewGuid():N}.tmp");
        long size;
        string sha;

        try
        {
            await using (var target = File.Create(tempPath))
            using (var hasher = SHA256.Create())
            {
                await using var hashing = new CryptoStream(target, hasher, CryptoStreamMode.Write);
                await content.CopyToAsync(hashing, cancellationToken);
                await hashing.FlushFinalBlockAsync(cancellationToken);
                size = target.Length;
                sha = Convert.ToHexStringLower(hasher.Hash!);
            }

            var finalPath = Path.Combine(tenantDir, sha);
            if (File.Exists(finalPath))
            {
                File.Delete(tempPath); // Same content already stored — dedupe.
            }
            else
            {
                File.Move(tempPath, finalPath);
            }

            return (sha, size, $"{tenantId:N}/{sha}");
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = Path.Combine(_root, storageKey.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Stored object '{storageKey}' is missing from file storage.", path);
        }

        return Task.FromResult<Stream>(File.OpenRead(path));
    }
}
