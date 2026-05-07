using System.Collections.Generic;
using CineFlow.Models;

namespace CineFlow.Models.ViewModels
{
    public class CatalogIndexViewModel
    {
        public List<Icerik> Icerikler { get; set; } = new();
        public List<string> Kategoriler { get; set; } = new();
        public List<string> Formatlar { get; set; } = new();
        public Icerik? OneCikanIcerik { get; set; }
        public string? Arama { get; set; }
        public string? Kategori { get; set; }
        public string? Format { get; set; }
        public IcerikTuru? Tur { get; set; }
        public int ToplamIcerik { get; set; }
        public int AnimeSayisi { get; set; }
        public int MangaSayisi { get; set; }
        public int DiziSayisi { get; set; }
    }
}
