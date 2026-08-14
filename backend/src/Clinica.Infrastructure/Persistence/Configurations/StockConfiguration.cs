using Clinica.Domain.Stock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinica.Infrastructure.Persistence.Configurations;

public class StockItemConfiguration : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> builder)
    {
        builder.ToTable("stock_items");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Name).HasMaxLength(150).IsRequired();
        builder.Property(i => i.Unit).HasMaxLength(20).IsRequired();
        builder.Property(i => i.PurchaseUnit).HasMaxLength(20).IsRequired();
        builder.Property(i => i.ContentPerUnit).HasPrecision(12, 3);
        builder.Property(i => i.ControlMode).HasConversion<string>().HasMaxLength(20).IsRequired();

        // 3 casas: insumo em ml ou g raramente é número redondo.
        builder.Property(i => i.MinimumQuantity).HasPrecision(12, 3);

        builder.HasIndex(i => new { i.TenantId, i.Name }).IsUnique();
    }
}

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("stock_movements");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(m => m.Quantity).HasPrecision(12, 3);
        builder.Property(m => m.Reason).HasMaxLength(500);

        builder.HasOne(m => m.StockItem)
            .WithMany()
            .HasForeignKey(m => m.StockItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Appointment)
            .WithMany()
            .HasForeignKey(m => m.AppointmentId)
            .OnDelete(DeleteBehavior.SetNull);

        // O saldo é somado por item; este índice é o que sustenta a tela de estoque.
        builder.HasIndex(m => new { m.TenantId, m.StockItemId });
    }
}
