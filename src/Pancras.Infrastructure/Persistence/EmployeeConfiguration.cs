using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pancras.Domain.Employees;

namespace Pancras.Infrastructure.Persistence;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("employee");
        builder.HasKey(employee => employee.Id);
        // Bộ đếm đăng nhập sai, khóa tạm và bước TOTP đổi mỗi lần đăng nhập: không tính là "sửa hồ sơ nhân viên".
        builder.HasAnnotation(
            AuditStampInterceptor.IgnoredPropertiesAnnotation,
            new[] { nameof(Employee.FailedLoginCount), nameof(Employee.LockedUntil), nameof(Employee.LastTotpStep), nameof(Employee.RecoveryCodeHashes) });
        builder.Property(employee => employee.FullName).HasMaxLength(200).IsRequired();
        builder.Property(employee => employee.Code).HasMaxLength(220).IsRequired();
        builder.Property(employee => employee.NormalizedCode).HasMaxLength(220).IsRequired();
        builder.Property(employee => employee.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(employee => employee.Role).HasMaxLength(50).IsRequired();
        builder.Property(employee => employee.SecurityStamp).HasMaxLength(64).IsRequired();
        builder.Property(employee => employee.Email).HasMaxLength(200).IsRequired();
        builder.Property(employee => employee.Phone).HasMaxLength(30).IsRequired();
        builder.Property(employee => employee.Address).HasMaxLength(300).IsRequired();
        builder.Property(employee => employee.IsActive).IsRequired();
        builder.Property(employee => employee.FailedLoginCount).IsRequired();
        builder.Property(employee => employee.TwoFactorEnabled).IsRequired();
        builder.Property(employee => employee.TotpSecret).HasMaxLength(600).IsRequired();
        builder.Property(employee => employee.RecoveryCodeHashes).HasMaxLength(1000).IsRequired();
        builder.Property(employee => employee.LastTotpStep).IsRequired();
        builder.HasOne<Pancras.Domain.Inventory.StockLocation>().WithMany().HasForeignKey(employee => employee.StockLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(employee => employee.StockLocationId)
            .IsUnique()
            .HasFilter("role = 'Trưởng Showroom/Kho' AND is_active")
            .HasDatabaseName("ux_employee_active_lead_per_location");
        builder.Property(employee => employee.JobTitle).HasMaxLength(100).IsRequired();
        builder.Property(employee => employee.Department).HasMaxLength(100).IsRequired();
        builder.HasIndex(employee => employee.NormalizedCode).IsUnique();
    }
}
