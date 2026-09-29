using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using PlagasForestalesAPI.Data;
using PlagasForestalesAPI.DTOs;
using PlagasForestalesAPI.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using PlagasForestalesAPI.Services;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;
    private readonly EmailService _emailService;
    private readonly SmsService _smsService;

    public AuthController(AppDbContext context, IConfiguration config, EmailService emailService, SmsService smsService)
    {
        _context = context;
        _config = config;
        _emailService = emailService;
        _smsService = smsService;
    }

    [HttpPost("password-reset/request")]
    public async Task<IActionResult> SolicitarRecuperacion([FromBody] SolicitarRecuperacionDto dto)
    {
        var identificador = dto.Identificador?.Trim();
        var canal = dto.Canal?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(identificador) || (canal != "email" && canal != "sms"))
            return BadRequest(new { mensaje = "Datos de recuperación inválidos." });

        var usuario = identificador.Contains('@')
            ? await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == identificador)
            : await _context.Usuarios.FirstOrDefaultAsync(u => u.Telefono == identificador);

        const string respuestaEnviada = "Te enviamos un código de recuperación.";
        if (usuario == null)
            return NotFound(new { mensaje = "Este usuario no existe o no se encuentra registrado." });

        var destino = canal == "email" ? usuario.Email : usuario.Telefono;
        if (string.IsNullOrWhiteSpace(destino))
            return NotFound(new { mensaje = "Este usuario no existe o no se encuentra registrado." });

        var ultimo = await _context.CodigosRecuperacionContrasena
            .Where(c => c.UsuarioId == usuario.Id)
            .OrderByDescending(c => c.FechaCreacionUtc)
            .FirstOrDefaultAsync();
        if (ultimo != null && ultimo.FechaCreacionUtc > DateTime.UtcNow.AddMinutes(-1))
            return StatusCode(429, new { mensaje = "Espera un minuto antes de solicitar otro código." });

        var codigo = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var registro = new CodigoRecuperacionContrasena
        {
            UsuarioId = usuario.Id,
            CodigoHash = HashCodigo(codigo),
            Canal = canal,
            FechaCreacionUtc = DateTime.UtcNow,
            ExpiraUtc = DateTime.UtcNow.AddMinutes(15),
            IntentosFallidos = 0
        };
        _context.CodigosRecuperacionContrasena.Add(registro);
        await _context.SaveChangesAsync();

        try
        {
            var enviado = canal == "email"
                ? await EnviarCodigoCorreo(destino, codigo)
                : await _smsService.EnviarCodigo(destino, codigo);
            if (!enviado)
            {
                _context.CodigosRecuperacionContrasena.Remove(registro);
                await _context.SaveChangesAsync();
                return StatusCode(503, new { mensaje = canal == "sms"
                    ? "El envío por SMS no está configurado actualmente. Usa el correo asociado a tu cuenta."
                    : "No fue posible enviar el correo. Intenta más tarde." });
            }
        }
        catch
        {
            _context.CodigosRecuperacionContrasena.Remove(registro);
            await _context.SaveChangesAsync();
            return StatusCode(503, new { mensaje = "No fue posible enviar el código. Intenta más tarde." });
        }

        return Ok(new { mensaje = respuestaEnviada });
    }

    [HttpPost("password-reset/confirm")]
    public async Task<IActionResult> ConfirmarRecuperacion([FromBody] ConfirmarRecuperacionDto dto)
    {
        var identificador = dto.Identificador?.Trim();
        if (string.IsNullOrWhiteSpace(identificador) || string.IsNullOrWhiteSpace(dto.Codigo) ||
            string.IsNullOrWhiteSpace(dto.NuevaContrasena) || dto.NuevaContrasena.Length < 8)
            return BadRequest(new { mensaje = "Verifica el código y usa una contraseña de al menos 8 caracteres." });

        var usuario = identificador.Contains('@')
            ? await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == identificador)
            : await _context.Usuarios.FirstOrDefaultAsync(u => u.Telefono == identificador);
        if (usuario == null) return BadRequest(new { mensaje = "El código no es válido o ha vencido." });

        var codigo = await _context.CodigosRecuperacionContrasena
            .Where(c => c.UsuarioId == usuario.Id && c.UsadoUtc == null && c.ExpiraUtc > DateTime.UtcNow)
            .OrderByDescending(c => c.FechaCreacionUtc)
            .FirstOrDefaultAsync();
        if (codigo == null || codigo.IntentosFallidos >= 5)
            return BadRequest(new { mensaje = "El código no es válido o ha vencido." });

        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(codigo.CodigoHash), Convert.FromHexString(HashCodigo(dto.Codigo))))
        {
            codigo.IntentosFallidos++;
            await _context.SaveChangesAsync();
            return BadRequest(new { mensaje = "El código no es válido o ha vencido." });
        }

        usuario.PasswordHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(dto.NuevaContrasena)));
        codigo.UsadoUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(new { mensaje = "Contraseña actualizada correctamente." });
    }

    private string HashCodigo(string codigo) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes($"{codigo}:{_config["Jwt:Key"]}")));

    private async Task<bool> EnviarCodigoCorreo(string destino, string codigo)
    {
        var seguro = HtmlEncoder.Default.Encode(codigo);
        await _emailService.EnviarCorreo(destino, "Código para recuperar tu contraseña",
            $"<p>Tu código de recuperación es <strong style=\"font-size:22px\">{seguro}</strong>.</p>" +
            "<p>Vence en 15 minutos. No lo compartas con nadie.</p>", incluirSeguimiento: false);
        return true;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginDto dto)
    {
        if (string.IsNullOrEmpty(dto.Password))
            return BadRequest("La contraseña es obligatoria");

        if (string.IsNullOrEmpty(dto.Email) && string.IsNullOrEmpty(dto.Telefono))
            return BadRequest("Debe enviar correo o teléfono");

        var hash = Convert.ToBase64String(
            SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(dto.Password)));

        Usuario? user = null;

        if (!string.IsNullOrEmpty(dto.Email))
        {
            user = _context.Usuarios
                .FirstOrDefault(u => u.Email == dto.Email && u.PasswordHash == hash);
        }
        else if (!string.IsNullOrEmpty(dto.Telefono))
        {
            user = _context.Usuarios
                .FirstOrDefault(u => u.Telefono == dto.Telefono && u.PasswordHash == hash);
        }

        if (user == null)
            return Unauthorized("Credenciales inválidas");

        var claims = new[]
        {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Role, user.Rol),
        new Claim("login", user.Email ?? user.Telefono!),
        new Claim("nombre", user.Nombre ?? "")
    };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.Now.AddMinutes(
                int.Parse(_config["Jwt:ExpireMinutes"])),
            signingCredentials: new SigningCredentials(
                key, SecurityAlgorithms.HmacSha256)
        );

        return Ok(new
        {
            token = new JwtSecurityTokenHandler().WriteToken(token),
            rol = user.Rol,
            nombre = user.Nombre
        });
    }


    [HttpPost("register")]
    public IActionResult Register([FromBody] RegisterDto dto)
    {
        if (string.IsNullOrEmpty(dto.Password))
            return BadRequest("La contraseña es obligatoria");

        if (string.IsNullOrEmpty(dto.Email) && string.IsNullOrEmpty(dto.Telefono))
            return BadRequest("Debe registrar correo, teléfono o ambos");

        if (!string.IsNullOrEmpty(dto.Email) &&
            _context.Usuarios.Any(u => u.Email == dto.Email))
            return BadRequest("El correo electrónico ya está asociado a una cuenta activa");

        if (!string.IsNullOrEmpty(dto.Telefono) &&
            _context.Usuarios.Any(u => u.Telefono == dto.Telefono))
            return BadRequest("Número de teléfono ya está asociado a una cuenta activa");

        var hash = Convert.ToBase64String(
            SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(dto.Password)));

        var usuario = new Usuario
        {
            Nombre = dto.Nombre,
            Email = dto.Email,
            Telefono = dto.Telefono,
            PasswordHash = hash,
            Rol = "Usuario",

            Edad = dto.Edad,
            Genero = dto.Genero,
            Estado = dto.Estado,
            Municipio = dto.Municipio,
            EsEtnia = dto.EsEtnia,
            Etnia = dto.EsEtnia ? dto.Etnia : null
        };

        _context.Usuarios.Add(usuario);
        _context.SaveChanges();

        return Ok("Usuario registrado correctamente");
    }

}
