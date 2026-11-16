using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pancras.Domain.Catalog;

namespace Pancras.Infrastructure.Persistence;

public sealed class CatalogItemConfiguration : IEntityTypeConfiguration<CatalogItem>
{
    public void Configure(EntityTypeBuilder<CatalogItem> builder)
    {
        builder.ToTable("catalog_item");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Code).HasMaxLength(CatalogItem.CodeMaxLength).IsRequired();
        builder.Property(item => item.NormalizedCode).HasMaxLength(CatalogItem.CodeMaxLength).IsRequired();
        builder.Property(item => item.Name).HasMaxLength(CatalogItem.NameMaxLength).IsRequired();
        builder.Property(item => item.Category).HasConversion<int>().IsRequired();
        builder.Property(item => item.Brand).HasMaxLength(CatalogItem.BrandMaxLength).IsRequired();
        builder.Property(item => item.Origin).HasMaxLength(CatalogItem.OriginMaxLength).IsRequired();
        builder.Property(item => item.Unit).HasMaxLength(CatalogItem.UnitMaxLength).IsRequired();
        builder.Property(item => item.WeightKg).HasPrecision(12, 3);
        builder.Property(item => item.VolumeM3).HasPrecision(12, 3);
        builder.Property(item => item.ImagePath).HasMaxLength(CatalogItem.ImagePathMaxLength).IsRequired();
        builder.Property(item => item.MinimumStock).HasPrecision(12, 3);
        builder.Property(item => item.CostPrice).HasPrecision(18, 0);
        builder.Property(item => item.RetailPrice).HasPrecision(18, 0);
        builder.Property(item => item.DealerPrice).HasPrecision(18, 0);
        builder.Property(item => item.DistributorPrice).HasPrecision(18, 0);
        builder.Property(item => item.ProjectPrice).HasPrecision(18, 0);
        builder.Property(item => item.IsCombo).IsRequired().HasDefaultValue(false);
        builder.Property(item => item.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(item => item.Version).IsConcurrencyToken().IsRequired();
        builder.HasIndex(item => item.NormalizedCode).IsUnique();
    }
}
