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

            // Parse connection string: http://accessKey:secretKey@endpoint:port
            var uri = new Uri(connectionString);
            var endpoint = $"{uri.Host}:{uri.Port}";
            var accessKey = uri.UserInfo.Split(':')[0];
            var secretKey = uri.UserInfo.Split(':')[1];
            var useSSL = uri.Scheme == "https";

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
