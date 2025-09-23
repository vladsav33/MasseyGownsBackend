using GownApi;
using GownApi.Dto;
using GownsApi;
using Microsoft.EntityFrameworkCore;

public class GownDb : DbContext
{
    public GownDb(DbContextOptions<GownDb> options)
        : base(options) { }

    public DbSet<Degrees> degrees => Set<Degrees>();

    public DbSet<Items> items => Set<Items>();

    public DbSet<Ceremonies> ceremonies => Set<Ceremonies>();

    public DbSet<DegreesAndCeremonies> degreesCeremonies => Set<DegreesAndCeremonies>();

    public DbSet<Orders> orders => Set<Orders>();
    public DbSet<Faq> faq => Set<Faq>();
    public DbSet<Sizes> sizes => Set<Sizes>();
    public DbSet<Fit> fit => Set<Fit>();
    public DbSet<ItemDegreeDto> ItemDegreeDtos { get; set; }  // optional
    public DbSet<ItemDegreeModel> ItemDegreeModels { get; set; }
    public DbSet<Contacts> Contacts { get; set; } //Joe20250920
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<ItemDegreeModel>().HasNoKey();  // tells EF Core it's a query type
    }

}
