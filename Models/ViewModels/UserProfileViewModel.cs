namespace CineFlow.Models.ViewModels
{
    public class UserProfileViewModel
    {
        public string KullaniciAdi { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public bool IsAdmin { get; set; }

        public bool ProfiliDuzenleyebilir { get; set; }

        public int YorumSayisi { get; set; }

        public int YorumYapilanIcerikSayisi { get; set; }

        public int KutuphaneKaydiSayisi { get; set; }

        public int FavoriSayisi { get; set; }

        public int ZiyaretSayisi { get; set; }

        public double? OrtalamaPuan { get; set; }

        public DateTime? SonAktivite { get; set; }

        public string? ProfilResmiKaynak { get; set; }

        public string? Biyografi { get; set; }

        public string ActiveSection { get; set; } = "overview";

        public List<UserProfileCommentViewModel> SonYorumlar { get; set; } = new();

        public List<UserProfileLibrarySectionViewModel> KutuphaneBolumleri { get; set; } = new();

        public List<UserProfileLibraryItemViewModel> FavoriIcerikler { get; set; } = new();

        public List<UserProfileLibraryItemViewModel> SonZiyaretler { get; set; } = new();

        public string Monogram
        {
            get
            {
                if (string.IsNullOrWhiteSpace(KullaniciAdi))
                    return "CF";

                var parts = KullaniciAdi
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Take(2)
                    .Select(x => char.ToUpperInvariant(x[0]));

                var monogram = string.Concat(parts);
                return string.IsNullOrWhiteSpace(monogram)
                    ? KullaniciAdi[..Math.Min(2, KullaniciAdi.Length)].ToUpperInvariant()
                    : monogram;
            }
        }

        public bool IsOverviewSection => ActiveSection == "overview";

        public bool IsFavoritesSection => ActiveSection == "favorites";

        public bool IsLibrarySection => ActiveSection == "library";

        public bool IsHistorySection => ActiveSection == "history";

        public bool IsSettingsSection => ActiveSection == "settings";
    }

    public class UserProfileCommentViewModel
    {
        public int IcerikId { get; set; }

        public string IcerikBaslik { get; set; } = string.Empty;

        public string Mesaj { get; set; } = string.Empty;

        public DateTime Tarih { get; set; }
    }

    public class UserProfileLibrarySectionViewModel
    {
        public string Baslik { get; set; } = string.Empty;

        public string? Aciklama { get; set; }

        public List<UserProfileLibraryItemViewModel> Icerikler { get; set; } = new();
    }

    public class UserProfileLibraryItemViewModel
    {
        public int IcerikId { get; set; }

        public string Baslik { get; set; } = string.Empty;

        public string TurEtiketi { get; set; } = string.Empty;

        public string FormatEtiketi { get; set; } = string.Empty;

        public string DurumEtiketi { get; set; } = string.Empty;

        public string? GorselKaynak { get; set; }

        public int? KisiselPuan { get; set; }

        public bool FavoriMi { get; set; }

        public int YorumSayisi { get; set; }

        public DateTime? SonZiyaretTarihi { get; set; }

        public DateTime GuncellemeTarihi { get; set; }
    }
}
