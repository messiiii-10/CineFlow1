using CineFlow.Data;
using Microsoft.AspNetCore.Mvc;
using CineFlow.Models;
using CineFlow.Models.ViewModels;
using Microsoft.AspNetCore.Http;
using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace CineFlow.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _dbContext;

        public HomeController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index(string? q, IcerikTuru? tur, string? kategori, string? format)
        {
            var katalog = _dbContext.Icerikler
                .AsNoTracking()
                .Include(x => x.Yorumlar)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var pattern = $"%{q.Trim()}%";
                katalog = katalog.Where(x =>
                    EF.Functions.Like(x.Baslik, pattern) ||
                    (x.AlternatifBaslik != null && EF.Functions.Like(x.AlternatifBaslik, pattern)) ||
                    (x.OrijinalBaslik != null && EF.Functions.Like(x.OrijinalBaslik, pattern)) ||
                    (x.Kategori != null && EF.Functions.Like(x.Kategori, pattern)) ||
                    (x.Etiketler != null && EF.Functions.Like(x.Etiketler, pattern)) ||
                    (x.AnaKarakterler != null && EF.Functions.Like(x.AnaKarakterler, pattern)) ||
                    (x.Yaraticilar != null && EF.Functions.Like(x.Yaraticilar, pattern)));
            }

            if (tur.HasValue)
                katalog = katalog.Where(x => x.Tur == tur.Value);

            if (!string.IsNullOrWhiteSpace(kategori))
                katalog = katalog.Where(x => x.Kategori != null && EF.Functions.Like(x.Kategori, $"%{kategori}%"));

            if (!string.IsNullOrWhiteSpace(format))
                katalog = katalog.Where(x => x.Format == format);

            var tumIcerikler = await _dbContext.Icerikler
                .AsNoTracking()
                .OrderByDescending(x => x.Populerlik ?? 0)
                .ThenByDescending(x => x.Skor ?? 0)
                .ToListAsync();

            var sonuc = await katalog
                .OrderByDescending(x => x.Populerlik ?? 0)
                .ThenByDescending(x => x.Skor ?? 0)
                .ThenBy(x => x.Baslik)
                .ToListAsync();

            var model = new CatalogIndexViewModel
            {
                Icerikler = sonuc,
                Arama = q,
                Tur = tur,
                Kategori = kategori,
                Format = format,
                ToplamIcerik = tumIcerikler.Count,
                AnimeSayisi = tumIcerikler.Count(x => x.Tur == IcerikTuru.Anime),
                MangaSayisi = tumIcerikler.Count(x => x.Tur == IcerikTuru.Manga),
                Kategoriler = tumIcerikler
                    .SelectMany(x => x.KategoriListesi)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)
                    .Take(18)
                    .ToList(),
                Formatlar = tumIcerikler
                    .Select(x => x.FormatEtiketi)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)
                    .ToList()
            };

            return View(model);
        }

        public async Task<IActionResult> Detay(int id)
        {
            var icerik = await _dbContext.Icerikler
                .Include(x => x.Yorumlar)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (icerik is null)
                return NotFound();

            var benzerler = await _dbContext.Icerikler
                .AsNoTracking()
                .Where(x => x.Id != id && x.Tur == icerik.Tur)
                .OrderByDescending(x => x.Kategori == icerik.Kategori)
                .ThenByDescending(x => x.Skor ?? 0)
                .ThenByDescending(x => x.Populerlik ?? 0)
                .Take(4)
                .ToListAsync();

            return View(new IcerikDetayViewModel
            {
                Icerik = icerik,
                BenzerIcerikler = benzerler
            });
        }

        [HttpGet]
        public IActionResult Login() => View(new UserLoginViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(UserLoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var email = model.Email.Trim();
            var k = await _dbContext.Kullanicilar
                .FirstOrDefaultAsync(x => x.Email.ToLower() == email.ToLower());

            if (k != null && PasswordHasher.VerifyPassword(model.Password, k.Password))
            {
                HttpContext.Session.SetString("User", k.KullaniciAdi);
                HttpContext.Session.SetString("UserEmail", k.Email);
                return RedirectToAction("Index");
            }

            ModelState.AddModelError(string.Empty, "E-posta veya şifre hatalı.");
            return View(model);
        }

        [HttpGet]
        public IActionResult Kayit() => View(new UserRegisterViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Kayit(UserRegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var username = model.Username.Trim();
            var email = model.Email.Trim();

            if (await _dbContext.Kullanicilar.AnyAsync(x => x.KullaniciAdi.ToLower() == username.ToLower()))
            {
                ModelState.AddModelError(nameof(model.Username), "Bu kullanıcı adı zaten kullanılıyor.");
                return View(model);
            }

            if (await _dbContext.Kullanicilar.AnyAsync(x => x.Email.ToLower() == email.ToLower()))
            {
                ModelState.AddModelError(nameof(model.Email), "Bu e-posta ile kayıt zaten var.");
                return View(model);
            }

            var yeni = new Kullanici
            {
                KullaniciAdi = username,
                Email = email,
                Password = PasswordHasher.HashPassword(model.Password)
            };

            _dbContext.Kullanicilar.Add(yeni);
            await _dbContext.SaveChangesAsync();
            TempData["AuthMessage"] = "Hesabınız oluşturuldu. Şimdi giriş yapabilirsiniz.";
            return RedirectToAction("Login");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Remove("User");
            HttpContext.Session.Remove("UserEmail");
            HttpContext.Session.Remove("AdminAuth");
            HttpContext.Session.Remove("AdminEmail");
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> YorumYap(int icerikId, string mesaj)
        {
            var user = HttpContext.Session.GetString("User");
            if (string.IsNullOrEmpty(user)) return RedirectToAction("Login");

            var icerik = await _dbContext.Icerikler.FirstOrDefaultAsync(x => x.Id == icerikId);
            if (icerik is null) return NotFound();

            var yorum = new Yorum
            {
                IcerikId = icerikId,
                KullaniciAdi = user,
                Mesaj = (mesaj ?? string.Empty).Trim()
            };

            if (TryValidateModel(yorum))
            {
                _dbContext.Yorumlar.Add(yorum);
                await _dbContext.SaveChangesAsync();
            }
            else
                TempData["CommentError"] = "Yorum 1 ile 500 karakter arasında olmalıdır.";

            return RedirectToAction("Detay", new { id = icerikId });
        }
    }
}
