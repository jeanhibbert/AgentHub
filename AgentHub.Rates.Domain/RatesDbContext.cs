using Microsoft.EntityFrameworkCore;

namespace AgentHub.Rates.Domain;

public sealed class RatesDbContext(DbContextOptions<RatesDbContext> options) : DbContext(options)
{
    public DbSet<SwapCurveShiftEntity> SwapCurveShifts => Set<SwapCurveShiftEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var shift = modelBuilder.Entity<SwapCurveShiftEntity>();
        shift.HasKey(item => item.Id);
        shift.HasIndex(item => item.ShiftId).IsUnique();
        shift.Property(item => item.ShiftId).HasMaxLength(64);
        shift.Property(item => item.ScenarioId).HasMaxLength(128);
        shift.Property(item => item.CorrelationKey).HasMaxLength(128);
        shift.Property(item => item.Desk).HasMaxLength(64);
        shift.Property(item => item.Narrative).HasMaxLength(512);
    }
}
