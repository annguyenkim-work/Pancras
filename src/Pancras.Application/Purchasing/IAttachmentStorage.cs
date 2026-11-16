using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Domain.Auditing;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Purchasing;

/// <summary>Nơi lưu nội dung file chứng từ, nằm ngoài thư mục công khai của web.</summary>
public interface IAttachmentStorage
{
    Task SaveAsync(string storedName, Stream content, CancellationToken cancellationToken);

    /// <summary>Trả về null nếu file không còn.</summary>
    Task<Stream?> OpenReadAsync(string storedName, CancellationToken cancellationToken);

    Task DeleteAsync(string storedName, CancellationToken cancellationToken);
}
