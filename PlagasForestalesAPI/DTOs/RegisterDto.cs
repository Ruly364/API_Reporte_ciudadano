using System.ComponentModel.DataAnnotations;
namespace PlagasForestalesAPI.DTOs
{
    public class RegisterDto
    {
        public string Nombre { get; set; }
        public string Password { get; set; }

        public string? Email { get; set; }
        public string? Telefono { get; set; }

        public int Edad { get; set; }
        public string Genero { get; set; }
        public string Estado { get; set; }
        public string Municipio { get; set; }
        public bool EsEtnia { get; set; }
        public string? Etnia { get; set; }
    }
}
