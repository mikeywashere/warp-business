namespace WarpBusiness.Storage;

public interface IFileStorageService
{
    Task UploadAsync(string bucket, string objectKey, Stream content, string contentType, long size = -1, CancellationToken ct = default);
    Task<string> GetPresignedUrlAsync(string bucket, string objectKey, int expirySeconds = 3600, CancellationToken ct = default);
    Task DeleteAsync(string bucket, string objectKey, CancellationToken ct = default);
    Task EnsureBucketExistsAsync(string bucket, CancellationToken ct = default);
}
