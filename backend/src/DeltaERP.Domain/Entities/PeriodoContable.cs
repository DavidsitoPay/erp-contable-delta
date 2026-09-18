namespace DeltaERP.Domain.Entities;

/// <summary>
/// Periodo contable (módulo M1). El cierre (RN-02) lo ejecuta
/// sp_cerrar_periodo, que además consolida SaldoCuentaPeriodo de forma
/// inmutable — no se modela aquí, se invoca vía PeriodosController.
/// </summary>
public class PeriodoContable
{
    public const string EstadoAbierto = "Abierto";
    public const string EstadoCerrado = "Cerrado";
    public static readonly string[] EstadosValidos = { EstadoAbierto, EstadoCerrado };

    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public string Estado { get; set; } = "Abierto";
}
