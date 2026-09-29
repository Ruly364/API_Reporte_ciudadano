using PlagasForestalesAPI.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Extensions.FileProviders;
using PlagasForestalesAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// =====================
// DB
// =====================
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// =====================
// Controllers & Swagger
// =====================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddScoped<EmailService>();
builder.Services.AddHttpClient<SmsService>();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Escribe: Bearer {tu token}"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// =====================
// JWT
// =====================
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = false,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
            )
        };
    });

builder.Services.AddAuthorization();

// =====================
// CORS
// =====================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader());
});

// =====================
// BUILD APP
// =====================
var app = builder.Build();

// 🔥 MOSTRAR ERRORES REALES
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

Program.ServiceProvider = app.Services;

app.UseStatusCodePages(async context =>
{
    var response = context.HttpContext.Response;
    await response.WriteAsync($"ERROR STATUS: {response.StatusCode}");
});

// =====================
// PATH BASE SOLO EN PRODUCCIÓN
// =====================
if (!app.Environment.IsDevelopment())
{
    app.UsePathBase("/PlagasForestalesAPI");
}

// =====================
// CORS
// =====================
app.UseCors("AllowAll");

// =====================
// SWAGGER
// =====================
app.UseSwagger();

app.UseSwaggerUI(c =>
{
    var prefix = app.Environment.IsDevelopment()
        ? ""
        : "/PlagasForestalesAPI";

    c.SwaggerEndpoint($"{prefix}/swagger/v1/swagger.json", "API V1");
    c.RoutePrefix = "swagger";
});

// =====================
// AUTH
// =====================
app.UseAuthentication();
app.UseAuthorization();

// =====================
// STATIC FILES (UPLOADS)
// =====================

//var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads"); //Conexión local
var uploadsPath = @"C:\inetpub\wwwroot\PlagasForestalesAPI\uploads"; //Conexión remota


if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

app.UseStaticFiles(); // para wwwroot normal

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

// =====================
// MAP
// =====================
app.MapControllers();

app.Run();

public partial class Program
{
    public static IServiceProvider ServiceProvider { get; set; }
}
