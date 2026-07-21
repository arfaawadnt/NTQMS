namespace NT.QAMS.Application.Abstractions;

/// <summary>
/// Object-storage port. Content-addressed and immutable: Save computes the
/// SHA-256 while streaming and returns it with the storage key; identical
/// content deduplicates naturally. Local-filesystem adapter today; S3/MinIO
/// adapter is a drop-in per the file-storage architecture.
/// </summary>
public interface IFileStorage
{
    Task<(string Sha256, long SizeBytes, string StorageKey)> SaveAsync(
        Guid tenantId, Stream content, CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken);
}
