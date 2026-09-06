using System.Net;
using System.Text;
using System.Text.Json;
using Gateway.Configuration;
using Gateway.Services;
using Microsoft.Extensions.Options;

namespace Gateway.Middleware;

/// <summary>
/// Middleware que intercepta todas las solicitudes HTTP entrantes y valida
/// su firma HMAC-SHA256 antes de permitir el paso al pipeline de Ocelot.
/// </summary>
public class SignatureValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ISignatureValidationService _validationService;
    private readonly GatewaySettings _settings;
    private readonly ILogger<SignatureValidationMiddleware> _logger;

    public SignatureValidationMiddleware(
        RequestDelegate next,
        ISignatureValidationService validationService,
        IOptions<GatewaySettings> settings,
        ILogger<SignatureValidationMiddleware> logger)
    {
        _next = next;
        _validationService = validationService;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Eximir preflights CORS OPTIONS
        if (HttpMethods.IsOptions(context.Request.Method))
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;

        // Verificar si la ruta está exenta de validación
        if (IsPathExcluded(path))
        {
            _logger.LogDebug("Ruta '{Path}' exenta de validación de firma.", path);
            await _next(context);
            return;
        }

        // Habilitar buffering para leer el body múltiples veces
        context.Request.EnableBuffering();

        string body;
        using (var reader = new StreamReader(
            context.Request.Body,
            encoding: Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true))
        {
            body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0; // Rebobinar para Ocelot
        }

        // Validar la firma
        if (!_validationService.ValidateSignature(context.Request, body))
        {
            _logger.LogWarning(
                "Acceso denegado. IP='{Ip}', Ruta='{Path}'.",
                context.Connection.RemoteIpAddress,
                path);

            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.ContentType = "application/json";

            var error = new
            {
                status = 401,
                error = "Unauthorized",
                message = "Firma inválida o headers de autenticación incorrectos."
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(error));
            return;
        }

        await _next(context);
    }

    /// <summary>
    /// Indica si la ruta está en la lista de rutas exentas de validación.
    /// </summary>
    private bool IsPathExcluded(string path)
        => _settings.SkipSignatureForPaths
            .Any(excluded => path.StartsWith(excluded, StringComparison.OrdinalIgnoreCase));
}
