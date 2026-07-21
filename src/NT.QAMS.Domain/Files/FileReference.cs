using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.Files;

/// <summary>
/// Immutable pointer to a stored file object. Content-addressed: the SHA-256 is
/// both the storage key component and the Part 11 integrity anchor linking
/// signed document versions to the exact bytes that were approved. Rows are
/// never updated; a new upload is a new reference.
/// </summary>
public sealed class FileReference : AggregateRoot, ITenantScoped
{
    private FileReference()
    {
        FileName = null!;
        ContentType = null!;
        Sha256 = null!;
        StorageKey = null!;
    }

    public Guid TenantId { get; set; }
    public string FileName { get; private set; }
    public string ContentType { get; private set; }
    public string Sha256 { get; private set; }
    public long SizeBytes { get; private set; }
    public string StorageKey { get; private set; }

    public static FileReference Register(
        string fileName, string contentType, string sha256, long sizeBytes, string storageKey)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new DomainException("FILE-001", "File name is required.");
        }

        if (sizeBytes <= 0)
        {
            throw new DomainException("FILE-002", "File is empty.");
        }

        return new FileReference
        {
            FileName = Path.GetFileName(fileName.Trim()),
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            Sha256 = sha256,
            SizeBytes = sizeBytes,
            StorageKey = storageKey,
        };
    }
}
