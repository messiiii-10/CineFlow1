using CineFlow.Models;

namespace CineFlow.Models.ViewModels
{
    public class AdminIndexViewModel
    {
        public List<Icerik> Icerikler { get; set; } = new();

        public string? Arama { get; set; }

        public int ToplamIcerik { get; set; }

        public int SonucSayisi => Icerikler.Count;

        public bool AramaVar => !string.IsNullOrWhiteSpace(Arama);
    }
}
