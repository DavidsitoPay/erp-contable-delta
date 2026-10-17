namespace DeltaERP.Api.Models;

public sealed record DiferencialRealizadoDto(decimal Ganancia, decimal Perdida);

public sealed record AplicacionPagoRespuesta(
    int Id, int PagoCabeceraId, int DocumentoId, decimal MontoAplicado,
    decimal MontoAplicadoGtq, decimal ValorGtq, decimal DiferencialGtq, decimal? TipoCambioPago);

public sealed record PagoProveedorRespuesta(
    int Id,
    int ProveedorId,
    DateOnly Fecha,
    decimal MontoTotal,
    string MetodoPago,
    string? ReferenciaBancaria,
    int? CuentaBancariaId,
    IReadOnlyList<AplicacionPagoRespuesta> Aplicaciones,
    string Moneda,
    decimal MontoTotalGtq,
    decimal MontoCuenta,
    DiferencialRealizadoDto DiferencialRealizado,
    IReadOnlyList<RetencionPagoRespuesta> Retenciones);
