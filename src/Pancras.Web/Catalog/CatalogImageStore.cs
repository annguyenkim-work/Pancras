namespace Pancras.Web.Catalog;

public sealed class CatalogImageStore(IWebHostEnvironment environment, Microsoft.Extensions.Configuration.IConfiguration configuration)
{
    public const long MaxBytes = 5 * 1024 * 1024;

    public const string WebFolder = "/uploads/catalog/";

    public async Task<CatalogImageDraft> ReadAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return CatalogImageDraft.None;
        }

        if (file.Length > MaxBytes)
        {
            return CatalogImageDraft.Reject("Ảnh không được lớn hơn 5 MB.");
        }

        var extension = Path.GetExtension(Path.GetFileName(file.FileName)).ToLowerInvariant();
        if (!IsAllowed(extension, file.ContentType))
        {
            return CatalogImageDraft.Reject("Ảnh phải là JPG, PNG, WEBP hoặc GIF.");
        }

        var content = new byte[checked((int)file.Length)];
        await using (var stream = file.OpenReadStream())
        {
            var read = 0;
            while (read < content.Length)
            {
                var count = await stream.ReadAsync(content.AsMemory(read), cancellationToken);
                if (count == 0)
                {
                    return CatalogImageDraft.Reject("Ảnh phải là JPG, PNG, WEBP hoặc GIF.");
                }

                read += count;
            }
        }

        if (!MagicMatches(extension, content))
        {
            return CatalogImageDraft.Reject("Ảnh phải là JPG, PNG, WEBP hoặc GIF.");
        }

        return CatalogImageDraft.Ready(content, extension == ".jpeg" ? ".jpg" : extension);
    }

    public async Task<string> SaveAsync(CatalogImageDraft draft, CancellationToken cancellationToken)
    {
        if (draft.Content is null || draft.Extension is null)
        {
            throw new InvalidOperationException("Không có ảnh để lưu.");
        }

        var root = Root();
        Directory.CreateDirectory(root);
        var name = Guid.CreateVersion7().ToString("N") + draft.Extension;
        var physical = Path.Combine(root, name);
        await File.WriteAllBytesAsync(physical, draft.Content, cancellationToken);
        return WebFolder + name;
    }

    public void Delete(string? webPath)
    {
        if (!TryMap(webPath, out var physical) || !File.Exists(physical))
        {
            return;
        }

        File.Delete(physical);
    }

    public static string? PublicUrl(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var trimmed = path.Trim().Replace('\\', '/');
        if (!trimmed.StartsWith(WebFolder, StringComparison.Ordinal))
        {
            return null;
        }

        var name = trimmed[WebFolder.Length..];
        if (name.Length == 0 || name.Contains('/') || name.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        return WebFolder + name;
    }

    private bool TryMap(string? webPath, out string physical)
    {
        physical = string.Empty;
        var url = PublicUrl(webPath);
        if (url is null)
        {
            return false;
        }

        var name = url[WebFolder.Length..];
        var root = Path.GetFullPath(Root());
        var candidate = Path.GetFullPath(Path.Combine(root, name));
        if (!candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !candidate.Equals(root, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        physical = candidate;
        return true;
    }

    /// <summary>Thư mục gốc của ảnh tải lên: <c>Storage:UploadsPath</c> nếu cấu hình, không thì <c>wwwroot/uploads</c>.</summary>
    public string UploadsRoot()
    {
        var configured = configuration["Storage:UploadsPath"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var web = environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(web))
        {
            web = Path.Combine(environment.ContentRootPath, "wwwroot");
        }

        return Path.Combine(web, "uploads");
    }

    private string Root()
    {
        return Path.Combine(UploadsRoot(), "catalog");
    }


    private static bool IsAllowed(string extension, string? contentType)
    {
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".gif"))
        {
            return false;
        }

        var media = MediaType(contentType);
        if (media.Length == 0 || media.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return extension switch
        {
            ".jpg" or ".jpeg" => media.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)
                || media.Equals("image/jpg", StringComparison.OrdinalIgnoreCase)
                || media.Equals("image/pjpeg", StringComparison.OrdinalIgnoreCase),
            ".png" => media.Equals("image/png", StringComparison.OrdinalIgnoreCase),
            ".gif" => media.Equals("image/gif", StringComparison.OrdinalIgnoreCase),
            ".webp" => media.Equals("image/webp", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static string MediaType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return string.Empty;
        }

        var separator = contentType.IndexOf(';');
        return (separator < 0 ? contentType : contentType[..separator]).Trim();
    }

    private static bool MagicMatches(string extension, ReadOnlySpan<byte> header)
    {
        return extension switch
        {
            ".jpg" or ".jpeg" => header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => header.Length >= 8
                && header[0] == 0x89
                && header[1] == 0x50
                && header[2] == 0x4E
                && header[3] == 0x47
                && header[4] == 0x0D
                && header[5] == 0x0A
                && header[6] == 0x1A
                && header[7] == 0x0A,
            ".gif" => header.Length >= 6
                && header[0] == (byte)'G'
                && header[1] == (byte)'I'
                && header[2] == (byte)'F'
                && header[3] == (byte)'8'
                && (header[4] == (byte)'7' || header[4] == (byte)'9')
                && header[5] == (byte)'a',
            ".webp" => header.Length >= 12
                && header[0] == (byte)'R'
                && header[1] == (byte)'I'
                && header[2] == (byte)'F'
                && header[3] == (byte)'F'
                && header[8] == (byte)'W'
                && header[9] == (byte)'E'
                && header[10] == (byte)'B'
                && header[11] == (byte)'P',
            _ => false
        };
    }
}

public sealed class CatalogImageDraft
{
    private CatalogImageDraft(bool accepted, string? error, byte[]? content, string? extension)
    {
        Accepted = accepted;
        Error = error;
        Content = content;
        Extension = extension;
    }

    public bool Accepted { get; }

    public string? Error { get; }

    public byte[]? Content { get; }

    public string? Extension { get; }

    public bool HasContent => Content is { Length: > 0 };

    public static CatalogImageDraft None { get; } = new(true, null, null, null);

    public static CatalogImageDraft Reject(string error) => new(false, error, null, null);

    public static CatalogImageDraft Ready(byte[] content, string extension) => new(true, null, content, extension);
}
