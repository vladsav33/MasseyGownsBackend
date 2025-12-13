using GownApi.Model;
using GownApi.Model.Dto;
using Microsoft.EntityFrameworkCore;

namespace GownApi
{
    public class GownDb : DbContext
    {
        public GownDb(DbContextOptions<GownDb> options)
            : base(options) { }
        public DbSet<Degrees> degrees => Set<Degrees>();
        public DbSet<Items> items => Set<Items>();
        public DbSet<Ceremonies> ceremonies => Set<Ceremonies>();
        public DbSet<DegreesAndCeremonies> degreesCeremonies => Set<DegreesAndCeremonies>();
        public DbSet<CeremonyDegree> ceremonyDegree => Set<CeremonyDegree>();
        public DbSet<Orders> orders => Set<Orders>();
        public DbSet<Faq> faq => Set<Faq>();
        public DbSet<Sizes> sizes => Set<Sizes>();
        public DbSet<Fit> fit => Set<Fit>();
        public DbSet<HoodType> hoods => Set<HoodType>();
        public DbSet<ItemDegreeDto> ItemDegreeDtos { get; set; }  // optional
        public DbSet<ItemDegreeModel> itemDegreeModels { get; set; }
        public DbSet<OrderedItems> orderedItems { get; set; }
        public DbSet<SelectedItemOut> selectedItemOut { get; set; }
        public DbSet<Contacts> Contacts { get; set; }
        public DbSet<User> users { get; set; }
        public DbSet<Sku> Sku { get; set; }
        public DbSet<CmsContentBlock> CmsContentBlocks { get; set; }
        public DbSet<EmailTemplate> EmailTemplates { get; set; } 

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<ItemDegreeModel>().HasNoKey();  // tells EF Core it's a query type
        }
    }
}
