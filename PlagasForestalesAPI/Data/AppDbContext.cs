using Microsoft.EntityFrameworkCore;
using PlagasForestalesAPI.Models;

namespace PlagasForestalesAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Reporte> Reportes { get; set; }
        public DbSet<Multimedia> Multimedia { get; set; }
        public DbSet<CatTipoPlaga> CatTipoPlaga { get; set; }
        public DbSet<ResponsableEstado> ResponsablesEstado { get; set; }
        public DbSet<Comentario> Comentarios { get; set; }
        public DbSet<HistorialEstatus> HistorialEstatus { get; set; }
        public DbSet<CodigoRecuperacionContrasena> CodigosRecuperacionContrasena { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Reporte>(entity =>
            {
                entity.Property(e => e.Latitud)
                      .HasPrecision(18, 10);

                entity.Property(e => e.Longitud)
                      .HasPrecision(18, 10);

                entity.HasIndex(e => new { e.UsuarioId, e.ClienteUuid })
                      .IsUnique()
                      .HasFilter("[ClienteUuid] IS NOT NULL");
            });

            modelBuilder.Entity<CodigoRecuperacionContrasena>(entity =>
            {
                entity.HasIndex(e => new { e.UsuarioId, e.ExpiraUtc });
                entity.Property(e => e.CodigoHash).HasMaxLength(64);
                entity.Property(e => e.Canal).HasMaxLength(10);
            });
        }
    }
}
