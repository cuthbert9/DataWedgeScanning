using DataWedgeScanner.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace DataWedgeScanner.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Item> Items => Set<Item>();

    public DbSet<ScanEvent> ScanEvents => Set<ScanEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Item>(entity =>
        {
            entity.ToTable("Items");

            entity.Property(i => i.Barcode)
                .IsRequired()
                .HasMaxLength(64);

            // Barcode is the natural key the scanner matches against -- must be unique and indexed.
            entity.HasIndex(i => i.Barcode)
                .IsUnique()
                .HasDatabaseName("IX_Items_Barcode");

            entity.Property(i => i.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(i => i.Description)
                .HasMaxLength(1000);

            // Store enums as their string name so the raw table is readable without a lookup,
            // and so adding new enum values later never shifts existing stored data.
            entity.Property(i => i.Status)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(i => i.CreatedAt).IsRequired();
            entity.Property(i => i.UpdatedAt).IsRequired();
        });

        modelBuilder.Entity<ScanEvent>(entity =>
        {
            entity.ToTable("ScanEvents");

            entity.Property(s => s.Barcode)
                .IsRequired()
                .HasMaxLength(64);

            entity.HasIndex(s => s.Barcode)
                .HasDatabaseName("IX_ScanEvents_Barcode");

            entity.HasIndex(s => s.ScannedAt)
                .HasDatabaseName("IX_ScanEvents_ScannedAt");

            entity.Property(s => s.Result)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(s => s.PreviousStatus)
                .HasConversion<string>()
                .HasMaxLength(32);

            entity.Property(s => s.NewStatus)
                .HasConversion<string>()
                .HasMaxLength(32);

            entity.Property(s => s.RawData).HasMaxLength(500);
            entity.Property(s => s.SourceIp).HasMaxLength(64);
            entity.Property(s => s.ErrorMessage).HasMaxLength(1000);

            entity.Property(s => s.ScannedAt).IsRequired();

            // ScanEvent.ItemId is nullable because a scan of an unknown barcode has no matching
            // Item. When an Item is deleted, keep its scan history but null out the link rather
            // than cascading the delete -- audit rows should outlive the item they refer to.
            entity.HasOne(s => s.Item)
                .WithMany(i => i.ScanEvents)
                .HasForeignKey(s => s.ItemId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
