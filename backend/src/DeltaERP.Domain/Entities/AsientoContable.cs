namespace DeltaERP.Domain.Entities;

public class AsientoContable
{
    public const string EstadoBorrador = "Borrador";
    public const string EstadoConfirmado = "Confirmado";
    public const string EstadoAnulado = "Anulado";
    public static readonly string[] EstadosContabilizados = { EstadoConfirmado, EstadoAnulado };

    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public int PeriodoId { get; set; }
    public decimal Monto { get; set; }
    public string Estado { get; set; } = "Borrador"; // Borrador | Confirmado | Anulado
    public int UsuarioId { get; set; }
    public decimal? TipoCambioAplicado { get; set; }
    public int? ReversaDeId { get; set; }

    public List<LineaAsiento> Lineas { get; set; } = new();
}
