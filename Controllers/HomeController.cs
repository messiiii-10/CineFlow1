using CineFlow.Data;
using Microsoft.AspNetCore.Mvc;
using CineFlow.Models;
using CineFlow.Models.ViewModels;
using Microsoft.AspNetCore.Http;
using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using CineFlow.Services;
using System.IO;

namespace CineFlow.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _dbContext;
        private readonly FirebaseIdentityService _firebaseIdentityService;
        private readonly IWebHostEnvironment _env;

        public HomeController(AppDbContext dbContext, FirebaseIdentityService firebaseIdentityService, IWebHostEnvironment env)
        {
            _dbContext = dbContext;
            _firebaseIdentityService = firebaseIdentityService;
            _env = env;
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
                DiziSayisi = tumIcerikler.Count(x => x.Tur == IcerikTuru.Dizi),
                OneCikanIcerik = tumIcerikler.FirstOrDefault(x => x.Tur == IcerikTuru.Dizi && !string.IsNullOrWhiteSpace(x.GorselKaynak))
                    ?? tumIcerikler.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.GorselKaynak))
                    ?? tumIcerikler.FirstOrDefault(),
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

            var currentEmail = GetCurrentEmail();
            var kullaniciKaydi = string.IsNullOrWhiteSpace(currentEmail)
                ? null
                : await _dbContext.KullaniciIcerikKayitlari
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.KullaniciEmail == currentEmail && x.IcerikId == id);

            var puanBilgisi = await _dbContext.KullaniciIcerikKayitlari
                .AsNoTracking()
                .Where(x => x.IcerikId == id && x.KisiselPuan.HasValue)
                .GroupBy(x => 1)
                .Select(x => new
                {
                    Ortalama = x.Average(y => y.KisiselPuan!.Value),
                    Sayi = x.Count()
                })
                .FirstOrDefaultAsync();

            return View(new IcerikDetayViewModel
            {
                Icerik = icerik,
                BenzerIcerikler = benzerler,
                KullaniciKaydi = kullaniciKaydi,
                OrtalamaKullaniciPuani = puanBilgisi?.Ortalama,
                PuanlayanKullaniciSayisi = puanBilgisi?.Sayi ?? 0,
                KutuphaneEtkilesimiAcik = !string.IsNullOrWhiteSpace(currentEmail)
            });
        }

        [HttpGet]
        public async Task<IActionResult> Profil(string? section)
        {
            var currentUser = HttpContext.Session.GetString("User");
            var currentEmail = HttpContext.Session.GetString("UserEmail");
            var adminEmail = HttpContext.Session.GetString("AdminEmail");
            var isAdmin = HttpContext.Session.GetString("AdminAuth") == "true";

            if (string.IsNullOrWhiteSpace(currentUser) && !isAdmin)
                return RedirectToAction("Login");

            var email = currentEmail ?? adminEmail ?? string.Empty;
            var displayName = string.IsNullOrWhiteSpace(currentUser) ? "Yönetici" : currentUser;
            var kullanici = string.IsNullOrWhiteSpace(email)
                ? null
                : await _dbContext.Kullanicilar
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Email == email);

            var yorumlar = string.IsNullOrWhiteSpace(currentUser)
                ? new List<Yorum>()
                : await _dbContext.Yorumlar
                    .AsNoTracking()
                    .Include(x => x.Icerik)
                    .Where(x => x.KullaniciAdi == currentUser)
                    .OrderByDescending(x => x.Tarih)
                    .ToListAsync();

            var kutuphaneKayitlari = string.IsNullOrWhiteSpace(email)
                ? new List<KullaniciIcerikKaydi>()
                : await _dbContext.KullaniciIcerikKayitlari
                    .AsNoTracking()
                    .Include(x => x.Icerik)
                    .Where(x => x.KullaniciEmail == email)
                    .OrderByDescending(x => x.GuncellemeTarihi)
                    .ToListAsync();

            var puanliKayitlar = kutuphaneKayitlari
                .Where(x => x.KisiselPuan.HasValue)
                .ToList();

            var normalizedSection = NormalizeProfileSection(section);

            var model = new UserProfileViewModel
            {
                KullaniciAdi = displayName,
                Email = email,
                IsAdmin = isAdmin,
                ProfiliDuzenleyebilir = kullanici != null,
                YorumSayisi = yorumlar.Count,
                YorumYapilanIcerikSayisi = yorumlar
                    .Select(x => x.IcerikId)
                    .Distinct()
                    .Count(),
                KutuphaneKaydiSayisi = kutuphaneKayitlari.Count,
                OrtalamaPuan = puanliKayitlar.Count > 0
                    ? puanliKayitlar.Average(x => x.KisiselPuan!.Value)
                    : null,
                SonAktivite = yorumlar.FirstOrDefault()?.Tarih,
                ProfilResmiKaynak = BuildAvatarPath(kullanici?.ProfilResmiYolu),
                Biyografi = kullanici?.Biyografi,
                ActiveSection = normalizedSection,
                SonYorumlar = yorumlar
                    .Take(6)
                    .Select(x => new UserProfileCommentViewModel
                    {
                        IcerikId = x.IcerikId,
                        IcerikBaslik = x.Icerik?.Baslik ?? "İçerik",
                        Mesaj = x.Mesaj,
                        Tarih = x.Tarih
                    })
                    .ToList(),
                KutuphaneBolumleri = kutuphaneKayitlari
                    .Where(x => x.Icerik != null)
                    .GroupBy(x => x.Icerik!.Tur)
                    .OrderBy(x => x.Key)
                    .Select(x => new UserProfileLibrarySectionViewModel
                    {
                        Baslik = x.Key switch
                        {
                            IcerikTuru.Film => "Filmler",
                            IcerikTuru.Dizi => "Diziler",
                            IcerikTuru.Anime => "Animeler",
                            IcerikTuru.Manga => "Mangalar",
                            _ => "Koleksiyon"
                        },
                        Icerikler = x
                            .OrderByDescending(y => y.GuncellemeTarihi)
                            .Select(y => new UserProfileLibraryItemViewModel
                            {
                                IcerikId = y.IcerikId,
                                Baslik = y.Icerik!.Baslik,
                                TurEtiketi = y.Icerik.TurEtiketi,
                                FormatEtiketi = y.Icerik.FormatEtiketi,
                                DurumEtiketi = y.DurumEtiketi,
                                GorselKaynak = y.Icerik.GorselKaynak,
                                KisiselPuan = y.KisiselPuan,
                                GuncellemeTarihi = y.GuncellemeTarihi
                            })
                            .ToList()
                    })
                    .ToList(),
                OneCikanIcerikler = kutuphaneKayitlari
                    .Where(x => x.Icerik != null && x.KisiselPuan.HasValue)
                    .OrderByDescending(x => x.KisiselPuan)
                    .ThenByDescending(x => x.GuncellemeTarihi)
                    .Take(4)
                    .Select(x => new UserProfileLibraryItemViewModel
                    {
                        IcerikId = x.IcerikId,
                        Baslik = x.Icerik!.Baslik,
                        TurEtiketi = x.Icerik.TurEtiketi,
                        FormatEtiketi = x.Icerik.FormatEtiketi,
                        DurumEtiketi = x.DurumEtiketi,
                        GorselKaynak = x.Icerik.GorselKaynak,
                        KisiselPuan = x.KisiselPuan,
                        GuncellemeTarihi = x.GuncellemeTarihi
                    })
                    .ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProfilGuncelle(string? biyografi, IFormFile? profilResmi)
        {
            var currentEmail = GetCurrentEmail();
            if (string.IsNullOrWhiteSpace(currentEmail))
                return RedirectToAction("Login");

            var kullanici = await _dbContext.Kullanicilar.FirstOrDefaultAsync(x => x.Email == currentEmail);
            if (kullanici is null)
            {
                TempData["ProfileError"] = "Profil bilgisi bulunamadı.";
                return RedirectToAction("Profil");
            }

            if (biyografi != null && biyografi.Length > 280)
            {
                TempData["ProfileError"] = "Biyografi en fazla 280 karakter olabilir.";
                return RedirectToAction("Profil");
            }

            if (profilResmi != null && profilResmi.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var extension = Path.GetExtension(profilResmi.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(extension))
                {
                    TempData["ProfileError"] = "Profil resmi için JPG, PNG veya WEBP yükleyebilirsin.";
                    return RedirectToAction("Profil");
                }

                if (profilResmi.Length > 5 * 1024 * 1024)
                {
                    TempData["ProfileError"] = "Profil resmi en fazla 5 MB olabilir.";
                    return RedirectToAction("Profil");
                }

                var uploadsDir = Path.Combine(_env.WebRootPath, "img", "avatars");
                if (!Directory.Exists(uploadsDir))
                    Directory.CreateDirectory(uploadsDir);

                if (!string.IsNullOrWhiteSpace(kullanici.ProfilResmiYolu))
                {
                    var existingPath = Path.Combine(uploadsDir, kullanici.ProfilResmiYolu);
                    if (System.IO.File.Exists(existingPath))
                        System.IO.File.Delete(existingPath);
                }

                var fileName = $"{Guid.NewGuid():N}{extension}";
                var filePath = Path.Combine(uploadsDir, fileName);

                await using var stream = new FileStream(filePath, FileMode.Create);
                await profilResmi.CopyToAsync(stream);
                kullanici.ProfilResmiYolu = fileName;
            }

            kullanici.Biyografi = string.IsNullOrWhiteSpace(biyografi) ? null : biyografi.Trim();
            await _dbContext.SaveChangesAsync();

            TempData["ProfileMessage"] = "Profilin güncellendi.";
            return RedirectToAction("Profil");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KutuphaneGuncelle(int icerikId, KullaniciIcerikDurumu durum, int? kisiselPuan)
        {
            var currentEmail = GetCurrentEmail();
            if (string.IsNullOrWhiteSpace(currentEmail))
                return RedirectToAction("Login");

            var icerikVar = await _dbContext.Icerikler.AnyAsync(x => x.Id == icerikId);
            if (!icerikVar)
                return NotFound();

            if (kisiselPuan is < 1 or > 10)
            {
                TempData["CollectionError"] = "Puan 1 ile 10 arasında olmalıdır.";
                return RedirectToAction("Detay", new { id = icerikId });
            }

            var kayit = await _dbContext.KullaniciIcerikKayitlari
                .FirstOrDefaultAsync(x => x.KullaniciEmail == currentEmail && x.IcerikId == icerikId);

            if (kayit is null)
            {
                kayit = new KullaniciIcerikKaydi
                {
                    KullaniciEmail = currentEmail,
                    IcerikId = icerikId,
                    OlusturmaTarihi = DateTime.UtcNow
                };

                _dbContext.KullaniciIcerikKayitlari.Add(kayit);
            }

            kayit.Durum = durum;
            kayit.KisiselPuan = kisiselPuan;
            kayit.GuncellemeTarihi = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            TempData["CollectionMessage"] = "İçerik profil kütüphanene kaydedildi.";
            return RedirectToAction("Detay", new { id = icerikId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KutuphaneKaydiniSil(int icerikId)
        {
            var currentEmail = GetCurrentEmail();
            if (string.IsNullOrWhiteSpace(currentEmail))
                return RedirectToAction("Login");

            var kayit = await _dbContext.KullaniciIcerikKayitlari
                .FirstOrDefaultAsync(x => x.KullaniciEmail == currentEmail && x.IcerikId == icerikId);

            if (kayit != null)
            {
                _dbContext.KullaniciIcerikKayitlari.Remove(kayit);
                await _dbContext.SaveChangesAsync();
            }

            TempData["CollectionMessage"] = "İçerik profilinden kaldırıldı.";
            return RedirectToAction("Detay", new { id = icerikId });
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

            if (_firebaseIdentityService.IsEnabled)
            {
                if (!_firebaseIdentityService.IsConfigured)
                {
                    ModelState.AddModelError(string.Empty, _firebaseIdentityService.ConfigurationErrorMessage);
                    return View(model);
                }

                var firebaseResult = await _firebaseIdentityService.SignInWithEmailPasswordAsync(email, model.Password);
                if (!firebaseResult.Succeeded || firebaseResult.User is null)
                {
                    ModelState.AddModelError(string.Empty, firebaseResult.ErrorMessage ?? "Firebase girisi basarisiz.");
                    return View(model);
                }

                await SignInFirebaseUserAsync(firebaseResult.User);
                return RedirectToAction("Index");
            }

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

            if (_firebaseIdentityService.IsEnabled)
            {
                if (!_firebaseIdentityService.IsConfigured)
                {
                    ModelState.AddModelError(string.Empty, _firebaseIdentityService.ConfigurationErrorMessage);
                    return View(model);
                }

                var firebaseResult = await _firebaseIdentityService.RegisterWithEmailPasswordAsync(email, model.Password);
                if (!firebaseResult.Succeeded || firebaseResult.User is null)
                {
                    ModelState.AddModelError(string.Empty, firebaseResult.ErrorMessage ?? "Firebase kaydi basarisiz.");
                    return View(model);
                }

                var yeniFirebaseKullanici = new Kullanici
                {
                    KullaniciAdi = username,
                    Email = firebaseResult.User.Email,
                    Password = $"FIREBASE::{firebaseResult.User.Uid}"
                };

                _dbContext.Kullanicilar.Add(yeniFirebaseKullanici);
                await _dbContext.SaveChangesAsync();

                await SignInFirebaseUserAsync(firebaseResult.User, username);
                TempData["AuthMessage"] = "Hesabınız Firebase ile oluşturuldu ve oturumunuz açıldı.";
                return RedirectToAction("Index");
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

        private async Task SignInFirebaseUserAsync(FirebaseAuthUser firebaseUser, string? preferredUsername = null)
        {
            var email = firebaseUser.Email.Trim();
            var mevcutKullanici = await _dbContext.Kullanicilar
                .FirstOrDefaultAsync(x => x.Email.ToLower() == email.ToLower());

            if (mevcutKullanici is null)
            {
                mevcutKullanici = new Kullanici
                {
                    KullaniciAdi = await BuildUniqueUsernameAsync(preferredUsername, email),
                    Email = email,
                    Password = $"FIREBASE::{firebaseUser.Uid}"
                };

                _dbContext.Kullanicilar.Add(mevcutKullanici);
                await _dbContext.SaveChangesAsync();
            }

            HttpContext.Session.SetString("User", mevcutKullanici.KullaniciAdi);
            HttpContext.Session.SetString("UserEmail", mevcutKullanici.Email);

            if (_firebaseIdentityService.IsAdminEmail(mevcutKullanici.Email))
            {
                HttpContext.Session.SetString("AdminAuth", "true");
                HttpContext.Session.SetString("AdminEmail", mevcutKullanici.Email);
            }
            else
            {
                HttpContext.Session.Remove("AdminAuth");
                HttpContext.Session.Remove("AdminEmail");
            }
        }

        private async Task<string> BuildUniqueUsernameAsync(string? preferredUsername, string email)
        {
            var rawValue = string.IsNullOrWhiteSpace(preferredUsername)
                ? email.Split('@')[0]
                : preferredUsername.Trim();

            var normalized = new string(rawValue
                .Where(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '.')
                .ToArray());

            if (string.IsNullOrWhiteSpace(normalized))
                normalized = "kullanici";

            if (normalized.Length < 3)
                normalized = $"{normalized}_uyelik";

            if (normalized.Length > 32)
                normalized = normalized[..32];

            var candidate = normalized;
            var suffix = 2;

            while (await _dbContext.Kullanicilar.AnyAsync(x => x.KullaniciAdi.ToLower() == candidate.ToLower()))
            {
                var suffixText = suffix.ToString();
                var prefixLength = Math.Min(normalized.Length, 40 - suffixText.Length);
                candidate = $"{normalized[..prefixLength]}{suffixText}";
                suffix++;
            }

            return candidate;
        }

        private string? GetCurrentEmail()
            => HttpContext.Session.GetString("UserEmail") ?? HttpContext.Session.GetString("AdminEmail");

        private static string? BuildAvatarPath(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return Uri.IsWellFormedUriString(value, UriKind.Absolute)
                ? value
                : $"/img/avatars/{value}";
        }

        private static string NormalizeProfileSection(string? section)
        {
            return (section ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "favorites" => "favorites",
                "library" => "library",
                "history" => "history",
                _ => "overview"
            };
        }
    }
}
