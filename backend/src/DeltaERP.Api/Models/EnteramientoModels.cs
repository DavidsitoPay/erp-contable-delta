using System.Text.Json.Serialization;
using DeltaERP.Domain.Entities;

namespace DeltaERP.Api.Models;

public sealed record SolicitudEnteramiento(string? Formulario, [property: JsonRequired] DateOnly Periodo, [property: JsonRequired] DateOnly FechaPago, string? Boleta, [property: JsonRequired] decimal Monto, [property: JsonRequired] int CuentaBancariaId);

public sealed record SolicitudVistaPreviaEnteramiento([property: JsonRequired] DateOnly Periodo, [property: JsonRequired] decimal Monto, [property: JsonRequired] int CuentaBancariaId);

public sealed record LineaPartidaDto(int CuentaId, string Codigo, string Nombre, decimal Debe, decimal Haber);

public sealed record VistaPreviaEnteramientoDto(IReadOnlyList<LineaPartidaDto> Lineas, decimal SaldoAntes, decimal SaldoDespues);

public sealed record SaldoEnterarDto(
    DateOnly Periodo, decimal Retenido, decimal Enterado, decimal Saldo, DateOnly FechaVencimiento, int DiasParaVencer,
    string EstadoVencimiento, string? AvisoMulta, bool CalendarioIncompleto);

public sealed record EnteramientoDto(
    int Id, string Concepto, string Formulario, DateOnly Periodo, DateOnly FechaPago, string Boleta, decimal Monto, int CuentaBancariaId,
    int AsientoId, int MovimientoTesoreriaId, string Estado, string? MotivoAnulacion, DateTimeOffset? AnuladoEn, int? AsientoReversaId,
    int RegistradoPor, DateTimeOffset CreadoEn)
{
    public static EnteramientoDto Desde(Enteramiento e) => new(
        e.Id, e.Concepto, e.Formulario, e.Periodo, e.FechaPago, e.Boleta, e.Monto, e.CuentaBancariaId, e.AsientoId, e.MovimientoTesoreriaId,
        e.Estado, e.MotivoAnulacion, e.AnuladoEn, e.AsientoReversaId, e.RegistradoPor, e.CreadoEn);
}

public sealed record AjusteRetencionDto(
    int Id, int RetencionId, int NotaCreditoId, int DocumentoOrigenId, string NotaCreditoNumero, string DocumentoOrigenNumero, string ProveedorNombre,
    DateOnly PeriodoFiscal, decimal MontoNc, string Estado, DateTimeOffset CreadoEn, string? MotivoResolucion, string? ReferenciaResolucion,
    DateTimeOffset? ResueltoEn);

public sealed record SolicitudResolverAjuste(string? Motivo, string? Referencia);

public sealed record FeriadoDto([property: JsonRequired] DateOnly Fecha, string? Nombre, [property: JsonRequired] bool MedioDia)
{
    public static FeriadoDto Desde(Feriado f) => new(f.Fecha, f.Nombre, f.MedioDia);
}
