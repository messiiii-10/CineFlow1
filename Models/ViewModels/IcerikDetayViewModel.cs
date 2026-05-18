using System.Collections.Generic;
using CineFlow.Models;

namespace CineFlow.Models.ViewModels
{
    public class IcerikDetayViewModel
    {
        public Icerik Icerik { get; set; } = new();
        public List<Icerik> BenzerIcerikler { get; set; } = new();
        public KullaniciIcerikKaydi? KullaniciKaydi { get; set; }
        public double? OrtalamaKullaniciPuani { get; set; }
        public int PuanlayanKullaniciSayisi { get; set; }
        public bool KutuphaneEtkilesimiAcik { get; set; }
    }
}
