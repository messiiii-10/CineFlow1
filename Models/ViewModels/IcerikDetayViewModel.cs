using System;
using System.Collections.Generic;
using System.Linq;
using CineFlow.Models;

namespace CineFlow.Models.ViewModels
{
    public class IcerikDetayViewModel
    {
        public Icerik Icerik { get; set; } = new();
        public List<Icerik> BenzerIcerikler { get; set; } = new();
        public KullaniciIcerikKaydi? KullaniciKaydi { get; set; }
        public List<IcerikCommentViewModel> Yorumlar { get; set; } = new();
        public double? OrtalamaKullaniciPuani { get; set; }
        public int PuanlayanKullaniciSayisi { get; set; }
        public bool KutuphaneEtkilesimiAcik { get; set; }
    }

    public class IcerikCommentViewModel
    {
        public string KullaniciAdi { get; set; } = string.Empty;
        public string Mesaj { get; set; } = string.Empty;
        public DateTime Tarih { get; set; }
        public string? ProfilResmiKaynak { get; set; }

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
    }
}
