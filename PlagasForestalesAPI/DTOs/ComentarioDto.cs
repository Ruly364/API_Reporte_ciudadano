namespace PlagasForestalesAPI.DTOs
{
    public class ComentarioCreateDto
    {
        public string Texto { get; set; } = string.Empty;
    }
    public class ComentarioResponseDto
    {
        public int Id { get; set; }
        public string Texto { get; set; } = string.Empty;
        public DateTime FechaHora { get; set; }
        public string NombreUsuario { get; set; } = string.Empty;
        public string RolUsuario { get; set; } = string.Empty;
    }
}
