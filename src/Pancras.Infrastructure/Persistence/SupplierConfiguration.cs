using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pancras.Domain.Suppliers;

namespace Pancras.Infrastructure.Persistence;

public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("supplier");
        builder.HasKey(supplier => supplier.Id);
        builder.Property(supplier => supplier.Code).HasMaxLength(Supplier.CodeMaxLength).IsRequired();
        builder.Property(supplier => supplier.NormalizedCode).HasMaxLength(Supplier.CodeMaxLength).IsRequired();
        builder.Property(supplier => supplier.Name).HasMaxLength(Supplier.NameMaxLength).IsRequired();
        builder.Property(supplier => supplier.Country).HasMaxLength(Supplier.CountryMaxLength).IsRequired();
        builder.Property(supplier => supplier.ContactName).HasMaxLength(Supplier.ContactNameMaxLength).IsRequired();
        builder.Property(supplier => supplier.Phone).HasMaxLength(Supplier.PhoneMaxLength).IsRequired();
        builder.Property(supplier => supplier.Email).HasMaxLength(Supplier.EmailMaxLength).IsRequired();
        builder.Property(supplier => supplier.Account).HasMaxLength(Supplier.AccountMaxLength).IsRequired();
        builder.Property(supplier => supplier.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.HasQueryFilter(supplier => !supplier.IsDeleted);
        builder.HasIndex(supplier => supplier.NormalizedCode)
            .IsUnique()
            .HasFilter("is_deleted = false");
    }
}
