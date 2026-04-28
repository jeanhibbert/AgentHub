using Microsoft.EntityFrameworkCore;

namespace AgentHub.Commodities.Domain;

public sealed class CommoditiesDbContext(DbContextOptions<CommoditiesDbContext> options) : DbContext(options)
{
    public DbSet<CommodityTradeEntity> CommodityTrades => Set<CommodityTradeEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var trade = modelBuilder.Entity<CommodityTradeEntity>();
        trade.HasKey(item => item.Id);
        trade.HasIndex(item => item.TradeId).IsUnique();
        trade.Property(item => item.TradeId).HasMaxLength(64);
        trade.Property(item => item.ScenarioId).HasMaxLength(128);
        trade.Property(item => item.CorrelationKey).HasMaxLength(128);
        trade.Property(item => item.Commodity).HasMaxLength(64);
        trade.Property(item => item.Benchmark).HasMaxLength(32);
        trade.Property(item => item.Trader).HasMaxLength(64);
        trade.Property(item => item.Desk).HasMaxLength(64);
        trade.Property(item => item.Narrative).HasMaxLength(512);
    }
}
