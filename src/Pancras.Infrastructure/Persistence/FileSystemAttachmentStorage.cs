using Pancras.Application.Purchasing;
using Pancras.Domain.Purchasing;

namespace Pancras.Infrastructure.Persistence;

/// <summary>Lưu file chứng từ trên đĩa, trong một thư mục riêng ngoài wwwroot. Tên file do hệ thống sinh và được kiểm tra trước khi ghép đường dẫn.</summary>
public sealed class FileSystemAttachmentStorage : IAttachmentStorage
{
    private readonly string _root;

    public FileSystemAttachmentStorage(string root)
    {
        _root = Path.GetFullPath(root);
    }

    public async Task SaveAsync(string storedName, Stream content, CancellationToken cancellationToken)
    {
        var path = ResolvePath(storedName);
        Directory.CreateDirectory(_root);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, cancellationToken);
    }

    public Task<Stream?> OpenReadAsync(string storedName, CancellationToken cancellationToken)
    {
        var path = ResolvePath(storedName);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storedName, CancellationToken cancellationToken)
    {
        var path = ResolvePath(storedName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string ResolvePath(string storedName)
    {
        if (!AttachmentRules.IsSafeStoredName(storedName))
        {
            throw new ArgumentException("Tên file lưu trữ không hợp lệ.", nameof(storedName));
        }

        var path = Path.GetFullPath(Path.Combine(_root, storedName));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("Đường dẫn nằm ngoài thư mục lưu trữ.", nameof(storedName));
        }

        return path;
    }
}
