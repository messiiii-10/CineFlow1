using CineFlow.Data;
using CineFlow.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CineFlow.Controllers
{
    /// <summary>
    /// Basit JSON API — mobil uygulama veya harici istemciler için.
    /// Örnek uç noktalar:
    ///   GET /api/catalog/populer
    ///   GET /api/catalog/{id}
    ///   GET /api/catalog/search?q=naruto
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class CatalogApiController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        public CatalogApiController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // GET /api/catalog/populer
        [HttpGet("populer")]
        public async Task<IActionResult> GetPopularAsync()
        {
            var items = await _dbContext.Icerikler
                .OrderByDescending(i => i.Populerlik ?? 0)
                .ThenByDescending(i => i.Skor ?? 0)
                .Take(20)
                .Select(i => new
                {
                    i.Id,
                    i.Baslik,
                    Tur = i.TurEtiketi,
                    i.Kategori,
                    Skor = i.Skor,
                    Populerlik = i.Populerlik,
                    Poster = i.GorselKaynak,
                    Banner = i.BannerKaynak
                })
                .ToListAsync();

            return Ok(items);
        }

        // GET /api/catalog/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var item = await _dbContext.Icerikler
                .Include(i => i.Yorumlar)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (item is null)
                return NotFound();

            var dto = new
            {
                item.Id,
                item.Baslik,
                item.Aciklama,
                item.Tur,
                TurEtiketi = item.TurEtiketi,
                item.Kategori,
                item.Skor,
                item.Populerlik,
                item.BannerKaynak,
                item.GorselKaynak,
                YorumSayisi = item.Yorumlar.Count
            };

            return Ok(dto);
        }

        // GET /api/catalog/search?q=...
        [HttpGet("search")]
        public async Task<IActionResult> SearchAsync([FromQuery] string? q, [FromQuery] IcerikTuru? tur)
        {
            var query = _dbContext.Icerikler.AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                query = query.Where(i =>
                    i.Baslik.Contains(q) ||
                    (i.Kategori != null && i.Kategori.Contains(q)) ||
                    (i.Yaraticilar != null && i.Yaraticilar.Contains(q)));
            }

            if (tur.HasValue)
            {
                query = query.Where(i => i.Tur == tur.Value);
            }

            var results = await query
                .OrderBy(i => i.Baslik)
                .Take(30)
                .Select(i => new
                {
                    i.Id,
                    i.Baslik,
                    Tur = i.TurEtiketi,
                    i.Kategori,
                    Poster = i.GorselKaynak
                })
                .ToListAsync();

            return Ok(results);
        }
    }
}

