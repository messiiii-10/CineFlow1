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
            await EnsureUserActivitySchemaAsync(dbContext);
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

            await EnsureCuratedCatalogExpansionAsync(dbContext);
            EnsureCuratedCatalogImageCorrections(dbContext);

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

        private static async Task EnsureUserActivitySchemaAsync(AppDbContext dbContext)
        {
            var connection = dbContext.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;

            if (shouldClose)
                await connection.OpenAsync();

            try
            {
                var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                await using var pragmaCommand = connection.CreateCommand();
                pragmaCommand.CommandText = "PRAGMA table_info(\"KullaniciIcerikKayitlari\");";

                await using var reader = await pragmaCommand.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    columns.Add(reader.GetString(1));

                if (!columns.Contains("KutuphanedeMi"))
                    await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE \"KullaniciIcerikKayitlari\" ADD COLUMN \"KutuphanedeMi\" INTEGER NOT NULL DEFAULT 1;");

                if (!columns.Contains("FavoriMi"))
                    await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE \"KullaniciIcerikKayitlari\" ADD COLUMN \"FavoriMi\" INTEGER NOT NULL DEFAULT 0;");

                if (!columns.Contains("SonZiyaretTarihi"))
                    await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE \"KullaniciIcerikKayitlari\" ADD COLUMN \"SonZiyaretTarihi\" TEXT NULL;");

                if (!columns.Contains("ZiyaretSayisi"))
                    await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE \"KullaniciIcerikKayitlari\" ADD COLUMN \"ZiyaretSayisi\" INTEGER NOT NULL DEFAULT 0;");
            }
            finally
            {
                if (shouldClose)
                    await connection.CloseAsync();
            }
        }

        private static Task EnsureCuratedCatalogExpansionAsync(AppDbContext dbContext)
        {
            var mevcutBasliklar = dbContext.Icerikler
                .AsEnumerable()
                .Select(BuildCatalogKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var eklenecekler = BuildCuratedCatalogExpansion()
                .Where(x => !mevcutBasliklar.Contains(BuildCatalogKey(x)))
                .ToList();

            if (eklenecekler.Count > 0)
                dbContext.Icerikler.AddRange(eklenecekler);

            return Task.CompletedTask;
        }

        private static string BuildCatalogKey(Icerik item)
            => $"{item.Baslik.Trim()}::{(int)item.Tur}";

        private static void EnsureCuratedCatalogImageCorrections(AppDbContext dbContext)
        {
            var posterUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Arcane"] = "https://static.tvmaze.com/uploads/images/original_untouched/536/1340287.jpg",
                ["Severance"] = "https://static.tvmaze.com/uploads/images/original_untouched/548/1371406.jpg",
                ["Mr. Robot"] = "https://static.tvmaze.com/uploads/images/original_untouched/211/528026.jpg",
                ["The Leftovers"] = "https://static.tvmaze.com/uploads/images/original_untouched/503/1259794.jpg",
                ["Babylon Berlin"] = "https://static.tvmaze.com/uploads/images/original_untouched/579/1448426.jpg",
                ["Blue Eye Samurai"] = "https://static.tvmaze.com/uploads/images/original_untouched/488/1220768.jpg",
                ["The Bear"] = "https://static.tvmaze.com/uploads/images/original_untouched/626/1567246.jpg",
                ["Frieren: Beyond Journey's End"] = "https://cdn.myanimelist.net/images/anime/1015/138006l.jpg",
                ["Odd Taxi"] = "https://cdn.myanimelist.net/images/anime/1981/113348l.jpg",
                ["Mushishi"] = "https://cdn.myanimelist.net/images/anime/2/73862l.jpg",
                ["Ping Pong the Animation"] = "https://cdn.myanimelist.net/images/anime/1586/146565l.jpg",
                ["Land of the Lustrous"] = "https://cdn.myanimelist.net/images/anime/3/88293l.jpg",
                ["The Tatami Galaxy"] = "https://cdn.myanimelist.net/images/anime/1633/123689l.jpg",
                ["Kaiba"] = "https://cdn.myanimelist.net/images/anime/1959/147908l.jpg",
                ["Baccano!"] = "https://cdn.myanimelist.net/images/anime/3/14547l.jpg",
                ["Succession"] = "https://static.tvmaze.com/uploads/images/original_untouched/453/1134275.jpg",
                ["Chernobyl"] = "https://static.tvmaze.com/uploads/images/original_untouched/193/482599.jpg",
                ["Fleabag"] = "https://static.tvmaze.com/uploads/images/original_untouched/192/482341.jpg",
                ["The Expanse"] = "https://static.tvmaze.com/uploads/images/original_untouched/445/1114081.jpg",
                ["Shouwa Genroku Rakugo Shinjuu"] = "https://cdn.myanimelist.net/images/anime/1354/124768l.jpg",
                ["Sonny Boy"] = "https://cdn.myanimelist.net/images/anime/1509/117149l.jpg",
                ["A Place Further Than the Universe"] = "https://cdn.myanimelist.net/images/anime/6/89879l.jpg",
                ["Haibane Renmei"] = "https://cdn.myanimelist.net/images/anime/9/13134l.jpg",
                ["Oyasumi Punpun"] = "https://cdn.myanimelist.net/images/manga/3/266834l.jpg",
                ["Dorohedoro"] = "https://cdn.myanimelist.net/images/manga/3/258246l.jpg",
                ["Girls' Last Tour"] = "https://cdn.myanimelist.net/images/manga/1/185918l.jpg",
                ["Blame!"] = "https://cdn.myanimelist.net/images/manga/1/174389l.jpg",
                ["Spirited Away"] = "https://cdn.myanimelist.net/images/anime/6/79597l.jpg",
                ["Perfect Blue"] = "https://cdn.myanimelist.net/images/anime/1254/134212l.jpg",
                ["Paprika"] = "https://cdn.myanimelist.net/images/anime/1929/93629l.jpg",
                ["Princess Mononoke"] = "https://cdn.myanimelist.net/images/anime/1355/147277l.jpg",
                ["Texhnolyze"] = "https://cdn.myanimelist.net/images/anime/1027/131977l.jpg",
                ["Ergo Proxy"] = "https://cdn.myanimelist.net/images/anime/1183/136187l.jpg",
                ["Katanagatari"] = "https://cdn.myanimelist.net/images/anime/1112/119225l.jpg",
                ["Dennou Coil"] = "https://cdn.myanimelist.net/images/anime/5/12844l.jpg",
                ["Planetes"] = "https://cdn.myanimelist.net/images/anime/1209/142900l.jpg",
                ["Kaiji: Ultimate Survivor"] = "https://cdn.myanimelist.net/images/anime/12/80032l.jpg",
                ["Serial Experiments Lain"] = "https://cdn.myanimelist.net/images/anime/1718/91550l.jpg",
                ["Paranoia Agent"] = "https://cdn.myanimelist.net/images/anime/7/10240l.jpg",
                ["Gankutsuou"] = "https://cdn.myanimelist.net/images/anime/1910/138560l.jpg",
                ["From the New World"] = "https://cdn.myanimelist.net/images/anime/1549/136389l.jpg",
                ["Mononoke"] = "https://cdn.myanimelist.net/images/anime/3/20713l.jpg",
                ["Moribito: Guardian of the Spirit"] = "https://cdn.myanimelist.net/images/anime/4/50337l.jpg",
                ["Space Brothers"] = "https://cdn.myanimelist.net/images/anime/1290/135694l.jpg",
                ["Revolutionary Girl Utena"] = "https://cdn.myanimelist.net/images/anime/1078/95285l.jpg",
                ["Rainbow"] = "https://cdn.myanimelist.net/images/anime/9/72697l.jpg",
                ["The Americans"] = "https://static.tvmaze.com/uploads/images/original_untouched/146/366911.jpg",
                ["Black Sails"] = "https://static.tvmaze.com/uploads/images/original_untouched/501/1253514.jpg",
                ["Twin Peaks"] = "https://static.tvmaze.com/uploads/images/original_untouched/397/992910.jpg",
                ["Millennium Actress"] = "https://cdn.myanimelist.net/images/anime/1648/93626l.jpg",
                ["Tokyo Godfathers"] = "https://cdn.myanimelist.net/images/anime/1480/132791l.jpg",
                ["Ghost in the Shell"] = "https://cdn.myanimelist.net/images/anime/10/82594l.jpg",
                ["Redline"] = "https://cdn.myanimelist.net/images/anime/12/28553l.jpg"
            };

            var items = dbContext.Icerikler
                .AsEnumerable()
                .Concat(dbContext.Icerikler.Local)
                .Distinct();

            foreach (var item in items)
            {
                if (posterUrls.TryGetValue(item.Baslik, out var posterUrl) && IsStockImagePath(item.ResimYolu))
                    item.ResimYolu = posterUrl;

                if (IsStockImagePath(item.BannerYolu))
                    item.BannerYolu = null;
            }
        }

        private static bool IsStockImagePath(string? imagePath)
            => !string.IsNullOrWhiteSpace(imagePath)
                && imagePath.Contains("images.unsplash.com", StringComparison.OrdinalIgnoreCase);

        private static List<Icerik> BuildCuratedCatalogExpansion()
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
                },
                new Icerik
                {
                    Baslik = "Arcane",
                    AlternatifBaslik = "Arcane",
                    Aciklama = "Piltover ile Zaun arasındaki sınıf çatışmasını iki kardeşin trajedisi üzerinden anlatan yüksek prodüksiyonlu animasyon dizisi.",
                    Tur = IcerikTuru.Dizi,
                    Format = "SERIES",
                    Kategori = "Action, Fantasy, Drama",
                    Etiketler = "Animation, Steampunk, Sisters, Revolution, Tragedy",
                    Durum = "RELEASING",
                    BaslangicYili = 2021,
                    BitisYili = 2024,
                    BolumSayisi = 18,
                    Studyo = "Fortiche, Netflix",
                    Yaraticilar = "Christian Linke, Alex Yee",
                    Kaynak = "VIDEO_GAME",
                    Skor = 93,
                    Populerlik = 760000,
                    ResimYolu = "https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1517604931442-7e0c8ed2963c?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://www.netflix.com/title/81435684",
                    AnaKarakterler = "Vi, Jinx, Caitlyn Kiramman, Jayce Talis"
                },
                new Icerik
                {
                    Baslik = "Severance",
                    AlternatifBaslik = "Severance",
                    Aciklama = "İş ve özel hayat anılarını ayıran bir şirket prosedürü, kurumsal distopyayı kişisel kimlik krizine dönüştürür.",
                    Tur = IcerikTuru.Dizi,
                    Format = "SERIES",
                    Kategori = "Sci-Fi, Thriller, Mystery",
                    Etiketler = "Office Horror, Corporate, Identity, Mystery Box, Slow Burn",
                    Durum = "RELEASING",
                    BaslangicYili = 2022,
                    BitisYili = 2025,
                    BolumSayisi = 19,
                    Studyo = "Apple TV+",
                    Yaraticilar = "Dan Erickson",
                    Kaynak = "ORIGINAL",
                    Skor = 91,
                    Populerlik = 480000,
                    ResimYolu = "https://images.unsplash.com/photo-1497032628192-86f99bcd76bc?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1497366754035-f200968a6e72?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://tv.apple.com/us/show/severance/umc.cmc.1srk2goyh2q2zdxcx605w8vtx",
                    AnaKarakterler = "Mark Scout, Helly Riggs, Irving Bailiff, Dylan George"
                },
                new Icerik
                {
                    Baslik = "Mr. Robot",
                    AlternatifBaslik = "Mr. Robot",
                    Aciklama = "Yalnız bir güvenlik mühendisi, kurumsal sisteme savaş açan hacker grubuyla birleşirken gerçeklik algısını da kaybetmeye başlar.",
                    Tur = IcerikTuru.Dizi,
                    Format = "SERIES",
                    Kategori = "Thriller, Drama, Psychological",
                    Etiketler = "Hacker, Paranoia, Anti-Hero, Cyberpunk, Mind Game",
                    Durum = "FINISHED",
                    BaslangicYili = 2015,
                    BitisYili = 2019,
                    BolumSayisi = 45,
                    Studyo = "USA Network",
                    Yaraticilar = "Sam Esmail",
                    Kaynak = "ORIGINAL",
                    Skor = 90,
                    Populerlik = 590000,
                    ResimYolu = "https://images.unsplash.com/photo-1516321318423-f06f85e504b3?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1510511459019-5dda7724fd87?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://www.tvmaze.com/shows/1871/mr-robot",
                    AnaKarakterler = "Elliot Alderson, Mr. Robot, Darlene, Angela Moss"
                },
                new Icerik
                {
                    Baslik = "The Leftovers",
                    AlternatifBaslik = "The Leftovers",
                    Aciklama = "Dünya nüfusunun bir kısmı aniden kaybolduktan sonra kalanların yas, inanç ve anlamsızlık duygusuyla mücadelesini takip eder.",
                    Tur = IcerikTuru.Dizi,
                    Format = "SERIES",
                    Kategori = "Drama, Mystery, Supernatural",
                    Etiketler = "Grief, Faith, Loss, Philosophical, Prestige TV",
                    Durum = "FINISHED",
                    BaslangicYili = 2014,
                    BitisYili = 2017,
                    BolumSayisi = 28,
                    Studyo = "HBO",
                    Yaraticilar = "Damon Lindelof, Tom Perrotta",
                    Kaynak = "NOVEL",
                    Skor = 92,
                    Populerlik = 310000,
                    ResimYolu = "https://images.unsplash.com/photo-1518998053901-5348d3961a04?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1500530855697-b586d89ba3ee?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://www.tvmaze.com/shows/978/the-leftovers",
                    AnaKarakterler = "Kevin Garvey, Nora Durst, Matt Jamison, Laurie Garvey"
                },
                new Icerik
                {
                    Baslik = "Babylon Berlin",
                    AlternatifBaslik = "Babylon Berlin",
                    Aciklama = "Weimar Cumhuriyeti döneminde suç, siyaset ve toplumsal çürüme arasında gezinen görkemli tarihsel noir.",
                    Tur = IcerikTuru.Dizi,
                    Format = "SERIES",
                    Kategori = "Crime, Drama, Historical",
                    Etiketler = "Period Drama, Noir, Politics, Germany, Conspiracy",
                    Durum = "RELEASING",
                    BaslangicYili = 2017,
                    BitisYili = 2025,
                    BolumSayisi = 40,
                    Studyo = "Sky Deutschland",
                    Yaraticilar = "Tom Tykwer, Achim von Borries, Henk Handloegten",
                    Kaynak = "NOVEL",
                    Skor = 88,
                    Populerlik = 190000,
                    ResimYolu = "https://images.unsplash.com/photo-1485846234645-a62644f84728?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1478720568477-152d9b164e26?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://www.imdb.com/title/tt4378376/",
                    AnaKarakterler = "Gereon Rath, Charlotte Ritter, Alfred Nyssen, Helga Rath"
                },
                new Icerik
                {
                    Baslik = "Blue Eye Samurai",
                    AlternatifBaslik = "Blue Eye Samurai",
                    Aciklama = "İntikam, kimlik ve şiddet estetiğini Edo dönemi Japonya'sında işleyen stilize animasyon macerası.",
                    Tur = IcerikTuru.Dizi,
                    Format = "SERIES",
                    Kategori = "Action, Adventure, Historical",
                    Etiketler = "Revenge, Samurai, Mature, Animation, Journey",
                    Durum = "RELEASING",
                    BaslangicYili = 2023,
                    BitisYili = 2026,
                    BolumSayisi = 8,
                    Studyo = "Netflix",
                    Yaraticilar = "Michael Green, Amber Noizumi",
                    Kaynak = "ORIGINAL",
                    Skor = 89,
                    Populerlik = 240000,
                    ResimYolu = "https://images.unsplash.com/photo-1505664194779-8beaceb93744?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1524412529635-a258ed66c010?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://www.netflix.com/title/81144203",
                    AnaKarakterler = "Mizu, Ringo, Taigen, Akemi"
                },
                new Icerik
                {
                    Baslik = "The Bear",
                    AlternatifBaslik = "The Bear",
                    Aciklama = "Kaotik bir mutfağın içinde yas, emek ve mükemmeliyetçiliği nefes kesen tempoyla anlatan modern karakter draması.",
                    Tur = IcerikTuru.Dizi,
                    Format = "SERIES",
                    Kategori = "Drama, Comedy",
                    Etiketler = "Kitchen, Anxiety, Family, Workplace, Character Growth",
                    Durum = "RELEASING",
                    BaslangicYili = 2022,
                    BitisYili = 2025,
                    BolumSayisi = 28,
                    Studyo = "FX",
                    Yaraticilar = "Christopher Storer",
                    Kaynak = "ORIGINAL",
                    Skor = 89,
                    Populerlik = 410000,
                    ResimYolu = "https://images.unsplash.com/photo-1559339352-11d035aa65de?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1414235077428-338989a2e8c0?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://www.fxnetworks.com/shows/the-bear",
                    AnaKarakterler = "Carmen Berzatto, Sydney Adamu, Richie Jerimovich, Marcus Brooks"
                },
                new Icerik
                {
                    Baslik = "Frieren: Beyond Journey's End",
                    AlternatifBaslik = "Sousou no Frieren",
                    OrijinalBaslik = "Sousou no Frieren",
                    Aciklama = "Şeytan kralı yenildikten sonraki boşlukta, ölümlülük ve hatırlama üzerine kurulmuş sakin ama etkileyici bir fantasy yolculuğu.",
                    Tur = IcerikTuru.Anime,
                    Format = "TV",
                    Kategori = "Adventure, Drama, Fantasy",
                    Etiketler = "Post-Adventure, Elves, Melancholy, Magic, Character Journey",
                    Durum = "RELEASING",
                    BaslangicYili = 2023,
                    BitisYili = 2026,
                    BolumSayisi = 28,
                    Studyo = "Madhouse",
                    Yaraticilar = "Kanehito Yamada, Tsukasa Abe",
                    Kaynak = "MANGA",
                    Skor = 95,
                    Populerlik = 540000,
                    ResimYolu = "https://images.unsplash.com/photo-1518709268805-4e9042af2176?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1500534314209-a25ddb2bd429?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/52991/Sousou_no_Frieren",
                    AnaKarakterler = "Frieren, Fern, Stark, Himmel"
                },
                new Icerik
                {
                    Baslik = "Odd Taxi",
                    AlternatifBaslik = "Odd Taxi",
                    OrijinalBaslik = "ODDTAXI",
                    Aciklama = "Bir taksi şoförünün küçük görünen rotaları, kayıp bir kız vakası ve suç ağlarıyla birbirine bağlanan sürprizli bir şehir bilmecesine dönüşür.",
                    Tur = IcerikTuru.Anime,
                    Format = "TV",
                    Kategori = "Mystery, Drama, Thriller",
                    Etiketler = "Dialogue Heavy, Urban, Ensemble, Crime, Mystery",
                    Durum = "FINISHED",
                    BaslangicYili = 2021,
                    BitisYili = 2021,
                    BolumSayisi = 13,
                    Studyo = "OLM, P.I.C.S.",
                    Yaraticilar = "Baku Kinoshita, Kazuya Konomoto",
                    Kaynak = "ORIGINAL",
                    Skor = 89,
                    Populerlik = 170000,
                    ResimYolu = "https://images.unsplash.com/photo-1511497584788-876760111969?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1516321497487-e288fb19713f?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/46102/Odd_Taxi",
                    AnaKarakterler = "Hiroshi Odokawa, Shirakawa, Kakihana, Dobu"
                },
                new Icerik
                {
                    Baslik = "Mushishi",
                    AlternatifBaslik = "Mushishi",
                    OrijinalBaslik = "Mushi-shi",
                    Aciklama = "Doğaüstü yaşam formlarının insan hayatıyla kesiştiği sakin, şiirsel ve meditatif episodik anlatı.",
                    Tur = IcerikTuru.Anime,
                    Format = "TV",
                    Kategori = "Fantasy, Slice of Life, Mystery",
                    Etiketler = "Atmospheric, Nature, Episodic, Iyashikei, Folklore",
                    Durum = "FINISHED",
                    BaslangicYili = 2005,
                    BitisYili = 2014,
                    BolumSayisi = 46,
                    Studyo = "Artland",
                    Yaraticilar = "Yuki Urushibara",
                    Kaynak = "MANGA",
                    Skor = 92,
                    Populerlik = 210000,
                    ResimYolu = "https://images.unsplash.com/photo-1470115636492-6d2b56f9146d?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1506744038136-46273834b3fb?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/457/Mushishi",
                    AnaKarakterler = "Ginko"
                },
                new Icerik
                {
                    Baslik = "Ping Pong the Animation",
                    AlternatifBaslik = "Ping Pong the Animation",
                    OrijinalBaslik = "Ping Pong THE ANIMATION",
                    Aciklama = "Sporun dış görünüşünden çok tutku, rekabet ve büyüme sancılarına odaklanan stil sahibi bir karakter draması.",
                    Tur = IcerikTuru.Anime,
                    Format = "TV",
                    Kategori = "Sports, Drama, Psychological",
                    Etiketler = "Table Tennis, Coming of Age, Unique Visuals, Rivalry, Growth",
                    Durum = "FINISHED",
                    BaslangicYili = 2014,
                    BitisYili = 2014,
                    BolumSayisi = 11,
                    Studyo = "Tatsunoko Production",
                    Yaraticilar = "Taiyo Matsumoto, Masaaki Yuasa",
                    Kaynak = "MANGA",
                    Skor = 90,
                    Populerlik = 150000,
                    ResimYolu = "https://images.unsplash.com/photo-1521412644187-c49fa049e84d?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1517649763962-0c623066013b?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/22135/Ping_Pong_the_Animation",
                    AnaKarakterler = "Peco, Smile, Kong Wenge, Kazama"
                },
                new Icerik
                {
                    Baslik = "Land of the Lustrous",
                    AlternatifBaslik = "Houseki no Kuni",
                    OrijinalBaslik = "Houseki no Kuni",
                    Aciklama = "Kırılgan bedenler, kimlik arayışı ve varoluş kaygısını parlak görsellikle birleştiren farklı bir fantasy anime.",
                    Tur = IcerikTuru.Anime,
                    Format = "TV",
                    Kategori = "Action, Drama, Fantasy",
                    Etiketler = "Identity, Existential, Gemstones, 3D CG, Melancholy",
                    Durum = "FINISHED",
                    BaslangicYili = 2017,
                    BitisYili = 2017,
                    BolumSayisi = 12,
                    Studyo = "Orange",
                    Yaraticilar = "Haruko Ichikawa",
                    Kaynak = "MANGA",
                    Skor = 87,
                    Populerlik = 190000,
                    ResimYolu = "https://images.unsplash.com/photo-1511497584788-876760111969?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1514525253161-7a46d19cd819?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/35557/Houseki_no_Kuni",
                    AnaKarakterler = "Phosphophyllite, Cinnabar, Bort, Diamond"
                },
                new Icerik
                {
                    Baslik = "The Tatami Galaxy",
                    AlternatifBaslik = "Yojouhan Shinwa Taikei",
                    OrijinalBaslik = "Yojouhan Shinwa Taikei",
                    Aciklama = "Üniversite hayatının sonsuz alternatiflerini hiperaktif anlatım ve keskin mizahla dolaşan deneysel bir gençlik öyküsü.",
                    Tur = IcerikTuru.Anime,
                    Format = "TV",
                    Kategori = "Comedy, Romance, Psychological",
                    Etiketler = "College, Surreal, Dialogue Heavy, Time Loop, Experimental",
                    Durum = "FINISHED",
                    BaslangicYili = 2010,
                    BitisYili = 2010,
                    BolumSayisi = 11,
                    Studyo = "Madhouse",
                    Yaraticilar = "Tomihiko Morimi, Masaaki Yuasa",
                    Kaynak = "NOVEL",
                    Skor = 91,
                    Populerlik = 160000,
                    ResimYolu = "https://images.unsplash.com/photo-1516321318423-f06f85e504b3?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1495567720989-cebdbdd97913?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/7785/Yojouhan_Shinwa_Taikei",
                    AnaKarakterler = "Watashi, Ozu, Akashi"
                },
                new Icerik
                {
                    Baslik = "Kaiba",
                    AlternatifBaslik = "Kaiba",
                    OrijinalBaslik = "Kaiba",
                    Aciklama = "Anıların depolanabildiği bir gelecekte beden, sınıf ve sevgi kavramlarını çocuksu görünümlü ama sert bir bilimkurguya dönüştürür.",
                    Tur = IcerikTuru.Anime,
                    Format = "TV",
                    Kategori = "Adventure, Mystery, Psychological",
                    Etiketler = "Memory, Sci-Fi, Identity, Experimental, Tragedy",
                    Durum = "FINISHED",
                    BaslangicYili = 2008,
                    BitisYili = 2008,
                    BolumSayisi = 12,
                    Studyo = "Madhouse",
                    Yaraticilar = "Masaaki Yuasa",
                    Kaynak = "ORIGINAL",
                    Skor = 86,
                    Populerlik = 95000,
                    ResimYolu = "https://images.unsplash.com/photo-1500534623283-312aade485b7?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1506748686214-e9df14d4d9d0?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/3701/Kaiba",
                    AnaKarakterler = "Kaiba, Neiro, Popo"
                },
                new Icerik
                {
                    Baslik = "Baccano!",
                    AlternatifBaslik = "Baccano!",
                    OrijinalBaslik = "Baccano!",
                    Aciklama = "Çapraz zaman çizgileri, gangsterler, ölümsüzlük ve çılgın enerjiyle akan kaotik ama çok eğlenceli bir dönem anlatısı.",
                    Tur = IcerikTuru.Anime,
                    Format = "TV",
                    Kategori = "Action, Mystery, Supernatural",
                    Etiketler = "Nonlinear, Mafia, Ensemble Cast, Immortality, Stylish",
                    Durum = "FINISHED",
                    BaslangicYili = 2007,
                    BitisYili = 2008,
                    BolumSayisi = 16,
                    Studyo = "Brain's Base",
                    Yaraticilar = "Ryohgo Narita",
                    Kaynak = "LIGHT_NOVEL",
                    Skor = 88,
                    Populerlik = 220000,
                    ResimYolu = "https://images.unsplash.com/photo-1513104890138-7c749659a591?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1487180144351-b8472da7d491?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/2251/Baccano",
                    AnaKarakterler = "Isaac Dian, Miria Harvent, Firo Prochainezo, Claire Stanfield"
                },
                new Icerik
                {
                    Baslik = "Succession",
                    AlternatifBaslik = "Succession",
                    Aciklama = "Medya imparatorluğu içindeki güç savaşlarını zehirli aile ilişkileri ve kurumsal entrikalar üzerinden anlatan modern dramedi.",
                    Tur = IcerikTuru.Dizi,
                    Format = "SERIES",
                    Kategori = "Drama, Comedy",
                    Etiketler = "Family, Corporate, Power Struggle, Prestige TV, Satire",
                    Durum = "FINISHED",
                    BaslangicYili = 2018,
                    BitisYili = 2023,
                    BolumSayisi = 39,
                    Studyo = "HBO",
                    Yaraticilar = "Jesse Armstrong",
                    Kaynak = "ORIGINAL",
                    Skor = 94,
                    Populerlik = 470000,
                    ResimYolu = "https://images.unsplash.com/photo-1520607162513-77705c0f0d4a?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1497366412874-3415097a27e7?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://www.hbo.com/succession",
                    AnaKarakterler = "Logan Roy, Kendall Roy, Shiv Roy, Roman Roy, Tom Wambsgans"
                },
                new Icerik
                {
                    Baslik = "Chernobyl",
                    AlternatifBaslik = "Chernobyl",
                    Aciklama = "1986 faciasını bürokrasi, fedakârlık ve devlet yalanları üzerinden sert ve sarsıcı bir mini diziye dönüştüren yapım.",
                    Tur = IcerikTuru.Dizi,
                    Format = "SERIES",
                    Kategori = "Drama, Historical, Thriller",
                    Etiketler = "Disaster, Politics, Miniseries, Tension, Real Events",
                    Durum = "FINISHED",
                    BaslangicYili = 2019,
                    BitisYili = 2019,
                    BolumSayisi = 5,
                    Studyo = "HBO, Sky",
                    Yaraticilar = "Craig Mazin",
                    Kaynak = "ORIGINAL",
                    Skor = 95,
                    Populerlik = 620000,
                    ResimYolu = "https://images.unsplash.com/photo-1516849841032-87cbac4d88f7?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1513828583688-c52646db42da?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://www.hbo.com/chernobyl",
                    AnaKarakterler = "Valery Legasov, Boris Shcherbina, Ulana Khomyuk"
                },
                new Icerik
                {
                    Baslik = "Fleabag",
                    AlternatifBaslik = "Fleabag",
                    Aciklama = "Yas, suçluluk ve yakınlık krizini acımasız mizahla işleyen kısa ama çok yoğun karakter anlatısı.",
                    Tur = IcerikTuru.Dizi,
                    Format = "SERIES",
                    Kategori = "Comedy, Drama",
                    Etiketler = "Fourth Wall, Grief, Romance, British, Character Study",
                    Durum = "FINISHED",
                    BaslangicYili = 2016,
                    BitisYili = 2019,
                    BolumSayisi = 12,
                    Studyo = "BBC, Amazon",
                    Yaraticilar = "Phoebe Waller-Bridge",
                    Kaynak = "PLAY",
                    Skor = 92,
                    Populerlik = 360000,
                    ResimYolu = "https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://www.amazon.com/Fleabag/",
                    AnaKarakterler = "Fleabag, Claire, The Priest, Martin"
                },
                new Icerik
                {
                    Baslik = "The Expanse",
                    AlternatifBaslik = "The Expanse",
                    Aciklama = "Güneş sistemine yayılmış insanlığın siyasal dengelerini uzay operası, dedektiflik ve savaş gerilimiyle birleştiren bilimkurgu dizisi.",
                    Tur = IcerikTuru.Dizi,
                    Format = "SERIES",
                    Kategori = "Sci-Fi, Drama, Mystery",
                    Etiketler = "Space Opera, Politics, Mystery, Ensemble, Adaptation",
                    Durum = "FINISHED",
                    BaslangicYili = 2015,
                    BitisYili = 2022,
                    BolumSayisi = 62,
                    Studyo = "Syfy, Amazon",
                    Yaraticilar = "Mark Fergus, Hawk Ostby",
                    Kaynak = "NOVEL",
                    Skor = 90,
                    Populerlik = 320000,
                    ResimYolu = "https://images.unsplash.com/photo-1462331940025-496dfbfc7564?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1446776811953-b23d57bd21aa?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://www.amazon.com/The-Expanse/",
                    AnaKarakterler = "James Holden, Naomi Nagata, Amos Burton, Chrisjen Avasarala"
                },
                new Icerik
                {
                    Baslik = "Shouwa Genroku Rakugo Shinjuu",
                    AlternatifBaslik = "Showa Genroku Rakugo Shinju",
                    OrijinalBaslik = "Shouwa Genroku Rakugo Shinjuu",
                    Aciklama = "Rakugo sanatını kuşaklar boyunca izleyen, sahne performansını kişisel trajedi ve tarihsel dönüşümle birleştiren özel bir anime.",
                    Tur = IcerikTuru.Anime,
                    Format = "TV",
                    Kategori = "Drama, Historical, Performing Arts",
                    Etiketler = "Rakugo, Period Drama, Character Study, Mature, Tragedy",
                    Durum = "FINISHED",
                    BaslangicYili = 2016,
                    BitisYili = 2017,
                    BolumSayisi = 25,
                    Studyo = "Studio Deen",
                    Yaraticilar = "Haruko Kumota",
                    Kaynak = "MANGA",
                    Skor = 91,
                    Populerlik = 120000,
                    ResimYolu = "https://images.unsplash.com/photo-1503095396549-807759245b35?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1500530855697-b586d89ba3ee?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/28735/Shouwa_Genroku_Rakugo_Shinjuu",
                    AnaKarakterler = "Yakumo, Sukeroku, Konatsu"
                },
                new Icerik
                {
                    Baslik = "Sonny Boy",
                    AlternatifBaslik = "Sonny Boy",
                    OrijinalBaslik = "Sonny Boy",
                    Aciklama = "Başka boyutlara savrulan öğrenciler üzerinden aidiyet, özgürlük ve yabancılaşmayı deneysel bir dille işleyen anime.",
                    Tur = IcerikTuru.Anime,
                    Format = "TV",
                    Kategori = "Mystery, Psychological, Sci-Fi",
                    Etiketler = "Surreal, Coming of Age, Parallel Worlds, Experimental, Isolation",
                    Durum = "FINISHED",
                    BaslangicYili = 2021,
                    BitisYili = 2021,
                    BolumSayisi = 12,
                    Studyo = "Madhouse",
                    Yaraticilar = "Shingo Natsume",
                    Kaynak = "ORIGINAL",
                    Skor = 84,
                    Populerlik = 130000,
                    ResimYolu = "https://images.unsplash.com/photo-1500534314209-a25ddb2bd429?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1498050108023-c5249f4df085?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/48849/Sonny_Boy",
                    AnaKarakterler = "Nagara, Nozomi, Mizuho, Rajdhani"
                },
                new Icerik
                {
                    Baslik = "A Place Further Than the Universe",
                    AlternatifBaslik = "Sora yori mo Tooi Basho",
                    OrijinalBaslik = "Sora yori mo Tooi Basho",
                    Aciklama = "Gençlik enerjisini kayıp, dostluk ve keşif duygusuyla birleştirip Antarktika yolculuğuna dönüştüren parlak bir macera draması.",
                    Tur = IcerikTuru.Anime,
                    Format = "TV",
                    Kategori = "Adventure, Drama, Slice of Life",
                    Etiketler = "Friendship, Antarctica, Coming of Age, Travel, Emotional",
                    Durum = "FINISHED",
                    BaslangicYili = 2018,
                    BitisYili = 2018,
                    BolumSayisi = 13,
                    Studyo = "Madhouse",
                    Yaraticilar = "Atsuko Ishizuka",
                    Kaynak = "ORIGINAL",
                    Skor = 90,
                    Populerlik = 180000,
                    ResimYolu = "https://images.unsplash.com/photo-1517760444937-f6397edcbbcd?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1518546305927-5a555bb7020d?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/35839/Sora_yori_mo_Tooi_Basho",
                    AnaKarakterler = "Mari Tamaki, Shirase Kobuchizawa, Hinata Miyake, Yuzuki Shiraishi"
                },
                new Icerik
                {
                    Baslik = "Haibane Renmei",
                    AlternatifBaslik = "Haibane Renmei",
                    OrijinalBaslik = "Haibane Renmei",
                    Aciklama = "Meleksi varlıkların kapalı bir kasabada yaşadığı sessiz dünya üzerinden suçluluk, bağışlanma ve aidiyet temalarını işler.",
                    Tur = IcerikTuru.Anime,
                    Format = "TV",
                    Kategori = "Drama, Fantasy, Mystery",
                    Etiketler = "Afterlife, Redemption, Iyashikei, Symbolism, Atmospheric",
                    Durum = "FINISHED",
                    BaslangicYili = 2002,
                    BitisYili = 2002,
                    BolumSayisi = 13,
                    Studyo = "Radix",
                    Yaraticilar = "Yoshitoshi ABe",
                    Kaynak = "OTHER",
                    Skor = 85,
                    Populerlik = 90000,
                    ResimYolu = "https://images.unsplash.com/photo-1500530855697-b586d89ba3ee?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1501785888041-af3ef285b470?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/387/Haibane_Renmei",
                    AnaKarakterler = "Rakka, Reki, Kana, Nemu"
                },
                new Icerik
                {
                    Baslik = "Oyasumi Punpun",
                    AlternatifBaslik = "Goodnight Punpun",
                    OrijinalBaslik = "Oyasumi Punpun",
                    Aciklama = "Çocukluktan yetişkinliğe uzanan ağır bir iç çöküşü, sevgi ve kendine zarar verme döngüsüyle anlatan karanlık manga.",
                    Tur = IcerikTuru.Manga,
                    Format = "MANGA",
                    Kategori = "Drama, Psychological, Slice of Life",
                    Etiketler = "Depression, Coming of Age, Trauma, Mature, Tragedy",
                    Durum = "FINISHED",
                    BaslangicYili = 2007,
                    BitisYili = 2013,
                    CiltSayisi = 13,
                    Yaraticilar = "Inio Asano",
                    Kaynak = "ORIGINAL",
                    Skor = 93,
                    Populerlik = 260000,
                    ResimYolu = "https://images.unsplash.com/photo-1512820790803-83ca734da794?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1507842217343-583bb7270b66?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/manga/4632/Oyasumi_Punpun",
                    AnaKarakterler = "Punpun Onodera, Aiko Tanaka, Sachi Nanjou"
                },
                new Icerik
                {
                    Baslik = "Dorohedoro",
                    AlternatifBaslik = "Dorohedoro",
                    OrijinalBaslik = "Dorohedoro",
                    Aciklama = "Çamurlu şehir estetiği, kara mizah ve vahşi fanteziyi hafıza kaybı merkezli bir gizemle birleştiren manga.",
                    Tur = IcerikTuru.Manga,
                    Format = "MANGA",
                    Kategori = "Action, Fantasy, Horror",
                    Etiketler = "Dark Comedy, Gore, Mystery, Sorcerers, Chaos",
                    Durum = "FINISHED",
                    BaslangicYili = 2000,
                    BitisYili = 2018,
                    CiltSayisi = 23,
                    Yaraticilar = "Q Hayashida",
                    Kaynak = "ORIGINAL",
                    Skor = 89,
                    Populerlik = 175000,
                    ResimYolu = "https://images.unsplash.com/photo-1513104890138-7c749659a591?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1515879218367-8466d910aaa4?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/manga/1133/Dorohedoro",
                    AnaKarakterler = "Caiman, Nikaido, Shin, Noi, En"
                },
                new Icerik
                {
                    Baslik = "Girls' Last Tour",
                    AlternatifBaslik = "Shoujo Shuumatsu Ryokou",
                    OrijinalBaslik = "Shoujo Shuumatsu Ryokou",
                    Aciklama = "Medeniyet sonrası sessizlikte iki kızın yolculuğunu minimal ama felsefi bir tonda anlatan post-apokaliptik manga.",
                    Tur = IcerikTuru.Manga,
                    Format = "MANGA",
                    Kategori = "Adventure, Sci-Fi, Slice of Life",
                    Etiketler = "Post-Apocalyptic, Philosophical, Minimalist, Friendship, Melancholy",
                    Durum = "FINISHED",
                    BaslangicYili = 2014,
                    BitisYili = 2018,
                    CiltSayisi = 6,
                    Yaraticilar = "Tsukumizu",
                    Kaynak = "ORIGINAL",
                    Skor = 88,
                    Populerlik = 110000,
                    ResimYolu = "https://images.unsplash.com/photo-1473773508845-188df298d2d1?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1446776811953-b23d57bd21aa?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/manga/72467/Shoujo_Shuumatsu_Ryokou",
                    AnaKarakterler = "Chito, Yuuri"
                },
                new Icerik
                {
                    Baslik = "Blame!",
                    AlternatifBaslik = "Blame!",
                    OrijinalBaslik = "Blame!",
                    Aciklama = "Sonsuz megayapı içinde geçen, neredeyse kelimesiz anlatımı ve mimari dehşetiyle öne çıkan siberpunk manga.",
                    Tur = IcerikTuru.Manga,
                    Format = "MANGA",
                    Kategori = "Action, Sci-Fi, Horror",
                    Etiketler = "Cyberpunk, Architecture, Sparse Dialogue, Dystopia, Exploration",
                    Durum = "FINISHED",
                    BaslangicYili = 1997,
                    BitisYili = 2003,
                    CiltSayisi = 10,
                    Yaraticilar = "Tsutomu Nihei",
                    Kaynak = "ORIGINAL",
                    Skor = 87,
                    Populerlik = 140000,
                    ResimYolu = "https://images.unsplash.com/photo-1460661419201-fd4cecdf8a8b?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1462331940025-496dfbfc7564?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/manga/149/Blame",
                    AnaKarakterler = "Killy, Cibo, Sanakan"
                },
                new Icerik
                {
                    Baslik = "Spirited Away",
                    AlternatifBaslik = "Sen to Chihiro no Kamikakushi",
                    OrijinalBaslik = "Sen to Chihiro no Kamikakushi",
                    Aciklama = "Ruhlar dünyasında ailesini kurtarmaya çalışan bir çocuğun öyküsü üzerinden kimlik, cesaret ve dönüşüm anlatan klasik film.",
                    Tur = IcerikTuru.Film,
                    Format = "MOVIE",
                    Kategori = "Adventure, Fantasy, Family",
                    Etiketler = "Coming of Age, Spirits, Ghibli, Fantasy, Classic",
                    Durum = "FINISHED",
                    BaslangicYili = 2001,
                    BitisYili = 2001,
                    SureDakika = 125,
                    Studyo = "Studio Ghibli",
                    Yaraticilar = "Hayao Miyazaki",
                    Kaynak = "ORIGINAL",
                    Skor = 97,
                    Populerlik = 910000,
                    ResimYolu = "https://images.unsplash.com/photo-1502136969935-8d8eef54d77c?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1500530855697-b586d89ba3ee?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/199/Sen_to_Chihiro_no_Kamikakushi",
                    AnaKarakterler = "Chihiro Ogino, Haku, Yubaba, No-Face"
                },
                new Icerik
                {
                    Baslik = "Perfect Blue",
                    AlternatifBaslik = "Perfect Blue",
                    OrijinalBaslik = "Perfect Blue",
                    Aciklama = "Şöhret, gözetlenme ve benlik çözülmesini rahatsız edici bir psikolojik gerilime dönüştüren Satoshi Kon filmi.",
                    Tur = IcerikTuru.Film,
                    Format = "MOVIE",
                    Kategori = "Psychological, Thriller, Horror",
                    Etiketler = "Identity, Stalker, Psychological, Mature, Classic",
                    Durum = "FINISHED",
                    BaslangicYili = 1997,
                    BitisYili = 1997,
                    SureDakika = 81,
                    Studyo = "Madhouse",
                    Yaraticilar = "Satoshi Kon, Yoshikazu Takeuchi",
                    Kaynak = "NOVEL",
                    Skor = 91,
                    Populerlik = 430000,
                    ResimYolu = "https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1478720568477-152d9b164e26?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/437/Perfect_Blue",
                    AnaKarakterler = "Mima Kirigoe, Rumi, Me-Mania"
                },
                new Icerik
                {
                    Baslik = "Paprika",
                    AlternatifBaslik = "Paprika",
                    OrijinalBaslik = "Paprika",
                    Aciklama = "Rüyaların paylaşıldığı bir teknolojiyi görsel taşkınlık ve kimlik karmaşasıyla buluşturan yaratıcı bilimkurgu filmi.",
                    Tur = IcerikTuru.Film,
                    Format = "MOVIE",
                    Kategori = "Sci-Fi, Mystery, Psychological",
                    Etiketler = "Dreams, Surreal, Mind Game, Technology, Satoshi Kon",
                    Durum = "FINISHED",
                    BaslangicYili = 2006,
                    BitisYili = 2006,
                    SureDakika = 90,
                    Studyo = "Madhouse",
                    Yaraticilar = "Satoshi Kon, Yasutaka Tsutsui",
                    Kaynak = "NOVEL",
                    Skor = 89,
                    Populerlik = 350000,
                    ResimYolu = "https://images.unsplash.com/photo-1518770660439-4636190af475?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1516321497487-e288fb19713f?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/1943/Paprika",
                    AnaKarakterler = "Atsuko Chiba, Paprika, Kosaku Tokita, Osanai"
                },
                new Icerik
                {
                    Baslik = "Princess Mononoke",
                    AlternatifBaslik = "Mononoke Hime",
                    OrijinalBaslik = "Mononoke Hime",
                    Aciklama = "İnsan ile doğa arasındaki çatışmayı epik mitoloji ve çevresel duyarlılıkla işleyen görkemli anime filmi.",
                    Tur = IcerikTuru.Film,
                    Format = "MOVIE",
                    Kategori = "Action, Adventure, Fantasy",
                    Etiketler = "Nature, War, Mythology, Ghibli, Epic",
                    Durum = "FINISHED",
                    BaslangicYili = 1997,
                    BitisYili = 1997,
                    SureDakika = 133,
                    Studyo = "Studio Ghibli",
                    Yaraticilar = "Hayao Miyazaki",
                    Kaynak = "ORIGINAL",
                    Skor = 93,
                    Populerlik = 520000,
                    ResimYolu = "https://images.unsplash.com/photo-1517604931442-7e0c8ed2963c?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1500534314209-a25ddb2bd429?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/164/Mononoke_Hime",
                    AnaKarakterler = "Ashitaka, San, Lady Eboshi, Jigo"
                },
                new Icerik
                {
                    Baslik = "Texhnolyze",
                    AlternatifBaslik = "Texhnolyze",
                    OrijinalBaslik = "Texhnolyze",
                    Aciklama = "Yeraltı şehrinde beden modifikasyonu, çürüme ve anlamsızlık etrafında dönen ağır tempolu siberpunk kabus.",
                    Tur = IcerikTuru.Anime,
                    Format = "TV",
                    Kategori = "Sci-Fi, Psychological, Drama",
                    Etiketler = "Cyberpunk, Dystopia, Existential, Slow Burn, Bleak",
                    Durum = "FINISHED",
                    BaslangicYili = 2003,
                    BitisYili = 2003,
                    BolumSayisi = 22,
                    Studyo = "Madhouse",
                    Yaraticilar = "Chiaki J. Konaka, Yoshitoshi ABe",
                    Kaynak = "ORIGINAL",
                    Skor = 83,
                    Populerlik = 75000,
                    ResimYolu = "https://images.unsplash.com/photo-1518770660439-4636190af475?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1516321497487-e288fb19713f?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/26/Texhnolyze",
                    AnaKarakterler = "Ichise, Ran, Onishi, Yoshii"
                },
                new Icerik
                {
                    Baslik = "Ergo Proxy",
                    AlternatifBaslik = "Ergo Proxy",
                    OrijinalBaslik = "Ergo Proxy",
                    Aciklama = "Kapalı kubbe şehirlerinde kimlik, hafıza ve tanrısallığı soruşturan soğuk ve stil sahibi bilimkurgu.",
                    Tur = IcerikTuru.Anime,
                    Format = "TV",
                    Kategori = "Sci-Fi, Mystery, Psychological",
                    Etiketler = "Cyberpunk, Androids, Mystery, Philosophy, Dystopia",
                    Durum = "FINISHED",
                    BaslangicYili = 2006,
                    BitisYili = 2006,
                    BolumSayisi = 23,
                    Studyo = "Manglobe",
                    Yaraticilar = "Dai Sato, Shukou Murase",
                    Kaynak = "ORIGINAL",
                    Skor = 86,
                    Populerlik = 210000,
                    ResimYolu = "https://images.unsplash.com/photo-1515879218367-8466d910aaa4?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1510511459019-5dda7724fd87?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/790/Ergo_Proxy",
                    AnaKarakterler = "Re-l Mayer, Vincent Law, Pino, Iggy"
                },
                new Icerik
                {
                    Baslik = "Katanagatari",
                    AlternatifBaslik = "Katanagatari",
                    OrijinalBaslik = "Katanagatari",
                    Aciklama = "Her ay bir kılıcın peşine düşen ikilinin yolculuğunu parlak diyaloglar ve masalsı estetikle anlatan macera.",
                    Tur = IcerikTuru.Anime,
                    Format = "TV",
                    Kategori = "Action, Adventure, Romance",
                    Etiketler = "Samurai, Dialogue Heavy, Journey, Strategy, Tragedy",
                    Durum = "FINISHED",
                    BaslangicYili = 2010,
                    BitisYili = 2010,
                    BolumSayisi = 12,
                    Studyo = "White Fox",
                    Yaraticilar = "Nisio Isin, Take",
                    Kaynak = "NOVEL",
                    Skor = 87,
                    Populerlik = 180000,
                    ResimYolu = "https://images.unsplash.com/photo-1515169067868-5387ec356754?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1524412529635-a258ed66c010?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/6594/Katanagatari",
                    AnaKarakterler = "Shichika Yasuri, Togame"
                },
                new Icerik
                {
                    Baslik = "Dennou Coil",
                    AlternatifBaslik = "Den-noh Coil",
                    OrijinalBaslik = "Dennou Coil",
                    Aciklama = "Artırılmış gerçeklik gözlüklerinin sıradan hayatı ve çocukluk gizemini nasıl dönüştürdüğünü anlatan ileri görüşlü anime.",
                    Tur = IcerikTuru.Anime,
                    Format = "TV",
                    Kategori = "Adventure, Mystery, Sci-Fi",
                    Etiketler = "Augmented Reality, Childhood, Mystery, Tech, Urban Legend",
                    Durum = "FINISHED",
                    BaslangicYili = 2007,
                    BitisYili = 2007,
                    BolumSayisi = 26,
                    Studyo = "Madhouse",
                    Yaraticilar = "Mitsuo Iso",
                    Kaynak = "ORIGINAL",
                    Skor = 83,
                    Populerlik = 65000,
                    ResimYolu = "https://images.unsplash.com/photo-1498050108023-c5249f4df085?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1516321497487-e288fb19713f?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/2164/Dennou_Coil",
                    AnaKarakterler = "Yuuko Okonogi, Haraken, Isako"
                },
                new Icerik
                {
                    Baslik = "Planetes",
                    AlternatifBaslik = "Planetes",
                    OrijinalBaslik = "Planetes",
                    Aciklama = "Uzay çöpü toplayan işçilerin gündelik emeği üzerinden hayal, kariyer ve insanlığın geleceğine bakan sıcak bilimkurgu.",
                    Tur = IcerikTuru.Anime,
                    Format = "TV",
                    Kategori = "Drama, Romance, Sci-Fi",
                    Etiketler = "Space, Workplace, Hard Sci-Fi, Growth, Realism",
                    Durum = "FINISHED",
                    BaslangicYili = 2003,
                    BitisYili = 2004,
                    BolumSayisi = 26,
                    Studyo = "Sunrise",
                    Yaraticilar = "Makoto Yukimura",
                    Kaynak = "MANGA",
                    Skor = 87,
                    Populerlik = 120000,
                    ResimYolu = "https://images.unsplash.com/photo-1446776811953-b23d57bd21aa?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1462331940025-496dfbfc7564?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/329/Planetes",
                    AnaKarakterler = "Hachirota Hoshino, Ai Tanabe, Fee Carmichael"
                },
                new Icerik
                {
                    Baslik = "Kaiji: Ultimate Survivor",
                    AlternatifBaslik = "Gyakkyou Burai Kaiji: Ultimate Survivor",
                    OrijinalBaslik = "Gyakkyou Burai Kaiji: Ultimate Survivor",
                    Aciklama = "Borç batağındaki adamın ölümcül kumar oyunlarında ayakta kalma mücadelesi yüksek tansiyonlu psikolojik animeye dönüşür.",
                    Tur = IcerikTuru.Anime,
                    Format = "TV",
                    Kategori = "Psychological, Thriller, Sports",
                    Etiketler = "Gambling, Tension, Mind Games, Survival, Mature",
                    Durum = "FINISHED",
                    BaslangicYili = 2007,
                    BitisYili = 2011,
                    BolumSayisi = 52,
                    Studyo = "Madhouse",
                    Yaraticilar = "Nobuyuki Fukumoto",
                    Kaynak = "MANGA",
                    Skor = 84,
                    Populerlik = 145000,
                    ResimYolu = "https://images.unsplash.com/photo-1511512578047-dfb367046420?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1521412644187-c49fa049e84d?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/3002/Gyakkyou_Burai_Kaiji__Ultimate_Survivor",
                    AnaKarakterler = "Kaiji Itou"
                },
                new Icerik
                {
                    Baslik = "Serial Experiments Lain",
                    AlternatifBaslik = "Serial Experiments Lain",
                    OrijinalBaslik = "Serial Experiments Lain",
                    Aciklama = "İnternet, bilinç ve kişilik sınırlarını internet öncesi kaygılarla sorgulayan kült teknoloji kabusu.",
                    Tur = IcerikTuru.Anime,
                    Format = "TV",
                    Kategori = "Mystery, Psychological, Sci-Fi",
                    Etiketler = "Internet, Identity, Surreal, Cyberpunk, Experimental",
                    Durum = "FINISHED",
                    BaslangicYili = 1998,
                    BitisYili = 1998,
                    BolumSayisi = 13,
                    Studyo = "Triangle Staff",
                    Yaraticilar = "Yasuyuki Ueda, Chiaki J. Konaka",
                    Kaynak = "ORIGINAL",
                    Skor = 83,
                    Populerlik = 250000,
                    ResimYolu = "https://images.unsplash.com/photo-1516321318423-f06f85e504b3?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1510511459019-5dda7724fd87?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/339/Serial_Experiments_Lain",
                    AnaKarakterler = "Lain Iwakura, Alice Mizuki"
                },
                new Icerik
                {
                    Baslik = "Paranoia Agent",
                    AlternatifBaslik = "Mousou Dairinin",
                    OrijinalBaslik = "Mousou Dairinin",
                    Aciklama = "Bir şehir efsanesine bağlanan toplumsal panik ve bastırılmış arzular, Satoshi Kon'un çok katmanlı geriliminde birleşir.",
                    Tur = IcerikTuru.Anime,
                    Format = "TV",
                    Kategori = "Mystery, Psychological, Supernatural",
                    Etiketler = "Urban Legend, Society, Surreal, Mystery, Satoshi Kon",
                    Durum = "FINISHED",
                    BaslangicYili = 2004,
                    BitisYili = 2004,
                    BolumSayisi = 13,
                    Studyo = "Madhouse",
                    Yaraticilar = "Satoshi Kon",
                    Kaynak = "ORIGINAL",
                    Skor = 81,
                    Populerlik = 170000,
                    ResimYolu = "https://images.unsplash.com/photo-1478720568477-152d9b164e26?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/323/Mousou_Dairinin",
                    AnaKarakterler = "Lil' Slugger, Tsukiko Sagi, Keiichi Ikari"
                },
                new Icerik
                {
                    Baslik = "Gankutsuou",
                    AlternatifBaslik = "Gankutsuou: The Count of Monte Cristo",
                    OrijinalBaslik = "Gankutsuou",
                    Aciklama = "Monte Kristo Kontu'nu uzay aristokrasisi ve çılgın görsel desenlerle yeniden kuran gösterişli intikam anlatısı.",
                    Tur = IcerikTuru.Anime,
                    Format = "TV",
                    Kategori = "Drama, Mystery, Sci-Fi",
                    Etiketler = "Revenge, Aristocracy, Sci-Fi, Classic Adaptation, Stylish",
                    Durum = "FINISHED",
                    BaslangicYili = 2004,
                    BitisYili = 2005,
                    BolumSayisi = 24,
                    Studyo = "Gonzo",
                    Yaraticilar = "Mahiro Maeda, Alexandre Dumas",
                    Kaynak = "NOVEL",
                    Skor = 82,
                    Populerlik = 82000,
                    ResimYolu = "https://images.unsplash.com/photo-1500534623283-312aade485b7?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1495567720989-cebdbdd97913?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/239/Gankutsuou",
                    AnaKarakterler = "The Count of Monte Cristo, Albert de Morcerf, Franz d'Epinay"
                },
                new Icerik
                {
                    Baslik = "From the New World",
                    AlternatifBaslik = "Shinsekai yori",
                    OrijinalBaslik = "Shinsekai yori",
                    Aciklama = "Psişik güçlerin egemen olduğu gelecek toplumunda çocukluktan yetişkinliğe ilerleyen karanlık bir uygarlık eleştirisi.",
                    Tur = IcerikTuru.Anime,
                    Format = "TV",
                    Kategori = "Drama, Horror, Mystery",
                    Etiketler = "Dystopia, Coming of Age, Society, Horror, Ethics",
                    Durum = "FINISHED",
                    BaslangicYili = 2012,
                    BitisYili = 2013,
                    BolumSayisi = 25,
                    Studyo = "A-1 Pictures",
                    Yaraticilar = "Yusuke Kishi",
                    Kaynak = "NOVEL",
                    Skor = 84,
                    Populerlik = 180000,
                    ResimYolu = "https://images.unsplash.com/photo-1501785888041-af3ef285b470?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1500530855697-b586d89ba3ee?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/13125/Shinsekai_yori",
                    AnaKarakterler = "Saki Watanabe, Satoru Asahina, Maria Akizuki"
                },
                new Icerik
                {
                    Baslik = "Mononoke",
                    AlternatifBaslik = "Mononoke",
                    OrijinalBaslik = "Mononoke",
                    Aciklama = "Gezgin ilaç satıcısının doğaüstü vakaları çözdüğü, desen ve renk kullanımıyla benzersiz görünen folklor korkusu.",
                    Tur = IcerikTuru.Anime,
                    Format = "TV",
                    Kategori = "Fantasy, Horror, Mystery",
                    Etiketler = "Folklore, Stylized, Exorcism, Psychological, Art Direction",
                    Durum = "FINISHED",
                    BaslangicYili = 2007,
                    BitisYili = 2007,
                    BolumSayisi = 12,
                    Studyo = "Toei Animation",
                    Yaraticilar = "Kenji Nakamura",
                    Kaynak = "ORIGINAL",
                    Skor = 85,
                    Populerlik = 115000,
                    ResimYolu = "https://images.unsplash.com/photo-1500534314209-a25ddb2bd429?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1518709268805-4e9042af2176?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/2246/Mononoke",
                    AnaKarakterler = "Medicine Seller"
                },
                new Icerik
                {
                    Baslik = "Moribito: Guardian of the Spirit",
                    AlternatifBaslik = "Seirei no Moribito",
                    OrijinalBaslik = "Seirei no Moribito",
                    Aciklama = "Bir korumanın prensle çıktığı yolculuk, siyaset ve mitolojiyi olgun karakter yazımıyla bir araya getirir.",
                    Tur = IcerikTuru.Anime,
                    Format = "TV",
                    Kategori = "Action, Adventure, Fantasy",
                    Etiketler = "Bodyguard, Politics, Mythology, Journey, Mature Cast",
                    Durum = "FINISHED",
                    BaslangicYili = 2007,
                    BitisYili = 2007,
                    BolumSayisi = 26,
                    Studyo = "Production I.G",
                    Yaraticilar = "Nahoko Uehashi",
                    Kaynak = "NOVEL",
                    Skor = 84,
                    Populerlik = 98000,
                    ResimYolu = "https://images.unsplash.com/photo-1505664194779-8beaceb93744?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1524412529635-a258ed66c010?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/1827/Seirei_no_Moribito",
                    AnaKarakterler = "Balsa, Chagum, Tanda"
                },
                new Icerik
                {
                    Baslik = "Space Brothers",
                    AlternatifBaslik = "Uchuu Kyoudai",
                    OrijinalBaslik = "Uchuu Kyoudai",
                    Aciklama = "Çocukluk hayallerinin peşinden yetişkin yaşta giden iki kardeş üzerinden azim ve uzay tutkusunu sıcak anlatır.",
                    Tur = IcerikTuru.Anime,
                    Format = "TV",
                    Kategori = "Comedy, Drama, Sci-Fi",
                    Etiketler = "Space, Brothers, Career, Inspirational, Realistic",
                    Durum = "FINISHED",
                    BaslangicYili = 2012,
                    BitisYili = 2014,
                    BolumSayisi = 99,
                    Studyo = "A-1 Pictures",
                    Yaraticilar = "Chuya Koyama",
                    Kaynak = "MANGA",
                    Skor = 88,
                    Populerlik = 125000,
                    ResimYolu = "https://images.unsplash.com/photo-1446776811953-b23d57bd21aa?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1462331940025-496dfbfc7564?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/12431/Uchuu_Kyoudai",
                    AnaKarakterler = "Mutta Nanba, Hibito Nanba"
                },
                new Icerik
                {
                    Baslik = "Revolutionary Girl Utena",
                    AlternatifBaslik = "Shoujo Kakumei Utena",
                    OrijinalBaslik = "Shoujo Kakumei Utena",
                    Aciklama = "Düello, cinsiyet rolleri ve masal imgeleri üzerinden ilişkileri söken katmanlı bir sembolik anime klasiği.",
                    Tur = IcerikTuru.Anime,
                    Format = "TV",
                    Kategori = "Drama, Fantasy, Psychological",
                    Etiketler = "Symbolism, Duels, Gender, Surreal, Classic",
                    Durum = "FINISHED",
                    BaslangicYili = 1997,
                    BitisYili = 1997,
                    BolumSayisi = 39,
                    Studyo = "J.C.Staff",
                    Yaraticilar = "Kunihiko Ikuhara",
                    Kaynak = "ORIGINAL",
                    Skor = 85,
                    Populerlik = 98000,
                    ResimYolu = "https://images.unsplash.com/photo-1524412529635-a258ed66c010?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1495567720989-cebdbdd97913?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/440/Shoujo_Kakumei_Utena",
                    AnaKarakterler = "Utena Tenjou, Anthy Himemiya"
                },
                new Icerik
                {
                    Baslik = "Rainbow",
                    AlternatifBaslik = "Rainbow: Nisha Rokubou no Shichinin",
                    OrijinalBaslik = "Rainbow: Nisha Rokubou no Shichinin",
                    Aciklama = "Savaş sonrası Japonya'da ıslahevine düşen gençlerin dayanışma ve hayatta kalma hikâyesi sert bir drama yaratır.",
                    Tur = IcerikTuru.Anime,
                    Format = "TV",
                    Kategori = "Drama, Historical, Thriller",
                    Etiketler = "Prison, Brotherhood, Mature, Survival, Post-War",
                    Durum = "FINISHED",
                    BaslangicYili = 2010,
                    BitisYili = 2010,
                    BolumSayisi = 26,
                    Studyo = "Madhouse",
                    Yaraticilar = "George Abe, Masasumi Kakizaki",
                    Kaynak = "MANGA",
                    Skor = 84,
                    Populerlik = 110000,
                    ResimYolu = "https://images.unsplash.com/photo-1516849841032-87cbac4d88f7?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1513828583688-c52646db42da?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/6114/Rainbow__Nisha_Rokubou_no_Shichinin",
                    AnaKarakterler = "Mario Minakami, Joe Yokosuka, Sakuragi"
                },
                new Icerik
                {
                    Baslik = "The Americans",
                    AlternatifBaslik = "The Americans",
                    Aciklama = "Soğuk Savaş döneminde banliyö hayatı süren Sovyet ajan çiftin kimlik ve sadakat çatışması çok kontrollü bir gerilim kurar.",
                    Tur = IcerikTuru.Dizi,
                    Format = "SERIES",
                    Kategori = "Drama, Thriller, Spy",
                    Etiketler = "Espionage, Family, Cold War, Tension, Prestige TV",
                    Durum = "FINISHED",
                    BaslangicYili = 2013,
                    BitisYili = 2018,
                    BolumSayisi = 75,
                    Studyo = "FX",
                    Yaraticilar = "Joe Weisberg",
                    Kaynak = "ORIGINAL",
                    Skor = 93,
                    Populerlik = 210000,
                    ResimYolu = "https://images.unsplash.com/photo-1497366754035-f200968a6e72?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1520607162513-77705c0f0d4a?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://www.fxnetworks.com/shows/the-americans",
                    AnaKarakterler = "Elizabeth Jennings, Philip Jennings, Stan Beeman"
                },
                new Icerik
                {
                    Baslik = "Black Sails",
                    AlternatifBaslik = "Black Sails",
                    Aciklama = "Korsan mitolojisini karanlık siyaset, deniz savaşları ve karakter odaklı yükseliş hikâyeleriyle yeniden kurar.",
                    Tur = IcerikTuru.Dizi,
                    Format = "SERIES",
                    Kategori = "Adventure, Drama, Historical",
                    Etiketler = "Pirates, Politics, War, Treasure, Character Drama",
                    Durum = "FINISHED",
                    BaslangicYili = 2014,
                    BitisYili = 2017,
                    BolumSayisi = 38,
                    Studyo = "Starz",
                    Yaraticilar = "Jonathan E. Steinberg, Robert Levine",
                    Kaynak = "NOVEL",
                    Skor = 88,
                    Populerlik = 160000,
                    ResimYolu = "https://images.unsplash.com/photo-1507525428034-b723cf961d3e?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1500375592092-40eb2168fd21?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://www.starz.com/us/en/series/black-sails",
                    AnaKarakterler = "Captain Flint, John Silver, Eleanor Guthrie"
                },
                new Icerik
                {
                    Baslik = "Twin Peaks",
                    AlternatifBaslik = "Twin Peaks",
                    Aciklama = "Küçük kasaba cinayet soruşturması üzerinden absürt mizah, rüya mantığı ve Amerikan karanlığını birleştiren kült televizyon.",
                    Tur = IcerikTuru.Dizi,
                    Format = "SERIES",
                    Kategori = "Mystery, Drama, Supernatural",
                    Etiketler = "Surreal, Small Town, Dream Logic, Cult Classic, Investigation",
                    Durum = "FINISHED",
                    BaslangicYili = 1990,
                    BitisYili = 2017,
                    BolumSayisi = 48,
                    Studyo = "ABC, Showtime",
                    Yaraticilar = "David Lynch, Mark Frost",
                    Kaynak = "ORIGINAL",
                    Skor = 91,
                    Populerlik = 280000,
                    ResimYolu = "https://images.unsplash.com/photo-1500530855697-b586d89ba3ee?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1501785888041-af3ef285b470?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://www.sho.com/twin-peaks",
                    AnaKarakterler = "Dale Cooper, Laura Palmer, Audrey Horne"
                },
                new Icerik
                {
                    Baslik = "Millennium Actress",
                    AlternatifBaslik = "Sennen Joyuu",
                    OrijinalBaslik = "Sennen Joyuu",
                    Aciklama = "Bir oyuncunun hayatı ile oynadığı roller arasında akan hafıza nehrini şiirsel biçimde kuran Satoshi Kon filmi.",
                    Tur = IcerikTuru.Film,
                    Format = "MOVIE",
                    Kategori = "Drama, Fantasy, Romance",
                    Etiketler = "Memory, Cinema, Romance, Satoshi Kon, Melancholy",
                    Durum = "FINISHED",
                    BaslangicYili = 2001,
                    BitisYili = 2001,
                    SureDakika = 87,
                    Studyo = "Madhouse",
                    Yaraticilar = "Satoshi Kon",
                    Kaynak = "ORIGINAL",
                    Skor = 90,
                    Populerlik = 190000,
                    ResimYolu = "https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1478720568477-152d9b164e26?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/1033/Sennen_Joyuu",
                    AnaKarakterler = "Chiyoko Fujiwara, Genya Tachibana"
                },
                new Icerik
                {
                    Baslik = "Tokyo Godfathers",
                    AlternatifBaslik = "Tokyo Godfathers",
                    OrijinalBaslik = "Tokyo Godfathers",
                    Aciklama = "Yılbaşı gecesi bulunan bebeğin peşinde üç evsizin çıktığı yolculuk sıcak, komik ve kırık dökük bir şehir masalı kurar.",
                    Tur = IcerikTuru.Film,
                    Format = "MOVIE",
                    Kategori = "Comedy, Drama",
                    Etiketler = "Found Family, Christmas, Tokyo, Satoshi Kon, Road Trip",
                    Durum = "FINISHED",
                    BaslangicYili = 2003,
                    BitisYili = 2003,
                    SureDakika = 92,
                    Studyo = "Madhouse",
                    Yaraticilar = "Satoshi Kon",
                    Kaynak = "ORIGINAL",
                    Skor = 88,
                    Populerlik = 175000,
                    ResimYolu = "https://images.unsplash.com/photo-1515169067868-5387ec356754?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1520607162513-77705c0f0d4a?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/759/Tokyo_Godfathers",
                    AnaKarakterler = "Gin, Hana, Miyuki"
                },
                new Icerik
                {
                    Baslik = "Ghost in the Shell",
                    AlternatifBaslik = "Koukaku Kidoutai",
                    OrijinalBaslik = "Koukaku Kidoutai",
                    Aciklama = "Siber bedenler ve yapay bilinç üzerinden benliğin sınırlarını sorgulayan tür belirleyici anime filmi.",
                    Tur = IcerikTuru.Film,
                    Format = "MOVIE",
                    Kategori = "Action, Sci-Fi, Psychological",
                    Etiketler = "Cyberpunk, Identity, AI, Classic, Philosophy",
                    Durum = "FINISHED",
                    BaslangicYili = 1995,
                    BitisYili = 1995,
                    SureDakika = 82,
                    Studyo = "Production I.G",
                    Yaraticilar = "Mamoru Oshii, Masamune Shirow",
                    Kaynak = "MANGA",
                    Skor = 90,
                    Populerlik = 420000,
                    ResimYolu = "https://images.unsplash.com/photo-1518770660439-4636190af475?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1510511459019-5dda7724fd87?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/43/Koukaku_Kidoutai",
                    AnaKarakterler = "Motoko Kusanagi, Batou, The Puppet Master"
                },
                new Icerik
                {
                    Baslik = "Redline",
                    AlternatifBaslik = "Redline",
                    OrijinalBaslik = "Redline",
                    Aciklama = "Çılgın galaktik yarışları el çizimi aşırılıkla patlatan saf enerji dolu aksiyon filmi.",
                    Tur = IcerikTuru.Film,
                    Format = "MOVIE",
                    Kategori = "Action, Sci-Fi, Sports",
                    Etiketler = "Racing, Adrenaline, Stylized, Space, Cult",
                    Durum = "FINISHED",
                    BaslangicYili = 2009,
                    BitisYili = 2009,
                    SureDakika = 102,
                    Studyo = "Madhouse",
                    Yaraticilar = "Takeshi Koike",
                    Kaynak = "ORIGINAL",
                    Skor = 86,
                    Populerlik = 160000,
                    ResimYolu = "https://images.unsplash.com/photo-1502877338535-766e1452684a?auto=format&fit=crop&w=900&q=80",
                    BannerYolu = "https://images.unsplash.com/photo-1503376780353-7e6692767b70?auto=format&fit=crop&w=1600&q=80",
                    DisBaglanti = "https://myanimelist.net/anime/6675/Redline",
                    AnaKarakterler = "JP, Sonoshee McLaren, Frisbee"
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
