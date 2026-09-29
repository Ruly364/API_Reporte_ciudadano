namespace PlagasForestalesAPI.Models
{
    public class Reporte
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public string? TipoPlaga { get; set; }
        public string? Descripcion { get; set; }
        public decimal Latitud { get; set; }
        public decimal Longitud { get; set; }
        public DateTime Fecha { get; set; }
        public string Estado { get; set; }
        public string? EstadoUbicacion { get; set; }
        public string? Municipio { get; set; }
        public string? Localidad { get; set; }
        public int? CantidadArboles { get; set; }
        public string? Folio { get; set; }
        public string? ClienteUuid { get; set; }
        public Usuario? Usuario { get; set; }
    }
}
