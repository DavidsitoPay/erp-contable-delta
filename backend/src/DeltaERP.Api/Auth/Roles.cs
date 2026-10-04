namespace DeltaERP.Api.Auth;

public static class Roles
{
    public const string Administrador = "Administrador del sistema";
    public const string Contador = "Contador";
    public const string Vendedor = "Vendedor";
    public const string Tecnico = "Técnico";
    public const string GestionCatalogo = $"{Administrador},{Contador}";

    // E4: Vendedor factura a clientes, por eso puede gestionar CxC.
    public const string GestionCxC = $"{Administrador},{Contador},{Vendedor}";

    // E5: CxP excluye a Vendedor.
    public const string GestionCxP = $"{Administrador},{Contador}";
}
