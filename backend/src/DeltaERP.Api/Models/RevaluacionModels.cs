using System.Text.Json.Serialization;

namespace DeltaERP.Api.Models;

public sealed record SolicitudRevaluacion(
    [property: JsonRequired] int Anio,
    [property: JsonRequired] int Mes,
    string? MonedaCodigo);

public sealed record TipoCambioResumen(int Id, decimal Tasa, string Fuente, bool Arrastrado);

public sealed record PartidaRevaluacionDto(
    string Tipo, string Referencia, int CuentaId, decimal SaldoOriginal, decimal ValorLibrosGtq, decimal ValorCierreGtq,
    decimal AjusteGtq, string Efecto);

public sealed record RevaluacionVistaPrevia(
    string MonedaCodigo, int Anio, int Mes, DateOnly FechaCierre, TipoCambioResumen TipoCambio,
    IReadOnlyList<PartidaRevaluacionDto> Partidas, decimal TotalGanancia, decimal TotalPerdida, string Leyenda);

public sealed record RevaluacionRespuesta(
    int Id, string MonedaCodigo, int Anio, int Mes, DateOnly FechaCierre, TipoCambioResumen TipoCambio,
    IReadOnlyList<PartidaRevaluacionDto> Partidas, decimal TotalGanancia, decimal TotalPerdida, int AsientoId, int AsientoReversaId,
    string Estado, string? MotivoAnulacion, string Leyenda);

public sealed record RevaluacionResumenDto(
    int Id, string MonedaCodigo, int Anio, int Mes, DateOnly FechaCierre, decimal Tasa, decimal TotalGanancia, decimal TotalPerdida,
    string Estado, int AsientoId, int AsientoReversaId);
