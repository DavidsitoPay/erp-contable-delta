namespace DeltaERP.Domain.Entities;

/// <summary>
/// Registro contable de partida doble (módulo M2). Ver docs/data-dictionary.md.
/// </summary>
public class AsientoContable
{
    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public int PeriodoId { get; set; }
    public decimal Monto { get; set; }
    public string Estado { get; set; } = "Borrador"; // Borrador | Confirmado | Anulado
    public int UsuarioId { get; set; }
    public decimal? TipoCambioAplicado { get; set; }

    public List<LineaAsiento> Lineas { get; set; } = new();
}
