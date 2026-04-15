using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;

namespace WarpBusiness.Storage;

public static class StorageServiceExtensions
{
    public static IServiceCollection AddMinioStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("minio");
        if (string.IsNullOrEmpty(connectionString))
        {
            // No MinIO configured (e.g. running without Aspire). Register a no-op so the API
            // starts cleanly; storage endpoints will return 503 when invoked.
            services.AddSingleton<IFileStorageService, NoOpFileStorageService>();
            return services;
        }

        services.AddSingleton<IMinioClient>(sp =>
        {
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
