using Microsoft.AspNetCore.Http;

namespace PlagasForestalesAPI.DTOs
{
    public class ReporteCreateDto
    {
        //public int UsuarioId { get; set; }        
        public string? TipoPlaga { get; set; }
        public string Descripcion { get; set; }
        public decimal Latitud { get; set; }
        public decimal Longitud { get; set; }

        public string? EstadoUbicacion { get; set; }
        public string? Municipio { get; set; }
        public string? Localidad { get; set; }

        public int? CantidadArboles { get; set; }

        /// <summary>UUID generado por el dispositivo para evitar duplicados al reintentar.</summary>
        public string? ClienteUuid { get; set; }

        public List<IFormFile>? Imagenes { get; set; }
    }
}
