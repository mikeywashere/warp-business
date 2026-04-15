using Minio;
using Minio.DataModel.Args;

namespace WarpBusiness.Storage;

public class MinioFileStorageService : IFileStorageService
{
    private readonly IMinioClient _minioClient;

    public MinioFileStorageService(IMinioClient minioClient)
    {
        _minioClient = minioClient;
    }

    public async Task EnsureBucketExistsAsync(string bucket, CancellationToken ct = default)
    {
        try
        {
            var args = new BucketExistsArgs().WithBucket(bucket);
            bool exists = await _minioClient.BucketExistsAsync(args, ct);

            if (!exists)
            {
                var makeArgs = new MakeBucketArgs().WithBucket(bucket);
                await _minioClient.MakeBucketAsync(makeArgs, ct);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to ensure bucket '{bucket}' exists", ex);
        }
    }

    public async Task UploadAsync(string bucket, string objectKey, Stream content, string contentType, long size = -1, CancellationToken ct = default)
    {
        try
        {
            var args = new PutObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectKey)
                .WithStreamData(content)
                .WithContentType(contentType);

            if (size >= 0)
            {
                args = args.WithObjectSize(size);
            }
            else
            {
                args = args.WithObjectSize(content.Length);
            }

            await _minioClient.PutObjectAsync(args, ct);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to upload object '{objectKey}' to bucket '{bucket}'", ex);
        }
    }

    public async Task<string> GetPresignedUrlAsync(string bucket, string objectKey, int expirySeconds = 3600, CancellationToken ct = default)
    {
        try
        {
            var args = new PresignedGetObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectKey)
                .WithExpiry(expirySeconds);

            return await _minioClient.PresignedGetObjectAsync(args);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get presigned URL for object '{objectKey}' in bucket '{bucket}'", ex);
        }
    }

    public async Task DeleteAsync(string bucket, string objectKey, CancellationToken ct = default)
    {
        try
        {
            var args = new RemoveObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectKey);

            await _minioClient.RemoveObjectAsync(args, ct);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to delete object '{objectKey}' from bucket '{bucket}'", ex);
        }
    }
}
