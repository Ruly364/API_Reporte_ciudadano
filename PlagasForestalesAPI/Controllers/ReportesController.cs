namespace PlagasForestalesAPI.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using PlagasForestalesAPI.Data;
    using PlagasForestalesAPI.DTOs;
    using PlagasForestalesAPI.Models;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.EntityFrameworkCore;
    using PlagasForestalesAPI.Services;
    using System.IO;
    






    [ApiController]
    [Route("api/reportes")]
    public class ReportesController : ControllerBase
    {
       private readonly EmailService _emailService;
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IServiceScopeFactory _scopeFactory;

        // Limpia el nombre para usarlo como carpeta
        // Ej: "Baja California" → "Baja_California"
        // Ej: "REP-2025-000001" → "REP-2025-000001"
        private static string LimpiarNombreCarpeta(string? nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return "Sin_Estado";

            // Reemplaza espacios por guion bajo y elimina caracteres inválidos
            var limpio = nombre.Trim()
                .Replace(" ", "_")
                .Replace("/", "-")
                .Replace("\\", "-")
                .Replace(":", "-")
                .Replace("*", "")
                .Replace("?", "")
                .Replace("\"", "")
                .Replace("<", "")
                .Replace(">", "")
                .Replace("|", "");

            return limpio;
        }



        public ReportesController(AppDbContext context, IWebHostEnvironment env, EmailService emailService, IServiceScopeFactory scopeFactory)
        {
            _context = context;
            _env = env;
            _emailService = emailService;
            _scopeFactory = scopeFactory;
        }
        
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CrearReporte([FromForm] ReporteCreateDto dto)
        {
            // El UUID hace idempotente el POST: si el teléfono perdió la
            // respuesta después de enviar, el siguiente intento devuelve el
            // mismo folio en lugar de crear un segundo reporte.
            if (!Guid.TryParse(dto.ClienteUuid, out _))
                return BadRequest(new { mensaje = "ClienteUuid es obligatorio y debe ser un UUID válido." });

            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null)
                return Unauthorized();

            int userId = int.Parse(userIdClaim);
            var existente = await _context.Reportes.AsNoTracking().FirstOrDefaultAsync(r =>
                r.UsuarioId == userId && r.ClienteUuid == dto.ClienteUuid);
            if (existente != null)
            {
                return Ok(new
                {
                    mensaje = "Reporte recibido previamente.",
                    folio = existente.Folio,
                    reporteId = existente.Id,
                    yaExistia = true
                });
            }

            // 🔢 GENERAR FOLIO
            var año = DateTime.Now.Year;
            int consecutivo;

            var connection = _context.Database.GetDbConnection();

            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT NEXT VALUE FOR Seq_FolioReportes";
                consecutivo = Convert.ToInt32(await command.ExecuteScalarAsync());
            }

            string folio = $"REP-{año}-{consecutivo.ToString("D6")}";

            try
            {
                // 💾 GUARDAR REPORTE (RÁPIDO)
                var reporte = new Reporte
                {
                    UsuarioId = userId,
                    TipoPlaga = dto.TipoPlaga,
                    Descripcion = dto.Descripcion,
                    Latitud = dto.Latitud,
                    Longitud = dto.Longitud,
                    Fecha = DateTime.Now,
                    Estado = "Pendiente",
                    EstadoUbicacion = dto.EstadoUbicacion,
                    Municipio = dto.Municipio,
                    Localidad = dto.Localidad,
                    CantidadArboles = dto.CantidadArboles,
                    Folio = folio,
                    ClienteUuid = dto.ClienteUuid
                };
                //validaciones
                Console.WriteLine($"IMÁGENES RECIBIDAS DTO: {dto.Imagenes?.Count}");

                _context.Reportes.Add(reporte);
                if (dto.Imagenes == null || !dto.Imagenes.Any())
                {
                    Console.WriteLine("⚠️ NO llegaron imágenes en DTO");
                }

                foreach (var file in Request.Form.Files)
                {
                    Console.WriteLine($"📸 FILE: {file.FileName} - SIZE: {file.Length}");
                }

                //validaciones

                await _context.SaveChangesAsync();

                // ⚡ RESPUESTA INMEDIATA
            //    var respuesta = Ok(new
             //   {
             //       mensaje = "Reporte creado correctamente",
             //       folio = reporte.Folio,
            //        reporteId = reporte.Id
            //    });

                var reporteId = reporte.Id;
                var folioGenerado = reporte.Folio;
                var estadoUbicacion = dto.EstadoUbicacion;
                var imagenes = dto.Imagenes;
                var contentRoot = _env.ContentRootPath;

                // 🔥 PROCESO EN SEGUNDO PLANO

                //   return Ok(new
                //   {
                //       mensaje = "Reporte creado correctamente",
                //       folio = reporte.Folio
                //   });

                //Bloque para imagenes

                // 📸GUARDAR IMÁGENES (ANTES del Task)

                // 📸 GUARDAR IMÁGENES
                if (dto.Imagenes != null && dto.Imagenes.Any())
                {
                    // Estructura: uploads/año/mes/EstadoUbicacion/Folio/
                    var carpetaAño = reporte.Fecha.Year.ToString();
                    var carpetames = reporte.Fecha.Month.ToString("D2");
                    var estadoLimpio = LimpiarNombreCarpeta(reporte.EstadoUbicacion);
                    var folioLimpio = LimpiarNombreCarpeta(reporte.Folio);

                    string carpetaBase = @"C:\inetpub\wwwroot\PlagasForestalesAPI\uploads";
                    string carpetaReporte = Path.Combine(
                        carpetaBase, carpetaAño, carpetames, estadoLimpio, folioLimpio);

                    if (!Directory.Exists(carpetaReporte))
                        Directory.CreateDirectory(carpetaReporte);

                    foreach (var imagen in dto.Imagenes)
                    {
                        if (imagen.Length > 0)
                        {
                            string nombreArchivo = Guid.NewGuid() + Path.GetExtension(imagen.FileName);
                            string rutaCompleta = Path.Combine(carpetaReporte, nombreArchivo);

                            using var stream = new FileStream(rutaCompleta, FileMode.Create);
                            await imagen.CopyToAsync(stream);

                            // Guardamos la ruta relativa: 2025/06/Jalisco/REP-2025-000001/archivo.jpg
                            string rutaRelativa = $"{carpetaAño}/{carpetames}/{estadoLimpio}/{folioLimpio}/{nombreArchivo}";

                            _context.Multimedia.Add(new Multimedia
                            {
                                ReporteId = reporte.Id,
                                UrlArchivo = rutaRelativa,
                                Tipo = "Imagen",
                                FechaRegistro = DateTime.Now
                            });
                        }
                    }

                    await _context.SaveChangesAsync();
                }

                //bloque para correos

                System.IO.File.AppendAllText(
                @"C:\inetpub\wwwroot\PlagasForestalesAPI\error_log_email.txt",
                    $"[{DateTime.Now}] 🚀 Punto previo al Task.Run - Estado: {dto.EstadoUbicacion}\n"
                );



                _ = Task.Run(async () =>
                {
                    //var rutaLog = Path.Combine(Directory.GetCurrentDirectory(), "error_log_email.txt");
                    var rutaLog = @"C:\inetpub\wwwroot\PlagasForestalesAPI\error_log_email.txt";
                    try
                    {
                        System.IO.File.AppendAllText(rutaLog, $"[{DateTime.Now}] ✅ Task.Run iniciado\n");

                        using var scope = _scopeFactory.CreateScope();

                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();

                        System.IO.File.AppendAllText(rutaLog, $"[{DateTime.Now}] 🔍 Buscando responsable para estado: '{dto.EstadoUbicacion}'\n");


                        var responsable = context.ResponsablesEstado
                            .FirstOrDefault(x =>
                                x.EstadoResponsable == dto.EstadoUbicacion &&
                                x.Activo);

                        if (responsable != null)
                        {
                            System.IO.File.AppendAllText(rutaLog, $"[{DateTime.Now}] 📧 Responsable encontrado: {responsable.Email}\n");

                            //Plantilla HTML para correo

                            var html = emailService.CargarPlantillaCorreo();

                            html = html.Replace("{{FOLIO}}", reporte.Folio);
                            html = html.Replace("{{TIPO_PLAGA}}", reporte.TipoPlaga);
                            html = html.Replace("{{ESTADO}}", reporte.EstadoUbicacion);
                            html = html.Replace("{{MUNICIPIO}}", reporte.Municipio);
                            html = html.Replace("{{LOCALIDAD}}", reporte.Localidad ?? "Sin localidad");
                            html = html.Replace("{{ARBOLES}}", reporte.CantidadArboles.ToString());
                            html = html.Replace("{{LATITUD}}", reporte.Latitud.ToString());
                            html = html.Replace("{{LONGITUD}}", reporte.Longitud.ToString());
                            html = html.Replace("{{DESCRIPCION}}", reporte.Descripcion);
                            html = html.Replace(
                                "{{FECHA}}",
                                DateTime.Now.ToString("dd/MM/yyyy HH:mm")
                            );


                            //    await emailService.EnviarCorreo(
                            //        responsable.Email,
                            //        "Nuevo reporte de plaga",
                            //            $@"
                            //    Se ha registrado un nuevo reporte:

                            //    Folio: {reporte.Folio}
                            //    Tipo: {reporte.TipoPlaga}
                            //    Descripción: {reporte.Descripcion}
                            //    Ubicación: {reporte.EstadoUbicacion}, {reporte.Municipio}                               
                            //    "
                            //    );

                            await emailService.EnviarCorreo(
                              responsable.Email,
                                "Nuevo reporte de plaga forestal",
                               html
                             );

                            System.IO.File.AppendAllText(rutaLog, $"[{DateTime.Now}] ✅ Correo enviado a {responsable.Email}\n");
                        }
                        else
                        {
                            System.IO.File.AppendAllText(rutaLog, $"[{DateTime.Now}] ⚠️ NO se encontró responsable para '{dto.EstadoUbicacion}'\n");

                            var todos = context.ResponsablesEstado.ToList();
                            foreach (var r in todos)
                            {
                                System.IO.File.AppendAllText(rutaLog, $"  → ID:{r.Id} | Estado:'{r.EstadoResponsable}' | Activo:{r.Activo} | Email:{r.Email}\n");
                            }

                        }
                    }
                    catch (Exception ex)
                    {
                    System.IO.File.AppendAllText(rutaLog, $"[{DateTime.Now}] ❌ ERROR:\n{ex}\n\n");


                      // var rutaLog = Path.Combine(
                      //  Directory.GetCurrentDirectory(),
                      //  "error_log_email.txt"
                      // );
                       // System.IO.File.AppendAllText(rutaLog, $"[{DateTime.Now}] ERROR EMAIL:\n{ex}\n\n");
                    }
                });

                // ✅ RESPUESTA AL FINAL
                return Ok(new
                {
                    mensaje = "Reporte creado correctamente",
                    folio = reporte.Folio
                });


                // 🚀 RESPONDER YA (NO ESPERAR)
                //return respuesta;
            }
            catch (DbUpdateException) when (!string.IsNullOrWhiteSpace(dto.ClienteUuid))
            {
                // Dos solicitudes simultáneas con el mismo UUID compiten por
                // el índice único; la segunda recupera el reporte creado.
                var existentePorCarrera = await _context.Reportes.AsNoTracking().FirstOrDefaultAsync(r =>
                    r.UsuarioId == userId && r.ClienteUuid == dto.ClienteUuid);
                if (existentePorCarrera != null)
                {
                    return Ok(new
                    {
                        mensaje = "Reporte recibido previamente.",
                        folio = existentePorCarrera.Folio,
                        reporteId = existentePorCarrera.Id,
                        yaExistia = true
                    });
                }
                throw;
            }
            catch (Exception ex)
            {
                //return StatusCode(500, new { mensaje = "Error interno al crear el reporte", error = ex.Message, inner = ex.InnerException?.Message });
                return StatusCode(500, ex.ToString());
            }
        }


        // Para mapa público o usuarios
        [HttpGet]
        public IActionResult ObtenerReportes()
        {
            var reportes = _context.Reportes
                .Select(r => new
                {
                    r.Id,
                    r.TipoPlaga,
                    r.Descripcion,
                    r.Latitud,
                    r.Longitud,
                    r.Fecha,
                    r.Estado
                })
                .ToList();

            return Ok(reportes);
        }

        [Authorize(Roles = "Administrador")]
        [HttpGet("admin")]
        public IActionResult ObtenerReportesAdmin([FromQuery] string? estadoUbicacion,
            [FromQuery] string? status,
            [FromQuery] DateTime? fechaInicio,
            [FromQuery] DateTime? fechaFin,
            [FromQuery] string? tipoPlaga,
            [FromQuery] int? cantidadArbolesMin,
            [FromQuery] int? cantidadArbolesMax)
        {
            var query = _context.Reportes
       .Include(r => r.Usuario)
       .OrderByDescending(r => r.Fecha)
       .AsQueryable();

            if (!string.IsNullOrEmpty(estadoUbicacion))
                query = query.Where(r => r.EstadoUbicacion == estadoUbicacion);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(r => r.Estado == status);

            if (fechaInicio.HasValue)
                query = query.Where(r => r.Fecha >= fechaInicio.Value);

            if (fechaFin.HasValue)
                query = query.Where(r => r.Fecha <= fechaFin.Value);

            if (!string.IsNullOrEmpty(tipoPlaga))
                query = query.Where(r => r.TipoPlaga == tipoPlaga);

            if (cantidadArbolesMin.HasValue)
                query = query.Where(r => r.CantidadArboles >= cantidadArbolesMin.Value);

            if (cantidadArbolesMax.HasValue)
                query = query.Where(r => r.CantidadArboles <= cantidadArbolesMax.Value);

            var reportes = query.Select(r => new
            {
                r.Id,
                r.Folio,
                r.TipoPlaga,
                r.Descripcion,
                r.Latitud,
                r.Longitud,
                r.Fecha,
                r.Estado,
                r.EstadoUbicacion,
                r.Municipio,
                r.Localidad,
                r.CantidadArboles,
                Usuario = new
                {
                    r.Usuario!.Nombre,
                    r.Usuario.Email,
                    r.Usuario.Telefono
                },
                Imagenes = _context.Multimedia
                    .Where(m => m.ReporteId == r.Id)
                    .Select(m => $"{Request.Scheme}://{Request.Host}/PlagasForestalesAPI/uploads/{m.UrlArchivo}")
                    .ToList(),
                Comentarios = _context.Comentarios
                    .Where(c => c.ReporteId == r.Id)
                    .OrderBy(c => c.FechaHora)
                    .Select(c => new
                    {
                        c.Id,
                        c.Texto,
                        c.FechaHora,
                        NombreUsuario = c.Usuario!.Nombre,
                        RolUsuario = c.Usuario.Rol
                    }).ToList(),

                HistorialEstatus = _context.HistorialEstatus
                    .Where(h => h.ReporteId == r.Id)
                    .OrderByDescending(h => h.FechaHora)
                    .Select(h => new
                    {
                        h.EstadoAnterior,
                        h.EstadoNuevo,
                        h.FechaHora,
                        NombreUsuario = h.Usuario!.Nombre,
                        RolUsuario = h.Usuario.Rol
                    }).ToList()



            }).ToList();

            return Ok(reportes);
        }



        [Authorize(Roles = "Administrador,Supervisor")]
        [HttpPut("{id}/estado")]
        public async Task<IActionResult> CambiarEstado(int id, [FromBody] string nuevoEstado)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null) return Unauthorized();
            int userId = int.Parse(userIdClaim);

            var reporte = await _context.Reportes.FindAsync(id);
            if (reporte == null) return NotFound();

            // Supervisor solo puede cambiar reportes de su estado
            if (User.IsInRole("Supervisor"))
            {
                var supervisor = await _context.Usuarios.FindAsync(userId);
                if (supervisor?.SupervisorEstado != reporte.EstadoUbicacion)
                    return Forbid();
            }

            // Guardar historial antes de cambiar
            var historial = new HistorialEstatus
            {
                ReporteId = id,
                UsuarioId = userId,
                EstadoAnterior = reporte.Estado,
                EstadoNuevo = nuevoEstado,
                FechaHora = DateTime.Now
            };

            reporte.Estado = nuevoEstado;

            _context.HistorialEstatus.Add(historial);
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Estado actualizado", estado = nuevoEstado });
        }

        [Authorize(Roles = "Supervisor")]
        [HttpGet("supervisor")]
        public async Task<IActionResult> ObtenerReportesSupervisor(
            [FromQuery] string? status,
            [FromQuery] string? tipoPlaga,
            [FromQuery] DateTime? fechaInicio,
            [FromQuery] DateTime? fechaFin)
        {
            var userIdClaim = User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null) return Unauthorized();

            var supervisor = await _context.Usuarios
                .FindAsync(int.Parse(userIdClaim));

            if (supervisor?.SupervisorEstado == null)
                return BadRequest(new { mensaje = "Supervisor sin estado asignado" });

            var query = _context.Reportes
                .Include(r => r.Usuario)
                .Where(r => r.EstadoUbicacion == supervisor.SupervisorEstado)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(r => r.Estado == status);

            if (!string.IsNullOrEmpty(tipoPlaga))
                query = query.Where(r => r.TipoPlaga == tipoPlaga);

            if (fechaInicio.HasValue)
                query = query.Where(r => r.Fecha >= fechaInicio.Value);

            if (fechaFin.HasValue)
                query = query.Where(r => r.Fecha <= fechaFin.Value);

            var reportes = query
                .OrderByDescending(r => r.Fecha)
                .Select(r => new
                {
                    r.Id,
                    r.Folio,
                    r.TipoPlaga,
                    r.Descripcion,
                    r.Latitud,
                    r.Longitud,
                    r.Fecha,
                    r.Estado,
                    r.EstadoUbicacion,
                    r.Municipio,
                    r.Localidad,
                    r.CantidadArboles,
                    Imagenes = _context.Multimedia
                    .Where(m => m.ReporteId == r.Id)
                    .Select(m => $"{Request.Scheme}://{Request.Host}/PlagasForestalesAPI/uploads/{m.UrlArchivo}")
                    .ToList(),

                    Usuario = new
                    {
                        r.Usuario!.Nombre,
                        r.Usuario.Email,
                        r.Usuario.Telefono
                    },
                    Comentarios = _context.Comentarios
                        .Where(c => c.ReporteId == r.Id)
                        .OrderBy(c => c.FechaHora)
                        .Select(c => new
                        {
                            c.Id,
                            c.Texto,
                            c.FechaHora,
                            NombreUsuario = c.Usuario!.Nombre,
                            RolUsuario = c.Usuario.Rol
                        }).ToList(),
                    HistorialEstatus = _context.HistorialEstatus
                        .Where(h => h.ReporteId == r.Id)
                        .OrderByDescending(h => h.FechaHora)
                        .Select(h => new
                        {
                            h.EstadoAnterior,
                            h.EstadoNuevo,
                            h.FechaHora,
                            NombreUsuario = h.Usuario!.Nombre,
                            RolUsuario = h.Usuario.Rol
                        }).ToList()
                }).ToList();

            return Ok(reportes);
        }

        [Authorize(Roles = "Administrador,Supervisor")]
        [HttpPost("{id}/comentarios")]
        public async Task<IActionResult> AgregarComentario(int id, [FromBody] ComentarioCreateDto dto)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null) return Unauthorized();
            int userId = int.Parse(userIdClaim);

            var reporte = await _context.Reportes.FindAsync(id);
            if (reporte == null) return NotFound();

            // Supervisor solo puede comentar en reportes de su estado
            if (User.IsInRole("Supervisor"))
            {
                var supervisor = await _context.Usuarios.FindAsync(userId);
                if (supervisor?.SupervisorEstado != reporte.EstadoUbicacion)
                    return Forbid();
            }

            var comentario = new Comentario
            {
                ReporteId = id,
                UsuarioId = userId,
                Texto = dto.Texto,
                FechaHora = DateTime.Now
            };

            _context.Comentarios.Add(comentario);
            await _context.SaveChangesAsync();

            var usuario = await _context.Usuarios.FindAsync(userId);

            return Ok(new
            {
                comentario.Id,
                comentario.Texto,
                comentario.FechaHora,
                NombreUsuario = usuario!.Nombre,
                RolUsuario = usuario.Rol
            });
        }




        [Authorize]
        [HttpGet("mis-reportes")]
        public IActionResult MisReportes()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (userIdClaim == null)
                return Unauthorized();

            int userId = int.Parse(userIdClaim);

            var reportes = _context.Reportes
                .Where(r => r.UsuarioId == userId) 
                .Select(r => new
                {
                    r.Id,
                    r.Folio,
                    r.TipoPlaga,
                    r.Descripcion,
                    r.Latitud,
                    r.Longitud,
                    r.Estado,
                    r.Fecha,
                    r.EstadoUbicacion,
                    r.Municipio,
                    r.Localidad,
                    r.CantidadArboles,

                    imagenes = _context.Multimedia
                    .Where(m => m.ReporteId == r.Id)                    
                    .Select(m => $"{Request.Scheme}://{Request.Host}/PlagasForestalesAPI/uploads/{m.UrlArchivo}")
                    //.Select(m => $"http://10.55.0.170:5036/uploads/{m.UrlArchivo}") //Url para mostrar las imagenes de reportes enviados
                    .ToList()
                })
                .ToList();

            return Ok(reportes);
        }







    }
}
