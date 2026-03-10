namespace Gateway.Configuration;

/// <summary>
/// Configuración tipada del API Gateway.
/// Se mapea desde la sección "GatewaySettings" en appsettings.json.
/// </summary>
public class GatewaySettings
{
    public const string SectionName = "GatewaySettings";

    /// <summary>
    /// Clave secreta compartida para generar y validar firmas HMAC-SHA256.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Diferencia máxima permitida (en segundos) entre el timestamp de la
    /// solicitud y el tiempo actual. Previene ataques de replay.
    /// </summary>
    public int AllowedTimestampDiffSeconds { get; set; } = 300;

    /// <summary>
    /// Prefijos de ruta exentos de la validación de firma (ej: /health, /api/auth/login).
    /// </summary>
    public List<string> SkipSignatureForPaths { get; set; } = new();
}
