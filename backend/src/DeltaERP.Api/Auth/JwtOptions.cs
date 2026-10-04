namespace DeltaERP.Api.Auth;

// Key es solo para desarrollo local; en producción debe venir de un secreto
// gestionado, no del repo.
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 60;
}
