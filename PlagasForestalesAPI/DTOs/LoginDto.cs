using System.ComponentModel.DataAnnotations;
namespace PlagasForestalesAPI.DTOs
{
    public class LoginDto
    {       
        public string? Email { get; set; }
        public string? Telefono { get; set; } 
        [Required(ErrorMessage ="Ingrese la contraseña")]
        public string Password { get; set; }
    }
}
