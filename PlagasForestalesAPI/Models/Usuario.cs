namespace PlagasForestalesAPI.Models
{
    public class Usuario
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string Rol { get; set; }
        public string? Telefono { get; set; }
        public int Edad { get; set; }
        public string? Genero { get; set; }
        public string? Estado { get; set; }
        public string? Municipio { get; set; }
        public bool? EsEtnia { get; set; }
        public string? Etnia { get; set; }
        public string? SupervisorEstado { get; set; }
    }
}
