using Microsoft.AspNetCore.Mvc;
using CineFlow.Data;
using CineFlow.Models;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;
using System;
using CineFlow.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CineFlow.Controllers
{
    public class AdminController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly AppDbContext _dbContext;

        public AdminController(IWebHostEnvironment env, AppDbContext dbContext)
        {
            _env = env;
            _dbContext = dbContext;
        }

        private bool IsAdmin() => HttpContext.Session.GetString("AdminAuth") == "true";

        [HttpGet]
        public IActionResult Login() => View(new AdminLogin());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(AdminLogin model)
        {
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

        public async Task<IActionResult> Index()
        {
            if (!IsAdmin()) return RedirectToAction("Login");

            var icerikler = await _dbContext.Icerikler
                .Include(x => x.Yorumlar)
                .OrderBy(x => x.Baslik)
                .ToListAsync();

            return View(icerikler);
        }

        public IActionResult Ekle() => IsAdmin() ? View() : RedirectToAction("Login");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ekle(Icerik model, IFormFile afis)
        {
            if (!IsAdmin()) return RedirectToAction("Login");

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
            if (!IsAdmin()) return RedirectToAction("Login");

            var veri = await _dbContext.Icerikler.FirstOrDefaultAsync(x => x.Id == id);
            return veri != null ? View(veri) : RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Duzenle(Icerik model, IFormFile? afis)
        {
            if (!IsAdmin()) return RedirectToAction("Login");

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
            if (!IsAdmin()) return RedirectToAction("Login");

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
    }
}
