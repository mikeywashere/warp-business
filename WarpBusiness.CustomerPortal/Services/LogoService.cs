namespace WarpBusiness.CustomerPortal.Services;

public class LogoService
{
    private readonly ILogger<LogoService> _logger;
    private static readonly string[] AllowedMimeTypes = ["image/png", "image/jpeg", "image/webp", "image/svg+xml"];
    private const long MaxFileSize = 2 * 1024 * 1024; // 2MB

    public LogoService(ILogger<LogoService> logger)
    {
        _logger = logger;
    }

    public bool IsValidMimeType(string mimeType)
    {
        return AllowedMimeTypes.Contains(mimeType.ToLowerInvariant());
    }

    public bool IsValidFileSize(long sizeBytes)
    {
        return sizeBytes <= MaxFileSize;
    }

    public async Task<(byte[] bytes, string mimeType)?> ValidateAndReadAsync(IFormFile file)
    {
        if (file == null)
            return null;

        if (!IsValidFileSize(file.Length))
        {
            _logger.LogWarning("[LogoService] File too large: {Size} bytes (max {Max})",
                file.Length, MaxFileSize);
            return null;
        }

        var mimeType = file.ContentType?.ToLowerInvariant() ?? "application/octet-stream";
        if (!IsValidMimeType(mimeType))
        {
            _logger.LogWarning("[LogoService] Invalid MIME type: {MimeType}", mimeType);
            return null;
        }

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        return (ms.ToArray(), mimeType);
    }

    public string ToBase64DataUri(byte[] logoBytes, string mimeType)
    {
        var base64 = Convert.ToBase64String(logoBytes);
        return $"data:{mimeType};base64,{base64}";
    }

    public string GetFileExtension(string mimeType) => mimeType.ToLowerInvariant() switch
    {
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/webp" => ".webp",
        "image/svg+xml" => ".svg",
        _ => ".bin"
    };
}
