namespace DeltaERP.Domain.Entities;

/// <summary>
/// Centro de costo (módulo M1), usado para distribuir líneas de asiento
/// (LineaAsiento.CentroCostoId).
/// </summary>
public class CentroCosto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}
