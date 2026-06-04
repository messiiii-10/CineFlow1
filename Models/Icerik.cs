using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;

namespace CineFlow.Models
{
    public enum IcerikTuru
    {
        Film = 1,
        Dizi = 2,
        Anime = 3,
        Manga = 4
    }

    public class Icerik
    {
        public int Id { get; set; }

        public int? AniListId { get; set; }

        [Required(ErrorMessage = "Başlık zorunludur.")]
        [StringLength(120, ErrorMessage = "Başlık en fazla 120 karakter olabilir.")]
        public string Baslik { get; set; } = string.Empty;

        [Required(ErrorMessage = "Açıklama zorunludur.")]
        [StringLength(8000, ErrorMessage = "Açıklama en fazla 8000 karakter olabilir.")]
        public string Aciklama { get; set; } = string.Empty;

        [StringLength(160)]
        public string? AlternatifBaslik { get; set; }

        [StringLength(160)]
        public string? OrijinalBaslik { get; set; }

        [Required(ErrorMessage = "Tür seçimi zorunludur.")]
        public IcerikTuru Tur { get; set; }

        [StringLength(200, ErrorMessage = "Kategori en fazla 200 karakter olabilir.")]
        public string? Kategori { get; set; }

        [StringLength(100)]
        public string? Format { get; set; }

        [StringLength(300)]
        public string? Etiketler { get; set; }

        [StringLength(80)]
        public string? Durum { get; set; }

        public int? BaslangicYili { get; set; }

        public int? BitisYili { get; set; }

        public int? BolumSayisi { get; set; }

        public int? CiltSayisi { get; set; }

        public int? SureDakika { get; set; }

        [StringLength(220)]
        public string? Studyo { get; set; }

        [StringLength(500)]
        public string? Yaraticilar { get; set; }

        [StringLength(80)]
        public string? Kaynak { get; set; }

        public int? Skor { get; set; }

        public int? Populerlik { get; set; }

        public string? ResimYolu { get; set; }

        public string? BannerYolu { get; set; }

        [StringLength(300)]
        public string? DisBaglanti { get; set; }

        [StringLength(400)]
        public string? AnaKarakterler { get; set; }

        public List<Yorum> Yorumlar { get; set; } = new List<Yorum>();

        public string? GorselKaynak => BuildAssetPath(ResimYolu);

        public string? BannerKaynak => BuildAssetPath(BannerYolu);

        public string TurEtiketi => Tur switch
        {
            IcerikTuru.Anime => "Anime",
            IcerikTuru.Manga => "Manga",
            IcerikTuru.Film => "Film",
            IcerikTuru.Dizi => "Dizi",
            _ => Tur.ToString()
        };

        public string FormatEtiketi => Format switch
        {
            "TV" => "TV Anime",
            "SERIES" => "Dizi",
            "TV_SHORT" => "Kısa TV Anime",
            "MOVIE" => "Film",
            "SPECIAL" => "Özel Bölüm",
            "OVA" => "OVA",
            "ONA" => "ONA",
            "MANGA" => "Manga",
            "NOVEL" => "Roman",
            "ONE_SHOT" => "Tek Bölüm",
            "MANHWA" => "Manhwa",
            "MANHUA" => "Manhua",
            _ => Format ?? TurEtiketi
        };

        public string DurumEtiketi => Durum switch
        {
            "FINISHED" => "Tamamlandı",
            "RELEASING" => "Devam Ediyor",
            "NOT_YET_RELEASED" => "Yakında",
            "HIATUS" => "Ara Verdi",
            "CANCELLED" => "İptal",
            _ => Durum ?? "Bilinmiyor"
        };

        public string KaynakEtiketi => Kaynak switch
        {
            "MANGA" => "Manga",
            "LIGHT_NOVEL" => "Hafif Roman",
            "NOVEL" => "Roman",
            "WEB_NOVEL" => "Web Roman",
            "ORIGINAL" => "Orijinal",
            "VIDEO_GAME" => "Video Oyunu",
            "OTHER" => "Diğer",
            _ => Kaynak ?? "Bilinmiyor"
        };

        public string DonemEtiketi
        {
            get
            {
                if (BaslangicYili is null && BitisYili is null) return "Bilinmiyor";
                if (BaslangicYili == BitisYili || BitisYili is null) return $"{BaslangicYili}";
                return $"{BaslangicYili} - {BitisYili}";
            }
        }

        public string SkorEtiketi => Skor is null ? "?" : $"{Skor}/100";

        public string PopulerlikEtiketi => Populerlik is null
            ? "Bilinmiyor"
            : Populerlik.Value.ToString("N0", CultureInfo.InvariantCulture);

        public string TurkceAciklama => ShouldUseOriginalDescription(Aciklama)
            ? Aciklama
            : BuildTurkishDescription();

        public IReadOnlyList<string> KategoriListesi => SplitList(Kategori);

        public IReadOnlyList<string> EtiketListesi => SplitList(Etiketler);

        public IReadOnlyList<string> KarakterListesi => SplitList(AnaKarakterler);

        public IReadOnlyList<string> YaraticiListesi => SplitList(Yaraticilar);

        private static string? BuildAssetPath(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return Uri.IsWellFormedUriString(value, UriKind.Absolute) ? value : $"/img/afisler/{value}";
        }

        private string BuildTurkishDescription()
        {
            var turAdi = TurEtiketi.ToLower(new CultureInfo("tr-TR"));
            var format = FormatEtiketi;
            var kategoriler = KategoriListesi.Select(TranslateCategory).Take(3).ToList();
            var kategoriMetni = kategoriler.Count > 0
                ? $"{string.Join(", ", kategoriler)} çizgisini öne çıkaran"
                : "karakterleri ve atmosferiyle öne çıkan";

            var parcalar = new List<string>
            {
                $"{Baslik}, {kategoriMetni} {format} formatında bir {turAdi} içeriğidir."
            };

            if (BaslangicYili is not null)
            {
                parcalar.Add(BitisYili is not null && BitisYili != BaslangicYili
                    ? $"{BaslangicYili}-{BitisYili} dönemini kapsar."
                    : $"{BaslangicYili} döneminde öne çıkar.");
            }

            if (!string.IsNullOrWhiteSpace(Studyo))
                parcalar.Add($"Yapım tarafında {Studyo} adı dikkat çeker.");

            if (KarakterListesi.Count > 0)
                parcalar.Add($"Başlıca karakterler: {string.Join(", ", KarakterListesi.Take(4))}.");

            return string.Join(" ", parcalar);
        }

        private static bool ShouldUseOriginalDescription(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;

            var lower = value.ToLower(new CultureInfo("tr-TR"));
            if (lower.Contains("(source:") || lower.Contains("source:")) return false;

            var turkishMarkers = new[]
            {
                "ı", "ğ", "ü", "ş", "ö", "ç", "bir ", " ve ", " için ", "ile ", "olarak", "anlat"
            };

            return turkishMarkers.Any(lower.Contains);
        }

        private static string TranslateCategory(string value) => value switch
        {
            "Action" => "aksiyon",
            "Adventure" => "macera",
            "Comedy" => "komedi",
            "Crime" => "suç",
            "Drama" => "dram",
            "Ecchi" => "ecchi",
            "Family" => "aile",
            "Fantasy" => "fantastik",
            "Historical" => "tarihi",
            "Horror" => "korku",
            "Mecha" => "mecha",
            "Music" => "müzik",
            "Mystery" => "gizem",
            "Performing Arts" => "sahne sanatları",
            "Psychological" => "psikolojik",
            "Romance" => "romantik",
            "Sci-Fi" => "bilim kurgu",
            "Slice of Life" => "gündelik yaşam",
            "Sports" => "spor",
            "Supernatural" => "doğaüstü",
            "Thriller" => "gerilim",
            _ => value
        };

        private static IReadOnlyList<string> SplitList(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? Array.Empty<string>()
                : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        }
    }

    public class Yorum
    {
        public int Id { get; set; }

        public int IcerikId { get; set; }

        public Icerik? Icerik { get; set; }

        [Required]
        [StringLength(120)]
        public string KullaniciAdi { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Mesaj { get; set; } = string.Empty;

        public DateTime Tarih { get; set; } = DateTime.UtcNow;
    }
}
