namespace Gateway.Services;

/// <summary>
/// Contrato del servicio de validación de firma HMAC-SHA256.
/// </summary>
public interface ISignatureValidationService
{
    /// <summary>
    /// Valida que la firma enviada en los headers del request sea correcta.
    /// </summary>
    /// <param name="request">La solicitud HTTP entrante.</param>
    /// <param name="body">El cuerpo leído previamente como string.</param>
    /// <returns>True si la firma es válida; false en caso contrario.</returns>
    bool ValidateSignature(HttpRequest request, string body);
}
