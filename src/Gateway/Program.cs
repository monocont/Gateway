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
// Puerto explícito: http://localhost:4444
// ──────────────────────────────────────────────────────────────────────
builder.WebHost.UseUrls("http://localhost:4444");

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

// Servicio de validación de firma (singleton: sin estado mutable)
builder.Services.AddSingleton<ISignatureValidationService, SignatureValidationService>();

// ── Autenticación JWT Bearer (RS256 via JWKS) ────────────────────────
var jwtIssuer   = builder.Configuration["JwtSettings:Issuer"]   ?? string.Empty;
var jwtAudience = builder.Configuration["JwtSettings:Audience"] ?? string.Empty;
var jwksUri     = builder.Configuration["JwtSettings:JwksUri"]  ?? string.Empty;

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

            // RS256: obtiene la clave pública desde el endpoint JWKS del AutenticacionService
            IssuerSigningKeyResolver = (token, securityToken, kid, parameters) =>
            {
                try
                {
                    // Aceptar certificados auto-firmados en desarrollo
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

// Health check (sin autenticación)
app.MapHealthChecks("/health");

// Autenticación y autorización deben ir ANTES del middleware de firma y Ocelot
app.UseAuthentication();
app.UseAuthorization();

// Middleware de firma HMAC-SHA256 (validación adicional de origen)
app.UseMiddleware<SignatureValidationMiddleware>();

// Ocelot gestiona el enrutamiento hacia los microservicios downstream
await app.UseOcelot();

app.Run();
