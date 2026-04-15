using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;

namespace WarpBusiness.Storage;

public static class StorageServiceExtensions
{
    public static IServiceCollection AddMinioStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IMinioClient>(sp =>
        {
            var connectionString = configuration.GetConnectionString("minio")
                ?? throw new InvalidOperationException("MinIO connection string 'minio' is not configured");

            // Aspire injects MinIO as a plain endpoint URL (e.g. "http://localhost:9000").
            // Credentials are not embedded — read separately with dev-safe defaults.
            string endpoint;
            bool useSSL;

            if (Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
            {
                endpoint = uri.Port > 0 ? $"{uri.Host}:{uri.Port}" : uri.Host;
                useSSL = uri.Scheme == "https";
            }
            else
            {
                // Bare host:port fallback (no scheme)
                endpoint = connectionString;
                useSSL = false;
            }

            var accessKey = configuration["Minio:AccessKey"] ?? "minioadmin";
            var secretKey = configuration["Minio:SecretKey"] ?? "minioadmin";

            return new MinioClient()
                .WithEndpoint(endpoint)
                .WithCredentials(accessKey, secretKey)
                .WithSSL(useSSL)
                .Build();
        });

        services.AddSingleton<IFileStorageService, MinioFileStorageService>();

        return services;
    }
}
