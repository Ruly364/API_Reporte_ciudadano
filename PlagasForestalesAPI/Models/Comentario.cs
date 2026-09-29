namespace PlagasForestalesAPI.Models
{
    public class Comentario
    {
        public int Id { get; set; }
        public int ReporteId { get; set; }
        public int UsuarioId { get; set; }
        public string Texto { get; set; } = string.Empty;
        public DateTime FechaHora { get; set; } = DateTime.Now;

        // Navegación
        public Reporte? Reporte { get; set; }
        public Usuario? Usuario { get; set; }
    }
}
