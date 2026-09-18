namespace DeltaERP.Api.Auth;

public static class Roles
{
    public const string Administrador = "Administrador del sistema";
    public const string Contador = "Contador";
    public const string Vendedor = "Vendedor";
    public const string Tecnico = "Técnico";
    public const string GestionCatalogo = $"{Administrador},{Contador}";
}
