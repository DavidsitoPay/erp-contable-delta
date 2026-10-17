namespace DeltaERP.Domain.Entities;

public class Revaluacion
{
    public const string EstadoVigente = "Vigente";
    public const string EstadoAnulada = "Anulada";

    public int Id { get; set; }
    public int MonedaId { get; set; }
    public short Anio { get; set; }
    public short Mes { get; set; }
    public DateOnly FechaCierre { get; set; }
    public int TipoCambioId { get; set; }
    public decimal Tasa { get; set; }
    public decimal TotalGanancia { get; set; }
    public decimal TotalPerdida { get; set; }
    public string Estado { get; set; } = EstadoVigente;
    public int AsientoId { get; set; }
    public int AsientoReversaId { get; set; }
    public string? MotivoAnulacion { get; set; }
    public int? AnuladaPor { get; set; }
    public DateTimeOffset? AnuladaEn { get; set; }
    public int? AsientoAnulacionId { get; set; }
    public int? AsientoAnulacionReversaId { get; set; }
    public int CreadoPor { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}

public class RevaluacionPartida
{
    public const string TipoCxC = "CxC";
    public const string TipoCxP = "CxP";
    public const string TipoBanco = "Banco";

    public int Id { get; set; }
    public int RevaluacionId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public int? DocumentoCxcId { get; set; }
    public int? DocumentoCxpId { get; set; }
    public int? CuentaBancariaId { get; set; }
    public int CuentaId { get; set; }
    public decimal SaldoOriginal { get; set; }
    public decimal ValorLibrosGtq { get; set; }
    public decimal ValorCierreGtq { get; set; }
    public decimal AjusteGtq { get; set; }
}
