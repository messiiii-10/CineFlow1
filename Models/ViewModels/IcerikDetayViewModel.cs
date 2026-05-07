using System.Collections.Generic;
using CineFlow.Models;

namespace CineFlow.Models.ViewModels
{
    public class IcerikDetayViewModel
    {
        public Icerik Icerik { get; set; } = new();
        public List<Icerik> BenzerIcerikler { get; set; } = new();
    }
}
