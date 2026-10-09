using DeltaERP.Domain.Entities;

namespace DeltaERP.Api.Models;

public sealed record ConfiguracionFiscalRespuesta(
    string? NitEmpresa,
    string? NombreLegal,
    int MonedaFuncionalId,
    string? MonedaFuncionalCodigo,
    string RegimenIsr,
    bool AgenteRetencionIva,
    string? TipoAgenteIva,
    int? CuentaIvaDebitoId,
    int? CuentaIvaCreditoId,
    DateTimeOffset ActualizadoEn,
    string? ActualizadoPorNombre);

public sealed record ImpuestoRespuesta(
    int Id,
    string Codigo,
    string Nombre,
    string Tipo,
    decimal Tasa,
    string AplicaA,
    bool GeneraCredito,
    string ArticuloLegal,
    string? Nota,
    DateOnly VigenteDesde,
    DateOnly? VigenteHasta,
    bool Activo,
    bool EnUso)
{
    public static ImpuestoRespuesta Desde(Impuesto i, bool enUso) => new(
        i.Id, i.Codigo, i.Nombre, i.Tipo, i.Tasa, i.AplicaA, i.GeneraCredito, i.ArticuloLegal,
        i.Nota, i.VigenteDesde, i.VigenteHasta, i.Activo, enUso);
}
