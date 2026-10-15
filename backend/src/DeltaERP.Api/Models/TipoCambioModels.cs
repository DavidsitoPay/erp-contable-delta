using System.Text.Json.Serialization;
using DeltaERP.Api.Services;

namespace DeltaERP.Api.Models;

public sealed record TipoCambioDto(
    int Id, string MonedaCodigo, DateOnly Fecha, decimal Tasa, string Fuente, bool Arrastrado, DateOnly FechaReferencia,
    decimal? CotizacionVenta, decimal? CotizacionCompra, decimal? ReferenciaUsd, string? Motivo, string? UsuarioNombre,
    bool Usada, bool Activa);

public sealed record ConsultaTipoCambioRespuesta(
    string Moneda, DateOnly Fecha, bool Disponible, TipoCambioDto? TipoCambio, PropuestaTipoCambio? Propuesta, string? Mensaje);

public sealed record SolicitudTasaManual(
    [property: JsonRequired] string MonedaCodigo,
    [property: JsonRequired] DateOnly Fecha,
    [property: JsonRequired] decimal Tasa,
    string? Motivo);

public sealed record SolicitudSustitucion(
    [property: JsonRequired] string MonedaCodigo,
    [property: JsonRequired] DateOnly Fecha);
