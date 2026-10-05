namespace DeltaERP.Api.Auth;

public static class Roles
{
    public const string Administrador = "Administrador del sistema";
    public const string Contador = "Contador";
    public const string Vendedor = "Vendedor";
    public const string Tecnico = "Técnico";
    public const string GestionCatalogo = $"{Administrador},{Contador}";

    // E4: Vendedor factura a clientes (no registra pagos: ver RegistroPagos).
    public const string GestionCxC = $"{Administrador},{Contador},{Vendedor}";

    // E5: CxP excluye a Vendedor.
    public const string GestionCxP = $"{Administrador},{Contador}";

    public const string GestionTesoreria = $"{Administrador},{Contador}";
    public const string RegistroPagos = $"{Administrador},{Contador}";
}
