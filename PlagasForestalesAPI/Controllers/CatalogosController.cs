using Microsoft.AspNetCore.Mvc;
using PlagasForestalesAPI.Data;

namespace PlagasForestalesAPI.Controllers
{
    [ApiController]
    [Route("api/catalogos")]
    public class CatalogosController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CatalogosController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("tipos-plaga")]
        public IActionResult ObtenerTiposPlaga()
        {
            var data = _context.CatTipoPlaga
                .Where(x => x.Activo)
                .Select(x => new
                {
                    x.Id,
                    x.Nombre
                })
                .ToList();

            return Ok(data);
        }
    }
}
