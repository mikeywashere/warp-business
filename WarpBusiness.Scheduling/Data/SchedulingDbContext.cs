using Microsoft.EntityFrameworkCore;
using WarpBusiness.Scheduling.Models;

namespace WarpBusiness.Scheduling.Data;

public class SchedulingDbContext : DbContext
{
    public SchedulingDbContext(DbContextOptions<SchedulingDbContext> options) : base(options)
    {
    }

    public DbSet<WorkLocation> WorkLocations => Set<WorkLocation>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<ScheduleTemplate> ScheduleTemplates => Set<ScheduleTemplate>();
    public DbSet<StaffingBlock> StaffingBlocks => Set<StaffingBlock>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<ScheduleShift> ScheduleShifts => Set<ScheduleShift>();
    public DbSet<ScheduleBreak> ScheduleBreaks => Set<ScheduleBreak>();
    public DbSet<BreakRule> BreakRules => Set<BreakRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("scheduling");

        modelBuilder.Entity<WorkLocation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Name, e.TenantId }).IsUnique();
            entity.HasIndex(e => e.TenantId);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.State).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Country).HasMaxLength(100);
        });

        modelBuilder.Entity<Position>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Name, e.TenantId }).IsUnique();
            entity.HasIndex(e => e.TenantId);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Color).HasMaxLength(20).IsRequired();
        });

        modelBuilder.Entity<ScheduleTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Name, e.TenantId }).IsUnique();
            entity.HasIndex(e => e.TenantId);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);

            entity.HasOne(e => e.WorkLocation)
                .WithMany(l => l.ScheduleTemplates)
                .HasForeignKey(e => e.WorkLocationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StaffingBlock>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TemplateId, e.PositionId, e.DayOfWeek, e.StartTime });

            entity.HasOne(e => e.Template)
                .WithMany(t => t.StaffingBlocks)
                .HasForeignKey(e => e.TemplateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Schedule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.WorkLocationId, e.StartDate, e.EndDate });
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.HasOne(e => e.WorkLocation)
                .WithMany(l => l.Schedules)
                .HasForeignKey(e => e.WorkLocationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ScheduleShift>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ScheduleId, e.Date });
            entity.HasIndex(e => e.EmployeeId);
            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(50);
            entity.Property(e => e.Notes).HasMaxLength(1000);

            entity.HasOne(e => e.Schedule)
                .WithMany(s => s.Shifts)
                .HasForeignKey(e => e.ScheduleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ScheduleBreak>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ShiftId);
            entity.Property(e => e.BreakType)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.HasOne(e => e.Shift)
                .WithMany(s => s.Breaks)
                .HasForeignKey(e => e.ShiftId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BreakRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.State, e.RuleType });
            entity.Property(e => e.State).HasMaxLength(10).IsRequired();
            entity.Property(e => e.RuleType)
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.Property(e => e.Notes).HasMaxLength(2000);
        });
    }
}
