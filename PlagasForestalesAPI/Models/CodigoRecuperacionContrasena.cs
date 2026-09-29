namespace PlagasForestalesAPI.Models
{
    public class CodigoRecuperacionContrasena
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public string CodigoHash { get; set; } = string.Empty;
        public string Canal { get; set; } = string.Empty;
        public DateTime FechaCreacionUtc { get; set; }
        public DateTime ExpiraUtc { get; set; }
        public DateTime? UsadoUtc { get; set; }
        public int IntentosFallidos { get; set; }
        public Usuario? Usuario { get; set; }
    }
}
