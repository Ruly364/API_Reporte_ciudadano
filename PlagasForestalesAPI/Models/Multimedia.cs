namespace PlagasForestalesAPI.Models
{
    public class Multimedia
    {
        public int Id { get; set; }
        public int ReporteId { get; set; }
        public string UrlArchivo { get; set; }
        public string Tipo { get; set; }
        public DateTime FechaRegistro { get; set; }
    }
}
