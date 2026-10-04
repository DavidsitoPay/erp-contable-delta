namespace DeltaERP.Domain.Entities;

// Sin columna "activa" (a diferencia de CuentaContable/CentroCosto): una
// contraparte solo admite editar sus datos de contacto.
public class Contraparte
{
    public static readonly string[] TiposValidos = { "Cliente", "Proveedor" };

    public int Id { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Nit { get; set; }
    public string? Direccion { get; set; }
}
