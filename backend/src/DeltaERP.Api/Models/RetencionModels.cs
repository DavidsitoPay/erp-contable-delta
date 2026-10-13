using System.Text.Json.Serialization;
using DeltaERP.Domain.Entities;

namespace DeltaERP.Api.Models;

public sealed record SolicitudRetencionPrevia(
    [property: JsonRequired] int ProveedorId,
    [property: JsonRequired] DateOnly Fecha,
    [property: JsonRequired] decimal MontoBase,
    [property: JsonRequired] decimal MontoTotal);

public sealed record SolicitudMotivo(string? Motivo);

public sealed record SolicitudConstancia(
    [property: JsonRequired] string Numero,
    [property: JsonRequired] DateOnly Fecha,
    string? Formulario,
    DateOnly? FechaPagoFormulario,
    DateOnly? PeriodoDeclarado);

public sealed record ConstanciaRespuesta(
    int Id,
    int RetencionId,
    string Tipo,
    int? RetenedorContraparteId,
    string Numero,
    DateOnly Fecha,
    string? Formulario,
    DateOnly? FechaPagoFormulario,
    DateOnly? PeriodoDeclarado,
    string Estado,
    string? MotivoAnulacion,
    IReadOnlyList<string> Advertencias)
{
    public static ConstanciaRespuesta Desde(ConstanciaRetencion c, IReadOnlyList<string> advertencias) => new(
        c.Id, c.RetencionId, c.Tipo, c.RetenedorContraparteId, c.Numero, c.Fecha, c.Formulario, c.FechaPagoFormulario,
        c.PeriodoDeclarado, c.Estado, c.MotivoAnulacion, advertencias);
}

public sealed record RetencionIsrRespuesta(
    string Estado,
    string? Motivo,
    decimal Base,
    decimal Tasa,
    decimal Monto,
    decimal Neto,
    int? RetencionId)
{
    public static RetencionIsrRespuesta Desde(Retencion r, decimal montoTotal) => new(
        "Retenida", r.MotivoAjuste, r.Base, r.Tasa, r.MontoRetenido, montoTotal - r.MontoRetenido, r.Id);
}

public sealed record VistaPreviaIsr(
    string Estado,
    string? Motivo,
    decimal Base,
    decimal? TasaBase,
    decimal? TramoLimite,
    decimal? TasaExcedente,
    decimal? UmbralMinimo,
    decimal Retencion,
    decimal Neto,
    int? ParametroId,
    bool Referencial,
    IReadOnlyList<string> Advertencias);

public sealed record SugerenciaIvaRespuesta(
    bool Aplica,
    string? Motivo,
    bool ClienteAgente,
    string? TipoAgente,
    decimal? Tasa,
    decimal IvaFactura,
    decimal IvaNotasCredito,
    decimal IvaRetenible,
    decimal? UmbralMinimo,
    decimal TotalFactura,
    decimal Sugerido,
    bool YaRetenida,
    int? ParametroId);

public sealed record RetencionCobroRespuesta(int RetencionId, int DocumentoId, decimal Monto, int? ConstanciaId);

public sealed record AplicacionCobroRespuesta(int Id, int ReciboPagoId, int DocumentoId, decimal MontoAplicado);

public sealed record ReciboCobroRespuesta(
    int Id,
    int ClienteId,
    DateOnly Fecha,
    decimal MontoTotal,
    decimal MontoNeto,
    string MetodoPago,
    string? ReferenciaBancaria,
    int? CuentaBancariaId,
    IReadOnlyList<AplicacionCobroRespuesta> Aplicaciones,
    IReadOnlyList<RetencionCobroRespuesta> Retenciones,
    IReadOnlyList<string> Advertencias);

public sealed record ParametroRetencionRespuesta(
    int Id,
    string Codigo,
    string Concepto,
    string Nombre,
    decimal? TasaBase,
    decimal? TramoLimite,
    decimal? TasaExcedente,
    decimal UmbralMinimo,
    string UmbralSobre,
    short? DiaLimite,
    string ArticuloLegal,
    string Nota,
    DateOnly VigenteDesde,
    DateOnly? VigenteHasta,
    bool Activo,
    bool EnUso)
{
    public static ParametroRetencionRespuesta Desde(ParametroRetencion p, bool enUso) => new(
        p.Id, p.Codigo, p.Concepto, p.Nombre, p.TasaBase, p.TramoLimite, p.TasaExcedente, p.UmbralMinimo, p.UmbralSobre,
        p.DiaLimite, p.ArticuloLegal, p.Nota, p.VigenteDesde, p.VigenteHasta, p.Activo, enUso);
}

public sealed record FiltroReporteRetencion(int Anio, int Mes, string? Sentido, string? Concepto, string? Nit, string? EstadoConstancia);

public sealed record FilaReporteRetencion(
    int RetencionId,
    string Sentido,
    string Concepto,
    int ContraparteId,
    string? Nit,
    string Nombre,
    string DocumentoTipo,
    string DocumentoNumero,
    string? DteSerie,
    string? DteNumero,
    Guid? DteUuid,
    DateOnly FechaDocumento,
    DateOnly FechaOperacion,
    decimal Base,
    decimal Tasa,
    decimal TasaEfectiva,
    decimal Monto,
    int? ConstanciaId,
    string? ConstanciaNumero,
    DateOnly? ConstanciaFecha,
    string? Formulario,
    string EstadoConstancia,
    bool PeriodoContableDistinto);

public sealed record TotalReporteRetencion(string Sentido, string Concepto, int Cantidad, decimal Base, decimal Monto, int SinConstancia);

public sealed record ConciliacionReporteRetencion(
    string Concepto,
    int CuentaId,
    string CuentaCodigo,
    decimal TotalReporte,
    decimal MovimientoContable,
    decimal Diferencia);

public sealed record ReporteRetencionRespuesta(
    int Anio,
    int Mes,
    IReadOnlyList<FilaReporteRetencion> Filas,
    IReadOnlyList<TotalReporteRetencion> Totales,
    IReadOnlyList<ConciliacionReporteRetencion> Conciliacion);
