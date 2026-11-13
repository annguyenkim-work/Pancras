using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Pancras.Application.Auditing;
using Pancras.Domain.Auditing;

namespace Pancras.Infrastructure.Persistence;

/// <summary>
/// Ghi dấu vết (ai tạo, ai sửa lần cuối, lúc nào) cho mọi thực thể kế thừa <see cref="AuditStamped"/> ngay trước khi lưu,
/// nên mọi module có dấu vết như nhau mà handler không phải nhớ. Người thao tác lấy từ <see cref="IRequestInfo"/> (mã nhân viên);
/// không có người dùng (lệnh tạo Admin đầu tiên, tiến trình nền) thì ghi "system".
/// </summary>
public sealed class AuditStampInterceptor(IRequestInfo? request = null, TimeProvider? clock = null) : SaveChangesInterceptor
{
    /// <summary>
    /// Annotation trên loại thực thể: danh sách thuộc tính mà việc chỉ đổi chúng không tính là "sửa bản ghi"
    /// (ví dụ bộ đếm đăng nhập sai của nhân viên).
    /// </summary>
    public const string IgnoredPropertiesAnnotation = "Pancras:AuditStampIgnoredProperties";

    public const string SystemActor = "system";

    private static readonly HashSet<string> StampProperties =
    [
        nameof(AuditStamped.CreatedAt),
        nameof(AuditStamped.CreatedBy),
        nameof(AuditStamped.UpdatedAt),
        nameof(AuditStamped.UpdatedBy)
    ];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        context.ChangeTracker.DetectChanges();
        var now = (clock ?? TimeProvider.System).GetUtcNow();
        var actor = string.IsNullOrWhiteSpace(request?.ActorCode) ? SystemActor : request!.ActorCode!.Trim();
        if (actor.Length > AuditStamped.ActorMaxLength)
        {
            actor = actor[..AuditStamped.ActorMaxLength];
        }

        foreach (var entry in context.ChangeTracker.Entries<AuditStamped>())
        {
            if (entry.State == EntityState.Added)
            {
                // Thực thể đã tự đóng dấu (đơn mua) thì giữ nguyên.
                if (entry.Entity.CreatedAt == default)
                {
                    entry.Property(nameof(AuditStamped.CreatedAt)).CurrentValue = now;
                }

                if (string.IsNullOrEmpty(entry.Entity.CreatedBy))
                {
                    entry.Property(nameof(AuditStamped.CreatedBy)).CurrentValue = actor;
                }
            }
            else if (entry.State == EntityState.Modified && HasRealChange(entry))
            {
                entry.Property(nameof(AuditStamped.UpdatedAt)).CurrentValue = now;
                entry.Property(nameof(AuditStamped.UpdatedBy)).CurrentValue = actor;
            }
        }
    }

    private static bool HasRealChange(EntityEntry entry)
    {
        // Thực thể đã tự cập nhật dấu vết (đơn mua) thì không ghi đè.
        if (entry.Property(nameof(AuditStamped.UpdatedAt)).IsModified)
        {
            return false;
        }

        var ignored = entry.Metadata.FindAnnotation(IgnoredPropertiesAnnotation)?.Value as string[] ?? [];
        return entry.Properties.Any(property =>
            property.IsModified
            && !StampProperties.Contains(property.Metadata.Name)
            && !ignored.Contains(property.Metadata.Name));
    }
}
