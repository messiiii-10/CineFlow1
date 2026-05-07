using CineFlow.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CineFlow.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(AppDbContext dbContext, string contentRootPath)
        {
            await dbContext.Database.EnsureCreatedAsync();

            if (!await dbContext.Adminler.AnyAsync())
            {
                dbContext.Adminler.Add(new Admin
                {
                    Email = "admin@gmail.com",
                    PasswordHash = PasswordHasher.HashPassword("Admin123!")
                });
            }

            var needsBootstrap = await dbContext.Icerikler.CountAsync() < 100;
            if (needsBootstrap)
            {
                dbContext.Yorumlar.RemoveRange(dbContext.Yorumlar);
                dbContext.Icerikler.RemoveRange(dbContext.Icerikler);
                await dbContext.SaveChangesAsync();

                var seedPath = Path.Combine(contentRootPath, "Data", "seed-catalog.json");
                var json = await File.ReadAllTextAsync(seedPath);
                var items = JsonSerializer.Deserialize<List<SeedCatalogItem>>(json) ?? new List<SeedCatalogItem>();

                dbContext.Icerikler.AddRange(items.Select(MapToEntity));
            }

            await dbContext.SaveChangesAsync();
        }

        private static Icerik MapToEntity(SeedCatalogItem item)
        {
            return new Icerik
            {
                AniListId = item.AniListId,
                Baslik = item.Baslik,
                AlternatifBaslik = item.AlternatifBaslik,
                OrijinalBaslik = item.OrijinalBaslik,
                Aciklama = item.Aciklama,
                Tur = Enum.TryParse<IcerikTuru>(item.Tur, ignoreCase: true, out var tur) ? tur : IcerikTuru.Anime,
                Format = item.Format,
                Kategori = item.Kategori,
                Etiketler = item.Etiketler,
                Durum = item.Durum,
                BaslangicYili = item.BaslangicYili,
                BitisYili = item.BitisYili,
                BolumSayisi = item.BolumSayisi,
                CiltSayisi = item.CiltSayisi,
                SureDakika = item.SureDakika,
                Studyo = item.Studyo,
                Yaraticilar = item.Yaraticilar,
                Kaynak = item.Kaynak,
                Skor = item.Skor,
                Populerlik = item.Populerlik,
                ResimYolu = item.ResimYolu,
                BannerYolu = item.BannerYolu,
                DisBaglanti = item.DisBaglanti,
                AnaKarakterler = item.AnaKarakterler
            };
        }

        private sealed class SeedCatalogItem
        {
            public int AniListId { get; set; }
            public string Baslik { get; set; } = string.Empty;
            public string? AlternatifBaslik { get; set; }
            public string? OrijinalBaslik { get; set; }
            public string Aciklama { get; set; } = string.Empty;
            public string Tur { get; set; } = "Anime";
            public string? Format { get; set; }
            public string? Kategori { get; set; }
            public string? Etiketler { get; set; }
            public string? Durum { get; set; }
            public int? BaslangicYili { get; set; }
            public int? BitisYili { get; set; }
            public int? BolumSayisi { get; set; }
            public int? CiltSayisi { get; set; }
            public int? SureDakika { get; set; }
            public string? Studyo { get; set; }
            public string? Yaraticilar { get; set; }
            public string? Kaynak { get; set; }
            public int? Skor { get; set; }
            public int? Populerlik { get; set; }
            public string? ResimYolu { get; set; }
            public string? BannerYolu { get; set; }
            public string? DisBaglanti { get; set; }
            public string? AnaKarakterler { get; set; }
        }
    }
}
