namespace DeltaERP.Domain.Entities;

/// <summary>
/// Catálogo compartido de clientes y proveedores (M4/M5), discriminado por
/// Tipo. Sin columna "activa": a diferencia de CuentaContable/CentroCosto, el
/// modelo de datos entregado no previó desactivación para esta tabla, así que
/// una contraparte una vez creada solo admite editar sus datos de contacto.
/// </summary>
public class Contraparte
{
    public static readonly string[] TiposValidos = { "Cliente", "Proveedor" };

    public int Id { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Nit { get; set; }
    public string? Direccion { get; set; }
}
