using System.Security.Cryptography;
using System.Text;
using Gateway.Configuration;
using Microsoft.Extensions.Options;

namespace Gateway.Services;

/// <summary>
/// Implementación del servicio de validación de firma HMAC-SHA256.
/// Verifica que las solicitudes provengan de clientes autorizados
/// comparando la firma enviada con la calculada localmente.
/// </summary>
public class SignatureValidationService : ISignatureValidationService
{
    private readonly GatewaySettings _settings;
    private readonly ILogger<SignatureValidationService> _logger;

    public SignatureValidationService(
        IOptions<GatewaySettings> settings,
        ILogger<SignatureValidationService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool ValidateSignature(HttpRequest request, string body)
    {
        // 1. Leer headers de autenticación
        if (!request.Headers.TryGetValue("X-Api-Key", out var apiKey) ||
            !request.Headers.TryGetValue("X-Timestamp", out var timestampStr) ||
            !request.Headers.TryGetValue("X-Signature", out var receivedSignature))
        {
            _logger.LogWarning("Solicitud rechazada: headers X-Api-Key, X-Timestamp o X-Signature faltantes.");
            return false;
        }

        // 2. Validar timestamp (anti-replay)
        if (!long.TryParse(timestampStr, out var timestamp))
        {
            _logger.LogWarning("Solicitud rechazada: timestamp inválido '{Timestamp}'.", (string?)timestampStr);
            return false;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var diff = Math.Abs(now - timestamp);

        if (diff > _settings.AllowedTimestampDiffSeconds)
        {
            _logger.LogWarning("Solicitud rechazada: timestamp fuera de rango. Diferencia: {Diff}s.", diff);
            return false;
        }

        // 3. Construir mensaje firmado: {METHOD}:{PATH}:{TIMESTAMP}:{BODY}
        var path = request.Path.Value ?? string.Empty;
        var method = request.Method.ToUpperInvariant();
        var messageToSign = $"{method}:{path}:{timestamp}:{body}";

        // 4. Calcular firma HMAC-SHA256
        var expectedSignature = ComputeHmacSha256(messageToSign, _settings.SecretKey);

        // 5. Comparar en tiempo constante (evita timing attacks)
        var receivedBytes = Convert.FromHexString(receivedSignature.ToString());
        var expectedBytes = Convert.FromHexString(expectedSignature);
        var isValid = CryptographicOperations.FixedTimeEquals(receivedBytes, expectedBytes);

        if (!isValid)
        {
            _logger.LogWarning(
                "Solicitud rechazada: firma inválida. ApiKey='{ApiKey}', Ruta='{Path}'.",
                apiKey.ToString(), path);
        }

        return isValid;
    }

    /// <summary>
    /// Calcula HMAC-SHA256 y retorna el resultado como hex en minúsculas.
    /// </summary>
    private static string ComputeHmacSha256(string message, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var messageBytes = Encoding.UTF8.GetBytes(message);
        using var hmac = new HMACSHA256(keyBytes);
        return Convert.ToHexString(hmac.ComputeHash(messageBytes)).ToLowerInvariant();
    }
}
