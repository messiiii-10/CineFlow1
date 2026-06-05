using Microsoft.AspNetCore.Mvc;
using CineFlow.Data;
using CineFlow.Models;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;
using System;
using CineFlow.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using CineFlow.Services;
using System.Globalization;
using System.Text;
namespace CineFlow.Controllers
{
    public class AdminController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly AppDbContext _dbContext;
        private readonly FirebaseIdentityService _firebaseIdentityService;

        public AdminController(IWebHostEnvironment env, AppDbContext dbContext, FirebaseIdentityService firebaseIdentityService)
        {
            _env = env;
            _dbContext = dbContext;
            _firebaseIdentityService = firebaseIdentityService;
        }

        private bool IsAdmin() => HttpContext.Session.GetString("AdminAuth") == "true";

        [HttpGet]
        public IActionResult Login()
            => _firebaseIdentityService.IsEnabled
                ? RedirectToAction("Login", "Home")
                : View(new AdminLogin());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(AdminLogin model)
        {
            if (_firebaseIdentityService.IsEnabled)
                return RedirectToAction("Login", "Home");

            if (!ModelState.IsValid)
                return View(model);

            var email = model.Email.Trim();
            var admin = await _dbContext.Adminler.FirstOrDefaultAsync(a => a.Email.ToLower() == email.ToLower());
            if (admin is null || !PasswordHasher.VerifyPassword(model.Password, admin.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "E-posta veya şifre hatalı.");
                return View(model);
            }

            HttpContext.Session.SetString("AdminAuth", "true");
            HttpContext.Session.SetString("AdminEmail", admin.Email);
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Index(string? q)
        {
            if (!IsAdmin()) return RedirectToAction("Login", _firebaseIdentityService.IsEnabled ? "Home" : "Admin");

            var query = _dbContext.Icerikler
                .Include(x => x.Yorumlar)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var pattern = $"%{q.Trim()}%";
                query = query.Where(x =>
                    EF.Functions.Like(x.Baslik, pattern) ||
                    (x.AlternatifBaslik != null && EF.Functions.Like(x.AlternatifBaslik, pattern)) ||
                    (x.OrijinalBaslik != null && EF.Functions.Like(x.OrijinalBaslik, pattern)) ||
                    (x.Kategori != null && EF.Functions.Like(x.Kategori, pattern)) ||
                    (x.Yaraticilar != null && EF.Functions.Like(x.Yaraticilar, pattern)));
            }

            var toplamIcerik = await _dbContext.Icerikler.CountAsync();
            var icerikler = await query
                .OrderBy(x => x.Baslik)
                .ToListAsync();

            return View(new AdminIndexViewModel
            {
                Icerikler = icerikler,
                Arama = q,
                ToplamIcerik = toplamIcerik
            });
        }

        public IActionResult Ekle() => IsAdmin() ? View() : RedirectToAction("Login", _firebaseIdentityService.IsEnabled ? "Home" : "Admin");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ekle(Icerik model, IFormFile afis)
        {
            if (!IsAdmin()) return RedirectToAction("Login", _firebaseIdentityService.IsEnabled ? "Home" : "Admin");

            if (!ModelState.IsValid)
                return View(model);

            if (afis != null && afis.Length > 0)
            {
                string uploadDir = Path.Combine(_env.WebRootPath, "img/afisler");
                if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);
                string dosya = Guid.NewGuid().ToString("N") + Path.GetExtension(afis.FileName);
                using (var s = new FileStream(Path.Combine(uploadDir, dosya), FileMode.Create)) await afis.CopyToAsync(s);
                model.ResimYolu = dosya;
            }

            _dbContext.Icerikler.Add(model);
            await _dbContext.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Duzenle(int id) 
        {
            if (!IsAdmin()) return RedirectToAction("Login", _firebaseIdentityService.IsEnabled ? "Home" : "Admin");

            var veri = await _dbContext.Icerikler.FirstOrDefaultAsync(x => x.Id == id);
            return veri != null ? View(veri) : RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Duzenle(Icerik model, IFormFile? afis)
        {
            if (!IsAdmin()) return RedirectToAction("Login", _firebaseIdentityService.IsEnabled ? "Home" : "Admin");

            if (!ModelState.IsValid)
                return View(model);

            var eski = await _dbContext.Icerikler.FirstOrDefaultAsync(x => x.Id == model.Id);
            if (eski == null) return RedirectToAction("Index");

            // Resim değiştirildiyse upload et; değilse eskisini koru
            if (afis != null && afis.Length > 0)
            {
                string uploadDir = Path.Combine(_env.WebRootPath, "img/afisler");
                if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);
                string dosya = Guid.NewGuid().ToString("N") + Path.GetExtension(afis.FileName);
                using (var s = new FileStream(Path.Combine(uploadDir, dosya), FileMode.Create)) await afis.CopyToAsync(s);
                eski.ResimYolu = dosya;
            }

            eski.Baslik = model.Baslik;
            eski.Aciklama = model.Aciklama;
            eski.Tur = model.Tur;
            eski.Kategori = model.Kategori;
            await _dbContext.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sil(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", _firebaseIdentityService.IsEnabled ? "Home" : "Admin");

            var veri = await _dbContext.Icerikler.FirstOrDefaultAsync(x => x.Id == id);
            if (veri != null)
            {
                _dbContext.Icerikler.Remove(veri);
                await _dbContext.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }
        // =====================================================================
// AdminController.cs dosyasına EKLENECEK metodlar
// Mevcut using'lere bunları ekle:
//   using System.Globalization;
//   using System.Text;
// =====================================================================

// --- Index() action'ının üstüne veya Logout()'un altına ekle ---

public async Task<IActionResult> Rapor(
    DateTime? baslangic,
    DateTime? bitis,
    string? durum,
    string? tur)
{
    if (!IsAdmin())
        return RedirectToAction("Login", _firebaseIdentityService.IsEnabled ? "Home" : "Admin");

    // --- İstatistikler ---
    var toplamKullanici  = await _dbContext.Kullanicilar.CountAsync();
    var toplamIcerik     = await _dbContext.Icerikler.CountAsync();
    var toplamYorum      = await _dbContext.Yorumlar.CountAsync();
    var toplamKayit      = await _dbContext.KullaniciIcerikKayitlari.CountAsync();

    // Türe göre dağılım
    var animeCount = await _dbContext.Icerikler.CountAsync(x => x.Tur == IcerikTuru.Anime);
    var mangaCount = await _dbContext.Icerikler.CountAsync(x => x.Tur == IcerikTuru.Manga);
    var diziCount  = await _dbContext.Icerikler.CountAsync(x => x.Tur == IcerikTuru.Dizi);
    var filmCount  = await _dbContext.Icerikler.CountAsync(x => x.Tur == IcerikTuru.Film);

    // Son 6 ay aylık yorum sayıları
    var simdi = DateTime.UtcNow;
    var aylikYorumlar = new List<AylikVeri>();
    for (int i = 5; i >= 0; i--)
    {
        var ay = simdi.AddMonths(-i);
        var sayi = await _dbContext.Yorumlar.CountAsync(y =>
            y.Tarih.Year == ay.Year && y.Tarih.Month == ay.Month);
        aylikYorumlar.Add(new AylikVeri
        {
            Ay   = ay.ToString("MMM yyyy", new CultureInfo("tr-TR")),
            Sayi = sayi
        });
    }

    // Kullanıcı kayıt durum dağılımı
    var planCount       = await _dbContext.KullaniciIcerikKayitlari.CountAsync(x => x.Durum == KullaniciIcerikDurumu.Planliyor);
    var izliyorCount    = await _dbContext.KullaniciIcerikKayitlari.CountAsync(x => x.Durum == KullaniciIcerikDurumu.Izliyor);
    var tamamlandiCount = await _dbContext.KullaniciIcerikKayitlari.CountAsync(x => x.Durum == KullaniciIcerikDurumu.Tamamlandi);
    var biraktiCount    = await _dbContext.KullaniciIcerikKayitlari.CountAsync(x => x.Durum == KullaniciIcerikDurumu.Birakti);

    // En çok ziyaret edilen 5 içerik
    var enCokZiyaret = await _dbContext.KullaniciIcerikKayitlari
        .Include(x => x.Icerik)
        .GroupBy(x => x.Icerik!.Baslik)
        .Select(g => new IcerikZiyaret
        {
            Baslik       = g.Key,
            ToplamZiyaret = g.Sum(x => x.ZiyaretSayisi)
        })
        .OrderByDescending(x => x.ToplamZiyaret)
        .Take(5)
        .ToListAsync();

    // --- Rapor listesi (filtrelenebilir) ---
    var query = _dbContext.KullaniciIcerikKayitlari
        .Include(x => x.Icerik)
        .AsQueryable();

    if (baslangic.HasValue)
        query = query.Where(x => x.OlusturmaTarihi >= baslangic.Value);
    if (bitis.HasValue)
        query = query.Where(x => x.OlusturmaTarihi <= bitis.Value.AddDays(1));
    if (!string.IsNullOrWhiteSpace(durum) && Enum.TryParse<KullaniciIcerikDurumu>(durum, out var durumEnum))
        query = query.Where(x => x.Durum == durumEnum);
    if (!string.IsNullOrWhiteSpace(tur) && Enum.TryParse<IcerikTuru>(tur, out var turEnum))
        query = query.Where(x => x.Icerik!.Tur == turEnum);

    var kayitlar = await query
        .OrderByDescending(x => x.OlusturmaTarihi)
        .Take(500)
        .ToListAsync();

    var vm = new AdminRaporViewModel
    {
        ToplamKullanici      = toplamKullanici,
        ToplamIcerik         = toplamIcerik,
        ToplamYorum          = toplamYorum,
        ToplamKayit          = toplamKayit,
        AnimeCount           = animeCount,
        MangaCount           = mangaCount,
        DiziCount            = diziCount,
        FilmCount            = filmCount,
        AylikYorumlar        = aylikYorumlar,
        PlanlivoryorCount    = planCount,
        IzliyorCount         = izliyorCount,
        TamamlandiCount      = tamamlandiCount,
        BiraktiCount         = biraktiCount,
        EnCokZiyaretEdilen   = enCokZiyaret,
        Kayitlar             = kayitlar,
        BaslangicTarihi      = baslangic,
        BitisTarihi          = bitis,
        DurumFiltre          = durum,
        TurFiltre            = tur
    };

    return View(vm);
}

// PDF Export — tablo verisini CSV olarak indir (harici kütüphane gerektirmez)
public async Task<IActionResult> RaporIndir(
    DateTime? baslangic,
    DateTime? bitis,
    string? durum,
    string? tur)
{
    if (!IsAdmin())
        return RedirectToAction("Login", _firebaseIdentityService.IsEnabled ? "Home" : "Admin");

    var query = _dbContext.KullaniciIcerikKayitlari
        .Include(x => x.Icerik)
        .AsQueryable();

    if (baslangic.HasValue)
        query = query.Where(x => x.OlusturmaTarihi >= baslangic.Value);
    if (bitis.HasValue)
        query = query.Where(x => x.OlusturmaTarihi <= bitis.Value.AddDays(1));
    if (!string.IsNullOrWhiteSpace(durum) && Enum.TryParse<KullaniciIcerikDurumu>(durum, out var durumEnum))
        query = query.Where(x => x.Durum == durumEnum);
    if (!string.IsNullOrWhiteSpace(tur) && Enum.TryParse<IcerikTuru>(tur, out var turEnum))
        query = query.Where(x => x.Icerik!.Tur == turEnum);

    var kayitlar = await query
        .OrderByDescending(x => x.OlusturmaTarihi)
        .Take(5000)
        .ToListAsync();

    var sb = new StringBuilder();
    sb.AppendLine("Kullanici Email,Icerik,Tur,Durum,Puan,Favori,Ziyaret,Olusturma Tarihi");
    foreach (var k in kayitlar)
    {
        sb.AppendLine(string.Join(",",
            Esc(k.KullaniciEmail),
            Esc(k.Icerik?.Baslik ?? "-"),
            Esc(k.Icerik?.TurEtiketi ?? "-"),
            Esc(k.DurumEtiketi),
            k.KisiselPuan?.ToString() ?? "-",
            k.FavoriMi ? "Evet" : "Hayır",
            k.ZiyaretSayisi.ToString(),
            k.OlusturmaTarihi.ToString("dd.MM.yyyy HH:mm")));
    }

    var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    return File(bytes, "text/csv", $"cineflow-rapor-{DateTime.Now:yyyyMMdd}.csv");
}

private static string Esc(string? v)
{
    if (string.IsNullOrEmpty(v)) return "";
    return v.Contains(',') || v.Contains('"') || v.Contains('\n')
        ? $"\"{v.Replace("\"", "\"\"")}\""
        : v;
}
    }
}
