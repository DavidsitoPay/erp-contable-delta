using DeltaERP.Api.Auth;
using DeltaERP.Api.Models;
using DeltaERP.Api.Services;
using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;
using DeltaERP.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeltaERP.Api.Controllers;

[ApiController]
[Route("api/configuracion-fiscal")]
[Authorize]
public class ConfiguracionFiscalController : ControllerBase
{
    private readonly DeltaErpDbContext _db;
    private readonly ConfiguracionFiscalService _configuracion;
    private readonly ValidacionContable _validacion;
    private readonly AuditoriaService _auditoria;

    public ConfiguracionFiscalController(
        DeltaErpDbContext db, ConfiguracionFiscalService configuracion, ValidacionContable validacion, AuditoriaService auditoria)
    {
        _db = db;
        _configuracion = configuracion;
        _validacion = validacion;
        _auditoria = auditoria;
    }

    [HttpGet]
    [Authorize(Roles = Roles.GestionCatalogo)]
    public async Task<IActionResult> Obtener() => Ok(await ConstruirRespuestaAsync());

    [HttpPut]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<IActionResult> Actualizar([FromBody] DatosConfiguracionFiscal recibidos)
    {
        var configuracion = await _configuracion.ObtenerAsync();
        var datos = recibidos with
        {
            CuentaIsrRetenidoPagarId = recibidos.CuentaIsrRetenidoPagarId ?? configuracion.CuentaIsrRetenidoPagarId,
            CuentaIvaRetenidoClientesId = recibidos.CuentaIvaRetenidoClientesId ?? configuracion.CuentaIvaRetenidoClientesId,
        };
        var error = ConfiguracionFiscalRules.Validar(datos) ?? await ValidarCuentasAsync(datos);
        if (error is not null)
        {
            return BadRequest(new { error });
        }

        var usuarioId = this.UsuarioId();
        var anterior = Describir(configuracion);
        configuracion.NitEmpresa = NitRules.Normalizar(datos.NitEmpresa);
        configuracion.NombreLegal = datos.NombreLegal;
        configuracion.RegimenIsr = datos.RegimenIsr!;
        configuracion.AgenteRetencionIva = datos.AgenteRetencionIva;
        configuracion.TipoAgenteIva = ConfiguracionFiscalRules.TipoAgenteNormalizado(datos);
        configuracion.CuentaIvaDebitoId = datos.CuentaIvaDebitoId;
        configuracion.CuentaIvaCreditoId = datos.CuentaIvaCreditoId;
        configuracion.CuentaIsrRetenidoPagarId = datos.CuentaIsrRetenidoPagarId;
        configuracion.CuentaIvaRetenidoClientesId = datos.CuentaIvaRetenidoClientesId;
        configuracion.ActualizadoEn = DateTimeOffset.UtcNow;
        configuracion.ActualizadoPor = usuarioId;

        await _auditoria.EjecutarAsync(usuarioId, "actualizar_configuracion_fiscal", "configuracionfiscal", async () =>
        {
            await _db.SaveChangesAsync();
            return $"Configuración fiscal actualizada. Antes: {anterior}. Después: {Describir(configuracion)}";
        });
        return Ok(await ConstruirRespuestaAsync());
    }

    private static string Describir(ConfiguracionFiscal c) =>
        $"régimen ISR {c.RegimenIsr}, agente IVA {c.AgenteRetencionIva} ({c.TipoAgenteIva}), cuenta débito {c.CuentaIvaDebitoId}, cuenta crédito {c.CuentaIvaCreditoId}, cuenta ISR retenido por pagar {c.CuentaIsrRetenidoPagarId}, cuenta IVA retenido por clientes {c.CuentaIvaRetenidoClientesId}";

    private async Task<string?> ValidarCuentasAsync(DatosConfiguracionFiscal datos)
    {
        var requisitos = new (int? CuentaId, string Tipo, string Naturaleza, string Mensaje)[]
        {
            (datos.CuentaIvaDebitoId, "Pasivo", "Acreedora", "La cuenta de IVA débito fiscal debe ser de tipo Pasivo y naturaleza Acreedora."),
            (datos.CuentaIvaCreditoId, "Activo", "Deudora", "La cuenta de IVA crédito fiscal debe ser de tipo Activo y naturaleza Deudora."),
            (datos.CuentaIsrRetenidoPagarId, "Pasivo", "Acreedora", "La cuenta de ISR retenido por pagar debe ser de tipo Pasivo y naturaleza Acreedora."),
            (datos.CuentaIvaRetenidoClientesId, "Activo", "Deudora", "La cuenta de IVA retenido por clientes debe ser de tipo Activo y naturaleza Deudora."),
        };
        foreach (var (cuentaId, tipo, naturaleza, mensaje) in requisitos)
        {
            var error = await ValidarCuentaAsync(cuentaId, tipo, naturaleza, mensaje);
            if (error is not null)
            {
                return error;
            }
        }
        return null;
    }

    private async Task<string?> ValidarCuentaAsync(int? cuentaId, string tipo, string naturaleza, string mensajeTipo)
    {
        if (cuentaId is null)
        {
            return null;
        }
        var error = await _validacion.ValidarCuentasPorIdAsync([cuentaId.Value]);
        if (error is not null)
        {
            return error;
        }
        var cuenta = await _db.CuentasContables.FindAsync(cuentaId.Value);
        return cuenta is not null && cuenta.Tipo == tipo && cuenta.Naturaleza == naturaleza ? null : mensajeTipo;
    }

    private async Task<ConfiguracionFiscalRespuesta> ConstruirRespuestaAsync()
    {
        var c = await _configuracion.ObtenerAsync();
        var moneda = await _db.Monedas.FindAsync(c.MonedaFuncionalId);
        var usuario = c.ActualizadoPor is { } usuarioId ? await _db.Usuarios.FindAsync(usuarioId) : null;
        return new ConfiguracionFiscalRespuesta(
            c.NitEmpresa, c.NombreLegal, c.MonedaFuncionalId, moneda?.Codigo, c.RegimenIsr, c.AgenteRetencionIva,
            c.TipoAgenteIva, c.CuentaIvaDebitoId, c.CuentaIvaCreditoId, c.CuentaIsrRetenidoPagarId, c.CuentaIvaRetenidoClientesId,
            c.ActualizadoEn, usuario?.Nombre);
    }
}
