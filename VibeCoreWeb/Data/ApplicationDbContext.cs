using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VibeCore.Models;

namespace VibeCore.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<TodoItem> Todos { get; set; }
    public DbSet<ScheduledTaskRun> ScheduledTaskRuns { get; set; }
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ScheduledTaskRun>(entity =>
        {
            entity.HasKey(run => run.Id);
            entity.Property(run => run.HandlerKey).HasMaxLength(100);
            entity.Property(run => run.Status).HasMaxLength(32);
            entity.Property(run => run.ErrorSummary).HasMaxLength(2000);
            entity.HasIndex(run => new { run.ScheduleId, run.StartedAt });
            entity.HasIndex(run => run.StartedAt);
        });
    }
}
