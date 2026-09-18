namespace DeltaERP.Api.Auth;

/// <summary>
/// Enlazado a la sección "Jwt" de appsettings.json. La clave de desarrollo está
/// marcada como local-only (mismo criterio que la contraseña de la BD en
/// devops/docker-compose.yml); en un entorno real debe venir de un secreto
/// gestionado (Azure Key Vault, variable de entorno de la pipeline), no del repo.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 60;
}
