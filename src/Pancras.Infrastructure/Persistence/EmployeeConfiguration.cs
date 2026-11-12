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
        builder.Property(employee => employee.FullName).HasMaxLength(200).IsRequired();
        builder.Property(employee => employee.Code).HasMaxLength(220).IsRequired();
        builder.Property(employee => employee.NormalizedCode).HasMaxLength(220).IsRequired();
        builder.Property(employee => employee.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(employee => employee.Role).HasMaxLength(50).IsRequired();
        builder.Property(employee => employee.SecurityStamp).HasMaxLength(64).IsRequired();
        builder.HasIndex(employee => employee.NormalizedCode).IsUnique();
    }
}
