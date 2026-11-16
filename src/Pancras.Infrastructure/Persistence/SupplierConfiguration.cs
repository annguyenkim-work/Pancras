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
        builder.Property(supplier => supplier.Version).IsConcurrencyToken().IsRequired();
        builder.Property(supplier => supplier.Code).HasMaxLength(Supplier.CodeMaxLength).IsRequired();
        builder.Property(supplier => supplier.NormalizedCode).HasMaxLength(Supplier.CodeMaxLength).IsRequired();
        builder.Property(supplier => supplier.Name).HasMaxLength(Supplier.NameMaxLength).IsRequired();
        builder.Property(supplier => supplier.Country).HasMaxLength(Supplier.CountryMaxLength).IsRequired();
        builder.Property(supplier => supplier.ContactName).HasMaxLength(Supplier.ContactNameMaxLength).IsRequired();
        builder.Property(supplier => supplier.Phone).HasMaxLength(Supplier.PhoneMaxLength).IsRequired();
        builder.Property(supplier => supplier.Email).HasMaxLength(Supplier.EmailMaxLength).IsRequired();
        builder.Property(supplier => supplier.BankName).HasMaxLength(Supplier.BankNameMaxLength).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(supplier => supplier.Account).HasMaxLength(Supplier.AccountMaxLength).IsRequired();
        builder.Property(supplier => supplier.SwiftCode).HasMaxLength(Supplier.SwiftMaxLength).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(supplier => supplier.IsInactive).IsRequired().HasDefaultValue(false);
        builder.HasIndex(supplier => supplier.NormalizedCode)
            .IsUnique()
            .HasFilter("is_inactive = false");
    }
}
