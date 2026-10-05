using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using DeltaERP.Domain.Entities;

namespace DeltaERP.Api.Models;

public record CuentaBancariaDto(
    int Id,
    string Banco,
    string Numero,
    string Tipo,
    bool Activa,
    int CuentaContableId,
    string CuentaContableCodigo,
    string CuentaContableNombre,
    decimal SaldoApertura,
    DateOnly? FechaApertura,
    decimal Saldo);

public record MovimientoDto(
    int Id,
    int CuentaBancariaId,
    DateOnly Fecha,
    string Tipo,
    decimal Monto,
    string Descripcion,
    string? Referencia,
    string Origen,
    int AsientoId,
    Guid? TransferenciaId,
    int? ReciboPagoId,
    int? PagoProveedorId,
    bool Conciliado);

public record NuevaConciliacion(
    [property: JsonRequired] int CuentaBancariaId,
    [property: JsonRequired] DateOnly Fecha,
    [property: JsonRequired] decimal SaldoExtracto);

public record EditarConciliacion(
    [property: JsonRequired] DateOnly Fecha,
    [property: JsonRequired] decimal SaldoExtracto);

public record MarcarMovimiento(
    [property: JsonRequired] int MovimientoId);

public record ConciliacionDetalleDto(ConciliacionResumen Resumen, List<MovimientoTesoreria> Marcados, List<MovimientoTesoreria> Disponibles);

public class CuentaBancariaRequest
{
    [Required, MaxLength(100)]
    public string? Banco { get; set; }

    [Required, MaxLength(50)]
    public string? Numero { get; set; }

    [Required]
    public string? Tipo { get; set; }

    [JsonRequired]
    public int CuentaContableId { get; set; }

    [JsonRequired]
    public decimal SaldoApertura { get; set; }

    public DateOnly? FechaApertura { get; set; }

    public int? CuentaContrapartidaId { get; set; }
}

public class NuevoMovimientoManual
{
    [JsonRequired]
    public int CuentaBancariaId { get; set; }

    [JsonRequired]
    public DateOnly Fecha { get; set; }

    [Required]
    public string? Tipo { get; set; }

    [JsonRequired]
    public decimal Monto { get; set; }

    [Required, MaxLength(255)]
    public string? Descripcion { get; set; }

    [MaxLength(100)]
    public string? Referencia { get; set; }

    [JsonRequired]
    public int CuentaContrapartidaId { get; set; }
}

public class NuevaTransferencia
{
    [JsonRequired]
    public int CuentaOrigenId { get; set; }

    [JsonRequired]
    public int CuentaDestinoId { get; set; }

    [JsonRequired]
    public DateOnly Fecha { get; set; }

    [JsonRequired]
    public decimal Monto { get; set; }

    [MaxLength(255)]
    public string? Descripcion { get; set; }

    [MaxLength(100)]
    public string? Referencia { get; set; }
}

public record TransferenciaResultado(Guid TransferenciaId, int AsientoId, MovimientoTesoreria Egreso, MovimientoTesoreria Ingreso);
