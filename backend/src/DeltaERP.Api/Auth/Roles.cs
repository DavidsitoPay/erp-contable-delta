namespace DeltaERP.Api.Auth;

public static class Roles
{
    public const string Administrador = "Administrador del sistema";
    public const string Contador = "Contador";
    public const string Vendedor = "Vendedor";
    public const string Tecnico = "Técnico";
    public const string GestionCatalogo = $"{Administrador},{Contador}";

    // E4: "Como Vendedor, quiero registrar facturas a clientes" (backlog-features-historias.md).
    public const string GestionCxC = $"{Administrador},{Contador},{Vendedor}";

    // E5: ambas historias de CxP están redactadas "Como Contador" — sin Vendedor.
    public const string GestionCxP = $"{Administrador},{Contador}";
}
