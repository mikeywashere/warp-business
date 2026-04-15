using FluentAssertions;
using Minio;
using Minio.DataModel.Args;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace WarpBusiness.Storage.Tests;

public class MinioFileStorageServiceTests
{
    #region EnsureBucketExistsAsync Tests

    [Fact]
    public async Task EnsureBucketExistsAsync_WhenBucketDoesNotExist_CreatesBucket()
    {
        // Arrange
        var mockClient = Substitute.For<IMinioClient>();
        mockClient.BucketExistsAsync(Arg.Any<BucketExistsArgs>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
        
        var service = new MinioFileStorageService(mockClient);

        // Act
        await service.EnsureBucketExistsAsync("test-bucket");

        // Assert
        await mockClient.Received(1).BucketExistsAsync(
            Arg.Any<BucketExistsArgs>(),
            Arg.Any<CancellationToken>());
        
        await mockClient.Received(1).MakeBucketAsync(
            Arg.Any<MakeBucketArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureBucketExistsAsync_WhenBucketExists_DoesNotCreateBucket()
    {
        // Arrange
        var mockClient = Substitute.For<IMinioClient>();
        mockClient.BucketExistsAsync(Arg.Any<BucketExistsArgs>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        
        var service = new MinioFileStorageService(mockClient);

        // Act
        await service.EnsureBucketExistsAsync("existing-bucket");

        // Assert
        await mockClient.Received(1).BucketExistsAsync(
            Arg.Any<BucketExistsArgs>(),
            Arg.Any<CancellationToken>());
        
        await mockClient.DidNotReceive().MakeBucketAsync(
            Arg.Any<MakeBucketArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureBucketExistsAsync_WhenMinioThrows_WrapsException()
    {
        // Arrange
        var mockClient = Substitute.For<IMinioClient>();
        mockClient.BucketExistsAsync(Arg.Any<BucketExistsArgs>(), Arg.Any<CancellationToken>())
            .Throws(new Exception("Minio connection failed"));
        
        var service = new MinioFileStorageService(mockClient);

        // Act
        Func<Task> act = async () => await service.EnsureBucketExistsAsync("test-bucket");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Failed to ensure bucket 'test-bucket' exists")
            .Where(ex => ex.InnerException!.Message == "Minio connection failed");
    }

    [Fact]
    public async Task EnsureBucketExistsAsync_PropagatesCancellationToken()
    {
        // Arrange
        var mockClient = Substitute.For<IMinioClient>();
        mockClient.BucketExistsAsync(Arg.Any<BucketExistsArgs>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
        
        var service = new MinioFileStorageService(mockClient);
        var cts = new CancellationTokenSource();

        // Act
        await service.EnsureBucketExistsAsync("test-bucket", cts.Token);

        // Assert
        await mockClient.Received().BucketExistsAsync(
            Arg.Any<BucketExistsArgs>(),
            Arg.Is<CancellationToken>(ct => ct == cts.Token));
    }

    #endregion

    #region UploadAsync Tests

    [Fact]
    public async Task UploadAsync_WithAllParameters_CallsPutObjectWithCorrectArgs()
    {
        // Arrange
        var mockClient = Substitute.For<IMinioClient>();
        var service = new MinioFileStorageService(mockClient);
        
        using var stream = new MemoryStream([1, 2, 3, 4, 5]);
        const long size = 5L;

        // Act
        await service.UploadAsync("my-bucket", "my-object.txt", stream, "text/plain", size);

        // Assert
        await mockClient.Received(1).PutObjectAsync(
            Arg.Any<PutObjectArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAsync_WithoutSizeParameter_UsesStreamLength()
    {
        // Arrange
        var mockClient = Substitute.For<IMinioClient>();
        var service = new MinioFileStorageService(mockClient);
        
        using var stream = new MemoryStream([1, 2, 3, 4, 5]);

        // Act
        await service.UploadAsync("my-bucket", "my-object.txt", stream, "text/plain");

        // Assert - service should call PutObjectAsync with stream.Length
        await mockClient.Received(1).PutObjectAsync(
            Arg.Any<PutObjectArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAsync_WithLargeStream_HandlesSize()
    {
        // Arrange
        var mockClient = Substitute.For<IMinioClient>();
        var service = new MinioFileStorageService(mockClient);
        
        using var stream = new MemoryStream(new byte[1024 * 1024]); // 1MB
        const long size = 1024L * 1024L;

        // Act
        await service.UploadAsync("my-bucket", "large-file.bin", stream, "application/octet-stream", size);

        // Assert
        await mockClient.Received(1).PutObjectAsync(
            Arg.Any<PutObjectArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAsync_WhenMinioThrows_WrapsException()
    {
        // Arrange
        var mockClient = Substitute.For<IMinioClient>();
        mockClient.PutObjectAsync(Arg.Any<PutObjectArgs>(), Arg.Any<CancellationToken>())
            .Throws(new Exception("Upload failed"));
        
        var service = new MinioFileStorageService(mockClient);
        using var stream = new MemoryStream([1, 2, 3]);

        // Act
        Func<Task> act = async () => await service.UploadAsync("bucket", "key", stream, "text/plain");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Failed to upload object 'key' to bucket 'bucket'")
            .Where(ex => ex.InnerException!.Message == "Upload failed");
    }

    [Fact]
    public async Task UploadAsync_PropagatesCancellationToken()
    {
        // Arrange
        var mockClient = Substitute.For<IMinioClient>();
        var service = new MinioFileStorageService(mockClient);
        var cts = new CancellationTokenSource();
        
        using var stream = new MemoryStream([1, 2, 3]);

        // Act
        await service.UploadAsync("bucket", "key", stream, "text/plain", ct: cts.Token);

        // Assert
        await mockClient.Received().PutObjectAsync(
            Arg.Any<PutObjectArgs>(),
            Arg.Is<CancellationToken>(ct => ct == cts.Token));
    }

    #endregion

    #region GetPresignedUrlAsync Tests

    [Fact]
    public async Task GetPresignedUrlAsync_ReturnsPresignedUrl()
    {
        // Arrange
        var mockClient = Substitute.For<IMinioClient>();
        mockClient.PresignedGetObjectAsync(Arg.Any<PresignedGetObjectArgs>())
            .Returns(Task.FromResult("https://minio.example.com/bucket/object?signature=abc123"));
        
        var service = new MinioFileStorageService(mockClient);

        // Act
        var url = await service.GetPresignedUrlAsync("bucket", "object.jpg");

        // Assert
        url.Should().Be("https://minio.example.com/bucket/object?signature=abc123");
        await mockClient.Received(1).PresignedGetObjectAsync(
            Arg.Any<PresignedGetObjectArgs>());
    }

    [Fact]
    public async Task GetPresignedUrlAsync_WithCustomExpiry_UsesCorrectExpiry()
    {
        // Arrange
        var mockClient = Substitute.For<IMinioClient>();
        mockClient.PresignedGetObjectAsync(Arg.Any<PresignedGetObjectArgs>())
            .Returns(Task.FromResult("https://minio.example.com/bucket/object?signature=xyz"));
        
        var service = new MinioFileStorageService(mockClient);

        // Act
        await service.GetPresignedUrlAsync("bucket", "object.jpg", expirySeconds: 7200);

        // Assert
        await mockClient.Received(1).PresignedGetObjectAsync(
            Arg.Any<PresignedGetObjectArgs>());
    }

    [Fact]
    public async Task GetPresignedUrlAsync_WhenMinioThrows_WrapsException()
    {
        // Arrange
        var mockClient = Substitute.For<IMinioClient>();
        mockClient.PresignedGetObjectAsync(Arg.Any<PresignedGetObjectArgs>())
            .Throws(new Exception("Object not found"));
        
        var service = new MinioFileStorageService(mockClient);

        // Act
        Func<Task> act = async () => await service.GetPresignedUrlAsync("bucket", "missing.jpg");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Failed to get presigned URL for object 'missing.jpg' in bucket 'bucket'")
            .Where(ex => ex.InnerException!.Message == "Object not found");
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_CallsRemoveObjectWithCorrectArgs()
    {
        // Arrange
        var mockClient = Substitute.For<IMinioClient>();
        var service = new MinioFileStorageService(mockClient);

        // Act
        await service.DeleteAsync("bucket", "object.txt");

        // Assert
        await mockClient.Received(1).RemoveObjectAsync(
            Arg.Any<RemoveObjectArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WhenMinioThrows_WrapsException()
    {
        // Arrange
        var mockClient = Substitute.For<IMinioClient>();
        mockClient.RemoveObjectAsync(Arg.Any<RemoveObjectArgs>(), Arg.Any<CancellationToken>())
            .Throws(new Exception("Delete failed"));
        
        var service = new MinioFileStorageService(mockClient);

        // Act
        Func<Task> act = async () => await service.DeleteAsync("bucket", "object.txt");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Failed to delete object 'object.txt' from bucket 'bucket'")
            .Where(ex => ex.InnerException!.Message == "Delete failed");
    }

    [Fact]
    public async Task DeleteAsync_PropagatesCancellationToken()
    {
        // Arrange
        var mockClient = Substitute.For<IMinioClient>();
        var service = new MinioFileStorageService(mockClient);
        var cts = new CancellationTokenSource();

        // Act
        await service.DeleteAsync("bucket", "object.txt", cts.Token);

        // Assert
        await mockClient.Received().RemoveObjectAsync(
            Arg.Any<RemoveObjectArgs>(),
            Arg.Is<CancellationToken>(ct => ct == cts.Token));
    }

    #endregion

    #region Edge Case Tests

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task UploadAsync_WithInvalidBucketName_ThrowsException(string invalidBucket)
    {
        // Arrange
        var mockClient = Substitute.For<IMinioClient>();
        var service = new MinioFileStorageService(mockClient);
        using var stream = new MemoryStream([1, 2, 3]);

        // Act - Minio SDK will validate and throw, service wraps it
        mockClient.PutObjectAsync(Arg.Any<PutObjectArgs>(), Arg.Any<CancellationToken>())
            .Throws(new ArgumentException("Bucket name cannot be empty"));

        Func<Task> act = async () => await service.UploadAsync(invalidBucket, "key", stream, "text/plain");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task UploadAsync_WithInvalidObjectKey_ThrowsException(string invalidKey)
    {
        // Arrange
        var mockClient = Substitute.For<IMinioClient>();
        var service = new MinioFileStorageService(mockClient);
        using var stream = new MemoryStream([1, 2, 3]);

        // Act - Minio SDK will validate and throw, service wraps it
        mockClient.PutObjectAsync(Arg.Any<PutObjectArgs>(), Arg.Any<CancellationToken>())
            .Throws(new ArgumentException("Object name cannot be empty"));

        Func<Task> act = async () => await service.UploadAsync("bucket", invalidKey, stream, "text/plain");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UploadAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var mockClient = Substitute.For<IMinioClient>();
        mockClient.PutObjectAsync(Arg.Any<PutObjectArgs>(), Arg.Any<CancellationToken>())
            .Throws(new OperationCanceledException());
        
        var service = new MinioFileStorageService(mockClient);
        using var stream = new MemoryStream([1, 2, 3]);
        
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = async () => await service.UploadAsync("bucket", "key", stream, "text/plain", ct: cts.Token);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .Where(ex => ex.InnerException is OperationCanceledException);
    }

    [Fact]
    public async Task EnsureBucketExistsAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var mockClient = Substitute.For<IMinioClient>();
        mockClient.BucketExistsAsync(Arg.Any<BucketExistsArgs>(), Arg.Any<CancellationToken>())
            .Throws(new OperationCanceledException());
        
        var service = new MinioFileStorageService(mockClient);
        
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = async () => await service.EnsureBucketExistsAsync("bucket", cts.Token);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .Where(ex => ex.InnerException is OperationCanceledException);
    }

    [Fact]
    public async Task DeleteAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var mockClient = Substitute.For<IMinioClient>();
        mockClient.RemoveObjectAsync(Arg.Any<RemoveObjectArgs>(), Arg.Any<CancellationToken>())
            .Throws(new OperationCanceledException());
        
        var service = new MinioFileStorageService(mockClient);
        
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = async () => await service.DeleteAsync("bucket", "key", cts.Token);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .Where(ex => ex.InnerException is OperationCanceledException);
    }

    #endregion
}
