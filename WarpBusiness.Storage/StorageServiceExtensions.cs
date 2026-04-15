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
            // Aspire's CommunityToolkit.Aspire.Hosting.Minio injects an ADO.NET-style
            // connection string: "Endpoint=http://host:port;AccessKey=...;SecretKey=..."
            // Parse that format first; fall back to treating the value as a plain URL.
            var parts = ParseConnectionStringParts(connectionString);

            string endpoint;
            bool useSSL;

            if (Uri.TryCreate(parts.Endpoint, UriKind.Absolute, out var uri))
            {
                endpoint = uri.Port > 0 ? $"{uri.Host}:{uri.Port}" : uri.Host;
                useSSL = uri.Scheme == "https";
            }
            else
            {
                endpoint = parts.Endpoint;
                useSSL = false;
            }

            // Credentials from the connection string take priority over config keys.
            var accessKey = parts.AccessKey
                ?? configuration["Minio:AccessKey"]
                ?? "minioadmin";
            var secretKey = parts.SecretKey
                ?? configuration["Minio:SecretKey"]
                ?? "minioadmin";

            return new MinioClient()
                .WithEndpoint(endpoint)
                .WithCredentials(accessKey, secretKey)
                .WithSSL(useSSL)
                .Build();
        });

        services.AddSingleton<IFileStorageService, MinioFileStorageService>();

        return services;
    }

    /// <summary>
    /// Parses an ADO.NET-style MinIO connection string
    /// (e.g. "Endpoint=http://host:port;AccessKey=x;SecretKey=y")
    /// OR a plain URL (e.g. "http://host:port").
    /// </summary>
    private static (string Endpoint, string? AccessKey, string? SecretKey) ParseConnectionStringParts(string connectionString)
    {
        if (!connectionString.Contains('='))
            return (connectionString, null, null);

        string? endpoint = null, accessKey = null, secretKey = null;

        foreach (var segment in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = segment.IndexOf('=');
            if (eq < 0) continue;

            var key   = segment[..eq].Trim();
            var value = segment[(eq + 1)..].Trim();

            switch (key.ToLowerInvariant())
            {
                case "endpoint": endpoint  = value; break;
                case "accesskey": accessKey = value; break;
                case "secretkey": secretKey = value; break;
            }
        }

        return (endpoint ?? connectionString, accessKey, secretKey);
    }
}
