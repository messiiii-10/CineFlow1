using CineFlow.Models;
using Microsoft.EntityFrameworkCore;

namespace CineFlow.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Icerik> Icerikler => Set<Icerik>();
        public DbSet<Yorum> Yorumlar => Set<Yorum>();
        public DbSet<Kullanici> Kullanicilar => Set<Kullanici>();
        public DbSet<Admin> Adminler => Set<Admin>();
        public DbSet<KullaniciIcerikKaydi> KullaniciIcerikKayitlari => Set<KullaniciIcerikKaydi>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Kullanici>()
                .HasIndex(x => x.KullaniciAdi)
                .IsUnique();

            modelBuilder.Entity<Kullanici>()
                .HasIndex(x => x.Email)
                .IsUnique();

            modelBuilder.Entity<Admin>()
                .HasIndex(x => x.Email)
                .IsUnique();

            modelBuilder.Entity<Icerik>()
                .HasIndex(x => x.AniListId)
                .IsUnique();

            modelBuilder.Entity<Icerik>()
                .HasMany(x => x.Yorumlar)
                .WithOne(x => x.Icerik!)
                .HasForeignKey(x => x.IcerikId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<KullaniciIcerikKaydi>()
                .HasIndex(x => new { x.KullaniciEmail, x.IcerikId })
                .IsUnique();

            modelBuilder.Entity<KullaniciIcerikKaydi>()
                .HasOne(x => x.Icerik)
                .WithMany()
                .HasForeignKey(x => x.IcerikId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
