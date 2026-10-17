namespace DeltaERP.Api.Models;

public sealed record FilaSaldoAntiguedad(
    int DocumentoId, string Numero, int TerceroId, string Tercero, DateOnly Fecha, DateOnly FechaVencimiento, string Moneda,
    decimal SaldoOriginal, decimal TipoCambioLibros, decimal SaldoGtq, int DiasVencido, string Tramo);

public sealed record TotalSaldoMoneda(string Moneda, decimal SaldoOriginal, decimal SaldoGtq, IReadOnlyDictionary<string, decimal> PorTramo);

public sealed record ConciliacionSaldo(
    int CuentaId, string CuentaCodigo, decimal TotalReporte, decimal SaldoMayor, decimal RevaluacionVigente, decimal Diferencia);

public sealed record ReporteSaldosRespuesta(
    string Tipo, DateOnly Corte, IReadOnlyList<FilaSaldoAntiguedad> Filas, IReadOnlyList<TotalSaldoMoneda> Totales, decimal TotalGtq,
    IReadOnlyList<ConciliacionSaldo> Conciliacion, string Leyenda);
