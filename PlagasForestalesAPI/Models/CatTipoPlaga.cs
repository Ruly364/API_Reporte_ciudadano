using System.ComponentModel.DataAnnotations.Schema;

namespace PlagasForestalesAPI.Models
{
    [Table("Cat_TipoPlaga")]

    public class CatTipoPlaga
    {
        public int Id { get; set; }
        public string? Nombre { get; set; }
        public bool Activo { get; set; }
    }
}
