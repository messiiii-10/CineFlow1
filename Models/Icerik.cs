using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

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
            "TV_SHORT" => "TV Short",
            "MOVIE" => "Film",
            "SPECIAL" => "Special",
            "OVA" => "OVA",
            "ONA" => "ONA",
            "MANGA" => "Manga",
            "NOVEL" => "Novel",
            "ONE_SHOT" => "One Shot",
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
            "LIGHT_NOVEL" => "Light Novel",
            "NOVEL" => "Roman",
            "WEB_NOVEL" => "Web Novel",
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

        public IReadOnlyList<string> KategoriListesi => SplitList(Kategori);

        public IReadOnlyList<string> EtiketListesi => SplitList(Etiketler);

        public IReadOnlyList<string> KarakterListesi => SplitList(AnaKarakterler);

        public IReadOnlyList<string> YaraticiListesi => SplitList(Yaraticilar);

        private static string? BuildAssetPath(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return Uri.IsWellFormedUriString(value, UriKind.Absolute) ? value : $"/img/afisler/{value}";
        }

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
