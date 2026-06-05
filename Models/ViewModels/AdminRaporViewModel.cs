using CineFlow.Models;

namespace CineFlow.Models.ViewModels
{
    public class AdminRaporViewModel
    {
        // --- İstatistik verileri ---
        public int ToplamKullanici { get; set; }
        public int ToplamIcerik { get; set; }
        public int ToplamYorum { get; set; }
        public int ToplamKayit { get; set; }

        // Türe göre içerik sayıları (pasta grafik)
        public int AnimeCount { get; set; }
        public int MangaCount { get; set; }
        public int DiziCount { get; set; }
        public int FilmCount { get; set; }

        // Aylık yorum sayıları (sütun grafik) - son 6 ay
        public List<AylikVeri> AylikYorumlar { get; set; } = new();

        // Kullanıcı kayıt durumu dağılımı
        public int PlanlivoryorCount { get; set; }
        public int IzliyorCount { get; set; }
        public int TamamlandiCount { get; set; }
        public int BiraktiCount { get; set; }

        // En çok ziyaret edilen içerikler (top 5)
        public List<IcerikZiyaret> EnCokZiyaretEdilen { get; set; } = new();

        // --- Rapor (filtrelenebilir liste) ---
        public List<KullaniciIcerikKaydi> Kayitlar { get; set; } = new();

        // Filtre parametreleri
        public DateTime? BaslangicTarihi { get; set; }
        public DateTime? BitisTarihi { get; set; }
        public string? DurumFiltre { get; set; }
        public string? TurFiltre { get; set; }
    }

    public class AylikVeri
    {
        public string Ay { get; set; } = string.Empty;
        public int Sayi { get; set; }
    }

    public class IcerikZiyaret
    {
        public string Baslik { get; set; } = string.Empty;
        public int ToplamZiyaret { get; set; }
    }
}
