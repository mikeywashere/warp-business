namespace WarpBusiness.Storage;

/// <summary>
/// No-op file storage service used when MinIO is not configured.
/// Storage operations will throw; bucket checks succeed silently.
/// </summary>
internal sealed class NoOpFileStorageService : IFileStorageService
{
    public Task UploadAsync(string bucket, string objectKey, Stream content, string contentType, long size = -1, CancellationToken ct = default)
        => throw new InvalidOperationException("File storage is not available: MinIO connection string is not configured. Run via Aspire or add a 'minio' connection string.");

    public Task<string> GetPresignedUrlAsync(string bucket, string objectKey, int expirySeconds = 3600, CancellationToken ct = default)
        => throw new InvalidOperationException("File storage is not available: MinIO connection string is not configured. Run via Aspire or add a 'minio' connection string.");

    public Task DeleteAsync(string bucket, string objectKey, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task EnsureBucketExistsAsync(string bucket, CancellationToken ct = default)
        => Task.CompletedTask;
}
