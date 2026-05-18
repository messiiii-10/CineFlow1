using CineFlow.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Data;

namespace CineFlow.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(AppDbContext dbContext, string contentRootPath)
        {
            await dbContext.Database.EnsureCreatedAsync();
            await EnsureUserLibrarySchemaAsync(dbContext);
            await EnsureUserProfileSchemaAsync(dbContext);

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

            await EnsureSeriesCatalogAsync(dbContext);

            await dbContext.SaveChangesAsync();
        }

        private static async Task EnsureUserLibrarySchemaAsync(AppDbContext dbContext)
        {
            const string createTableSql = """
                CREATE TABLE IF NOT EXISTS "KullaniciIcerikKayitlari" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_KullaniciIcerikKayitlari" PRIMARY KEY AUTOINCREMENT,
                    "KullaniciEmail" TEXT NOT NULL,
                    "IcerikId" INTEGER NOT NULL,
                    "Durum" INTEGER NOT NULL,
                    "KisiselPuan" INTEGER NULL,
                    "OlusturmaTarihi" TEXT NOT NULL,
                    "GuncellemeTarihi" TEXT NOT NULL,
                    CONSTRAINT "FK_KullaniciIcerikKayitlari_Icerikler_IcerikId" FOREIGN KEY ("IcerikId") REFERENCES "Icerikler" ("Id") ON DELETE CASCADE
                );
                """;

            const string createIndexSql = """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_KullaniciIcerikKayitlari_KullaniciEmail_IcerikId"
                ON "KullaniciIcerikKayitlari" ("KullaniciEmail", "IcerikId");
                """;

            await dbContext.Database.ExecuteSqlRawAsync(createTableSql);
            await dbContext.Database.ExecuteSqlRawAsync(createIndexSql);
        }

        private static async Task EnsureUserProfileSchemaAsync(AppDbContext dbContext)
        {
            var connection = dbContext.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;

            if (shouldClose)
                await connection.OpenAsync();

            try
            {
                var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                await using var pragmaCommand = connection.CreateCommand();
                pragmaCommand.CommandText = "PRAGMA table_info(\"Kullanicilar\");";

                await using var reader = await pragmaCommand.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    columns.Add(reader.GetString(1));

                if (!columns.Contains("ProfilResmiYolu"))
                    await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE \"Kullanicilar\" ADD COLUMN \"ProfilResmiYolu\" TEXT NULL;");

                if (!columns.Contains("Biyografi"))
                    await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE \"Kullanicilar\" ADD COLUMN \"Biyografi\" TEXT NULL;");
            }
            finally
            {
                if (shouldClose)
                    await connection.CloseAsync();
            }
        }

        private static Task EnsureSeriesCatalogAsync(AppDbContext dbContext)
        {
            var mevcutBasliklar = dbContext.Icerikler
                .Where(x => x.Tur == IcerikTuru.Dizi)
                .Select(x => x.Baslik)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var eklenecekler = BuildSeriesCatalog()
                .Where(x => !mevcutBasliklar.Contains(x.Baslik))
                .ToList();

            if (eklenecekler.Count > 0)
                dbContext.Icerikler.AddRange(eklenecekler);

            return Task.CompletedTask;
        }

        private static List<Icerik> BuildSeriesCatalog()
        {
            return new List<Icerik>
            {
                new Icerik
                {
                    Baslik = "Breaking Bad",
                    AlternatifBaslik = "Breaking Bad",
                    Aciklama = "Bir lise kimya öğretmeni ölümcül hastalık teşhisinden sonra ailesine para bırakmak için suç dünyasına girer ve giderek geri dönüşü olmayan bir dönüşüm yaşar.",
                    Tur = IcerikTuru.Dizi,
                    Format = "SERIES",
                    Kategori = "Crime, Drama, Thriller",
                    Etiketler = "Anti-Hero, Crime, Family, Tension, Morality",
                    Durum = "FINISHED",
                    BaslangicYili = 2008,
                    BitisYili = 2013,
                    BolumSayisi = 62,
                    Studyo = "AMC",
                    Yaraticilar = "Vince Gilligan",
                    Kaynak = "ORIGINAL",
                    Skor = 96,
                    Populerlik = 980000,
                    ResimYolu = "https://static.tvmaze.com/uploads/images/original_untouched/0/2400.jpg",
                    BannerYolu = "https://static.tvmaze.com/uploads/images/original_untouched/496/1242160.jpg",
                    DisBaglanti = "https://www.tvmaze.com/shows/169/breaking-bad",
                    AnaKarakterler = "Walter White, Jesse Pinkman, Skyler White, Saul Goodman, Hank Schrader"
                },
                new Icerik
                {
                    Baslik = "Dark",
                    AlternatifBaslik = "Dark",
                    Aciklama = "Bir çocuğun ortadan kaybolması küçük bir Alman kasabasında dört aileyi zaman, kader ve kuşaklar arası günahlarla örülü karanlık bir bilmeceye sürükler.",
                    Tur = IcerikTuru.Dizi,
                    Format = "SERIES",
                    Kategori = "Mystery, Sci-Fi, Thriller",
                    Etiketler = "Time Travel, Family Mystery, Noir, Paradox, Suspense",
                    Durum = "FINISHED",
                    BaslangicYili = 2017,
                    BitisYili = 2020,
                    BolumSayisi = 26,
                    Studyo = "Netflix",
                    Yaraticilar = "Baran bo Odar, Jantje Friese",
                    Kaynak = "ORIGINAL",
                    Skor = 92,
                    Populerlik = 770000,
                    ResimYolu = "https://static.tvmaze.com/uploads/images/original_untouched/155/388026.jpg",
                    BannerYolu = "https://static.tvmaze.com/uploads/images/original_untouched/155/388027.jpg",
                    DisBaglanti = "https://www.tvmaze.com/shows/7036/dark",
                    AnaKarakterler = "Jonas Kahnwald, Martha Nielsen, Ulrich Nielsen, Claudia Tiedemann"
                },
                new Icerik
                {
                    Baslik = "The Sopranos",
                    AlternatifBaslik = "The Sopranos",
                    Aciklama = "New Jersey mafya patronu Tony Soprano, suç imparatorluğunu yönetirken aile hayatı ve psikolojik baskılar arasında sıkışır.",
                    Tur = IcerikTuru.Dizi,
                    Format = "SERIES",
                    Kategori = "Crime, Drama",
                    Etiketler = "Mafia, Family, Psychological, Classic, Character Study",
                    Durum = "FINISHED",
                    BaslangicYili = 1999,
                    BitisYili = 2007,
                    BolumSayisi = 86,
                    Studyo = "HBO",
                    Yaraticilar = "David Chase",
                    Kaynak = "ORIGINAL",
                    Skor = 95,
                    Populerlik = 650000,
                    ResimYolu = "https://static.tvmaze.com/uploads/images/original_untouched/4/11314.jpg",
                    BannerYolu = "https://static.tvmaze.com/uploads/images/original_untouched/4/11313.jpg",
                    DisBaglanti = "https://www.tvmaze.com/shows/527/the-sopranos",
                    AnaKarakterler = "Tony Soprano, Carmela Soprano, Christopher Moltisanti, Dr. Melfi"
                },
                new Icerik
                {
                    Baslik = "Game of Thrones",
                    AlternatifBaslik = "Game of Thrones",
                    Aciklama = "Hanedan savaşları, kadim tehditler ve demir taht mücadelesi; epik fantasy televizyonunun en büyük kırılma noktalarından biri.",
                    Tur = IcerikTuru.Dizi,
                    Format = "SERIES",
                    Kategori = "Fantasy, Drama, Action",
                    Etiketler = "Politics, War, Fantasy, Ensemble Cast, Epic",
                    Durum = "FINISHED",
                    BaslangicYili = 2011,
                    BitisYili = 2019,
                    BolumSayisi = 73,
                    Studyo = "HBO",
                    Yaraticilar = "David Benioff, D. B. Weiss",
                    Kaynak = "NOVEL",
                    Skor = 89,
                    Populerlik = 930000,
                    ResimYolu = "https://static.tvmaze.com/uploads/images/original_untouched/190/476117.jpg",
                    BannerYolu = "https://static.tvmaze.com/uploads/images/original_untouched/190/476118.jpg",
                    DisBaglanti = "https://www.tvmaze.com/shows/82/game-of-thrones",
                    AnaKarakterler = "Jon Snow, Daenerys Targaryen, Tyrion Lannister, Arya Stark"
                },
                new Icerik
                {
                    Baslik = "Sherlock",
                    AlternatifBaslik = "Sherlock",
                    Aciklama = "Arthur Conan Doyle'un dedektifini modern Londra'ya taşıyan yüksek tempolu yorum; zekâ düelloları, vaka çözümü ve karakter çatışması merkezde.",
                    Tur = IcerikTuru.Dizi,
                    Format = "SERIES",
                    Kategori = "Crime, Mystery, Drama",
                    Etiketler = "Detective, Modern Classic, Duo, Case Solving, British",
                    Durum = "FINISHED",
                    BaslangicYili = 2010,
                    BitisYili = 2017,
                    BolumSayisi = 13,
                    Studyo = "BBC",
                    Yaraticilar = "Steven Moffat, Mark Gatiss",
                    Kaynak = "NOVEL",
                    Skor = 88,
                    Populerlik = 690000,
                    ResimYolu = "https://static.tvmaze.com/uploads/images/original_untouched/82/205120.jpg",
                    BannerYolu = "https://static.tvmaze.com/uploads/images/original_untouched/82/205119.jpg",
                    DisBaglanti = "https://www.tvmaze.com/shows/335/sherlock",
                    AnaKarakterler = "Sherlock Holmes, John Watson, Jim Moriarty, Mycroft Holmes"
                },
                new Icerik
                {
                    Baslik = "True Detective",
                    AlternatifBaslik = "True Detective",
                    Aciklama = "Her sezon farklı bir suç dosyasına, farklı karakterlere ve ağır atmosferli bir anlatıma odaklanan modern suç antolojisi.",
                    Tur = IcerikTuru.Dizi,
                    Format = "SERIES",
                    Kategori = "Crime, Mystery, Drama",
                    Etiketler = "Anthology, Detective, Noir, Psychological, Prestige TV",
                    Durum = "RELEASING",
                    BaslangicYili = 2014,
                    BitisYili = 2024,
                    BolumSayisi = 30,
                    Studyo = "HBO",
                    Yaraticilar = "Nic Pizzolatto, Issa López",
                    Kaynak = "ORIGINAL",
                    Skor = 87,
                    Populerlik = 560000,
                    ResimYolu = "https://static.tvmaze.com/uploads/images/original_untouched/452/1131028.jpg",
                    BannerYolu = "https://static.tvmaze.com/uploads/images/original_untouched/452/1131027.jpg",
                    DisBaglanti = "https://www.tvmaze.com/shows/1505/true-detective",
                    AnaKarakterler = "Rust Cohle, Marty Hart, Wayne Hays, Liz Danvers"
                },
                new Icerik
                {
                    Baslik = "Stranger Things",
                    AlternatifBaslik = "Stranger Things",
                    Aciklama = "Küçük bir kasabada kaybolan çocuk vakası, deneyler, başka bir boyut ve büyüme hikâyesiyle birleşerek modern pop kültür ikonuna dönüşür.",
                    Tur = IcerikTuru.Dizi,
                    Format = "SERIES",
                    Kategori = "Sci-Fi, Horror, Adventure",
                    Etiketler = "Coming of Age, 80s, Mystery, Friendship, Supernatural",
                    Durum = "RELEASING",
                    BaslangicYili = 2016,
                    BitisYili = 2025,
                    BolumSayisi = 34,
                    Studyo = "Netflix",
                    Yaraticilar = "The Duffer Brothers",
                    Kaynak = "ORIGINAL",
                    Skor = 86,
                    Populerlik = 890000,
                    ResimYolu = "https://static.tvmaze.com/uploads/images/original_untouched/200/501942.jpg",
                    BannerYolu = "https://static.tvmaze.com/uploads/images/original_untouched/200/501943.jpg",
                    DisBaglanti = "https://www.tvmaze.com/shows/2993/stranger-things",
                    AnaKarakterler = "Eleven, Mike Wheeler, Dustin Henderson, Jim Hopper, Steve Harrington"
                },
                new Icerik
                {
                    Baslik = "The Wire",
                    AlternatifBaslik = "The Wire",
                    Aciklama = "Baltimore'un kurumlarını, sokaklarını ve sistemik çöküşünü çok katmanlı anlatan kült suç dizisi.",
                    Tur = IcerikTuru.Dizi,
                    Format = "SERIES",
                    Kategori = "Crime, Drama",
                    Etiketler = "System Critique, Police, Politics, Realism, Ensemble Cast",
                    Durum = "FINISHED",
                    BaslangicYili = 2002,
                    BitisYili = 2008,
                    BolumSayisi = 60,
                    Studyo = "HBO",
                    Yaraticilar = "David Simon",
                    Kaynak = "ORIGINAL",
                    Skor = 97,
                    Populerlik = 610000,
                    ResimYolu = "https://static.tvmaze.com/uploads/images/original_untouched/4/11048.jpg",
                    BannerYolu = "https://static.tvmaze.com/uploads/images/original_untouched/4/11047.jpg",
                    DisBaglanti = "https://www.tvmaze.com/shows/179/the-wire",
                    AnaKarakterler = "Jimmy McNulty, Omar Little, Stringer Bell, Bunk Moreland"
                },
                new Icerik
                {
                    Baslik = "Better Call Saul",
                    AlternatifBaslik = "Better Call Saul",
                    Aciklama = "Saul Goodman'ın köken hikâyesi; hukuk, suç ve kimlik erozyonunu yavaş ama çok kontrollü bir ritimle anlatan prestij dizi.",
                    Tur = IcerikTuru.Dizi,
                    Format = "SERIES",
                    Kategori = "Crime, Drama",
                    Etiketler = "Lawyer, Crime, Slow Burn, Character Study, Spin-off",
                    Durum = "FINISHED",
                    BaslangicYili = 2015,
                    BitisYili = 2022,
                    BolumSayisi = 63,
                    Studyo = "AMC",
                    Yaraticilar = "Vince Gilligan, Peter Gould",
                    Kaynak = "ORIGINAL",
                    Skor = 94,
                    Populerlik = 730000,
                    ResimYolu = "https://static.tvmaze.com/uploads/images/original_untouched/386/965470.jpg",
                    BannerYolu = "https://static.tvmaze.com/uploads/images/original_untouched/386/965469.jpg",
                    DisBaglanti = "https://www.tvmaze.com/shows/618/better-call-saul",
                    AnaKarakterler = "Jimmy McGill, Kim Wexler, Mike Ehrmantraut, Nacho Varga"
                },
                new Icerik
                {
                    Baslik = "Mindhunter",
                    AlternatifBaslik = "Mindhunter",
                    Aciklama = "FBI profil çıkarma biriminin ilk dönemlerine odaklanan, seri katil psikolojisini ve kurumsal gerilimi öne çıkaran karanlık suç dizisi.",
                    Tur = IcerikTuru.Dizi,
                    Format = "SERIES",
                    Kategori = "Crime, Drama, Thriller",
                    Etiketler = "Serial Killer, Investigation, Psychological, FBI, Dialogue Heavy",
                    Durum = "HIATUS",
                    BaslangicYili = 2017,
                    BitisYili = 2019,
                    BolumSayisi = 19,
                    Studyo = "Netflix",
                    Yaraticilar = "Joe Penhall, David Fincher",
                    Kaynak = "BOOK",
                    Skor = 90,
                    Populerlik = 520000,
                    ResimYolu = "https://static.tvmaze.com/uploads/images/original_untouched/164/412749.jpg",
                    BannerYolu = "https://static.tvmaze.com/uploads/images/original_untouched/164/412748.jpg",
                    DisBaglanti = "https://www.tvmaze.com/shows/27391/mindhunter",
                    AnaKarakterler = "Holden Ford, Bill Tench, Wendy Carr"
                }
            };
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
