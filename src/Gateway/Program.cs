using System.Text;
using Gateway.Configuration;
using Gateway.Middleware;
using Gateway.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

// ──────────────────────────────────────────────────────────────────────
// Puerto explícito: http://localhost:5050
// ──────────────────────────────────────────────────────────────────────
builder.WebHost.UseUrls("http://localhost:5050");

// Límite de carga para archivos (SIRE compras/ventas hasta 50MB)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 52428800; // 50 MB
});

// ──────────────────────────────────────────────────────────────────────
// Archivos de configuración
// ──────────────────────────────────────────────────────────────────────
builder.Configuration
    .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

// ──────────────────────────────────────────────────────────────────────
// Registro de servicios
// ──────────────────────────────────────────────────────────────────────

// Configuración tipada del Gateway
builder.Services.Configure<GatewaySettings>(
    builder.Configuration.GetSection(GatewaySettings.SectionName));

// Configuración de CORS para el Frontend de Angular (http://localhost:4200)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Servicio de validación de firma (singleton: sin estado mutable)
builder.Services.AddSingleton<ISignatureValidationService, SignatureValidationService>();

// ── Autenticación JWT Bearer (RS256 via JWKS) ────────────────────────
var jwtIssuer   = builder.Configuration["JwtSettings:Issuer"]   ?? "Service.Seguridad";
var jwtAudience = builder.Configuration["JwtSettings:Audience"] ?? "Monocont";
var jwksUri     = builder.Configuration["JwtSettings:JwksUri"]  ?? "http://localhost:5000/.well-known/jwks";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.RequireHttpsMetadata = false; // true en producción
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtIssuer,
            ValidAudience            = jwtAudience,
            ClockSkew                = TimeSpan.Zero,

            // RS256: obtiene la clave pública desde el endpoint JWKS de Service.Seguridad
            IssuerSigningKeyResolver = (token, securityToken, kid, parameters) =>
            {
                try
                {
                    var handler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback =
                            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    };

                    using var http = new HttpClient(handler);
                    var json = http.GetStringAsync(jwksUri).GetAwaiter().GetResult();
                    var jwks = new Microsoft.IdentityModel.Tokens.JsonWebKeySet(json);
                    return jwks.Keys;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(
                        $"[Gateway] Error al obtener claves JWKS desde '{jwksUri}': {ex.Message}");
                    return Enumerable.Empty<SecurityKey>();
                }
            }
        };
    });

builder.Services.AddAuthorization();

// Ocelot
builder.Services.AddOcelot(builder.Configuration);

// Health checks para monitoreo en contenedores
builder.Services.AddHealthChecks();

// ──────────────────────────────────────────────────────────────────────
// Pipeline HTTP
// ──────────────────────────────────────────────────────────────────────
var app = builder.Build();

// 1. CORS debe ser lo primero para responder a preflights OPTIONS de Angular
app.UseCors("AllowAngularApp");

// 2. Health check (sin autenticación)
app.MapHealthChecks("/health");

// 3. Autenticación y autorización
app.UseAuthentication();
app.UseAuthorization();

// 4. Middleware de firma HMAC-SHA256 (para clientes B2B/externos; excluye SPA)
app.UseMiddleware<SignatureValidationMiddleware>();

// 5. Ocelot gestiona el enrutamiento hacia los microservicios downstream
await app.UseOcelot();

app.Run();
