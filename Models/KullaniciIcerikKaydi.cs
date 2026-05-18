using System.ComponentModel.DataAnnotations;

namespace CineFlow.Models
{
    public enum KullaniciIcerikDurumu
    {
        Planliyor = 1,
        Izliyor = 2,
        Tamamlandi = 3,
        Birakti = 4
    }

    public class KullaniciIcerikKaydi
    {
        public int Id { get; set; }

        [Required]
        [EmailAddress]
        public string KullaniciEmail { get; set; } = string.Empty;

        public int IcerikId { get; set; }

        public Icerik? Icerik { get; set; }

        [Required]
        public KullaniciIcerikDurumu Durum { get; set; } = KullaniciIcerikDurumu.Planliyor;

        [Range(1, 10, ErrorMessage = "Puan 1 ile 10 arasında olmalıdır.")]
        public int? KisiselPuan { get; set; }

        public bool KutuphanedeMi { get; set; } = true;

        public bool FavoriMi { get; set; }

        public DateTime? SonZiyaretTarihi { get; set; }

        public int ZiyaretSayisi { get; set; }

        public DateTime OlusturmaTarihi { get; set; } = DateTime.UtcNow;

        public DateTime GuncellemeTarihi { get; set; } = DateTime.UtcNow;

        public string DurumEtiketi => Durum switch
        {
            KullaniciIcerikDurumu.Planliyor => "Listemde",
            KullaniciIcerikDurumu.Izliyor => "İzliyorum",
            KullaniciIcerikDurumu.Tamamlandi => "Tamamladım",
            KullaniciIcerikDurumu.Birakti => "Bıraktım",
            _ => "Listemde"
        };
    }
}
