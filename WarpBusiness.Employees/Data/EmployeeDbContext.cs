using Microsoft.EntityFrameworkCore;
using WarpBusiness.Employees.Models;

namespace WarpBusiness.Employees.Data;

public class EmployeeDbContext : DbContext
{
    public EmployeeDbContext(DbContextOptions<EmployeeDbContext> options) : base(options)
    {
    }

    public DbSet<Employee> Employees => Set<Employee>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("employees");

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.EmployeeNumber, e.TenantId }).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.TenantId);

            entity.Property(e => e.EmployeeNumber).HasMaxLength(50);
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.MiddleName).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.Phone).HasMaxLength(50);
            entity.Property(e => e.Department).HasMaxLength(200);
            entity.Property(e => e.JobTitle).HasMaxLength(200);

            entity.Property(e => e.EmploymentStatus)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(e => e.EmploymentType)
                .HasConversion<string>()
                .HasMaxLength(50);

            // Self-referencing FK for org hierarchy
            entity.HasOne(e => e.Manager)
                .WithMany(e => e.DirectReports)
                .HasForeignKey(e => e.ManagerId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
