using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;
using DeltaERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Api.Services;

public class ValidacionContable
{
    private readonly DeltaErpDbContext _db;

    public ValidacionContable(DeltaErpDbContext db)
    {
        _db = db;
    }

    public async Task<Dictionary<int, CuentaContable>> ObtenerCuentasAsync(IEnumerable<ILineaFactura> lineas)
    {
        var idsCuenta = lineas.Select(l => l.CuentaContableId).Distinct().ToList();
        return await _db.CuentasContables.Where(c => idsCuenta.Contains(c.Id)).ToDictionaryAsync(c => c.Id);
    }

    // Devuelve el tercero validado para el detalle de auditoría, o el primer error.
    public async Task<(Contraparte? Tercero, string? Error)> ValidarFacturaAsync(IDocumentoFactura documento, int terceroId, PerfilFactura perfil)
    {
        var error = FacturaRules.ValidarCabecera(perfil.TiposDocumentoValidos, documento);
        if (error is not null)
        {
            return (null, error);
        }

        var (tercero, errorTercero) = await ValidarTerceroAsync(terceroId, perfil.TipoTercero);
        if (errorTercero is not null)
        {
            return (null, errorTercero);
        }

        error = await ValidarPeriodoAbiertoAsync(documento.PeriodoId)
            ?? await ValidarCuentasAsync(documento)
            ?? await ValidarCuentaControlAsync(documento.CuentaControlId, perfil)
            ?? await ValidarCentrosCostoAsync(documento);
        return (tercero, error);
    }

    // RN-04: tipo es "Cliente" o "Proveedor".
    public async Task<(Contraparte? Tercero, string? Error)> ValidarTerceroAsync(int terceroId, string tipo)
    {
        var tercero = await _db.Contrapartes.FindAsync(terceroId);
        if (tercero is null || tercero.Tipo != tipo)
        {
            return (null, $"El {tipo.ToLowerInvariant()} indicado no existe en el catálogo de contrapartes.");
        }
        return (tercero, null);
    }

    private async Task<string?> ValidarPeriodoAbiertoAsync(int periodoId)
    {
        var periodo = await _db.PeriodosContables.FindAsync(periodoId);
        if (periodo is null)
        {
            return "El periodo indicado no existe.";
        }
        if (periodo.Estado != PeriodoContable.EstadoAbierto)
        {
            return $"El periodo '{periodo.Nombre}' está en estado '{periodo.Estado}'; no se pueden registrar facturas en un periodo que no esté Abierto.";
        }
        return null;
    }

    // Valida cuenta de control y cuentas de línea juntas: ambas terminan en
    // LineaAsiento del asiento generado.
    private async Task<string?> ValidarCuentasAsync(IDocumentoFactura documento)
    {
        var idsCuenta = documento.Lineas.Select(l => l.CuentaContableId).Append(documento.CuentaControlId).Distinct().ToList();
        var cuentasPorId = await _db.CuentasContables.Where(c => idsCuenta.Contains(c.Id)).ToDictionaryAsync(c => c.Id);
        var idsConSubcuentas = (await _db.CuentasContables
            .Where(c => c.CuentaPadreId != null && idsCuenta.Contains(c.CuentaPadreId.Value))
            .Select(c => c.CuentaPadreId!.Value)
            .Distinct()
            .ToListAsync())
            .ToHashSet();

        var inexistentes = new List<string>();
        var inactivas = new List<string>();
        var deMayor = new List<string>();
        foreach (var cuentaId in idsCuenta)
        {
            if (!cuentasPorId.TryGetValue(cuentaId, out var cuenta))
            {
                inexistentes.Add(cuentaId.ToString());
                continue;
            }
            if (!cuenta.Activa)
            {
                inactivas.Add(cuenta.Codigo);
            }
            if (idsConSubcuentas.Contains(cuentaId))
            {
                deMayor.Add(cuenta.Codigo);
            }
        }
        if (inexistentes.Count > 0)
        {
            return $"Las siguientes cuentas no existen: {string.Join(", ", inexistentes)}.";
        }
        if (inactivas.Count > 0)
        {
            return $"Las siguientes cuentas están inactivas: {string.Join(", ", inactivas)}.";
        }
        if (deMayor.Count > 0)
        {
            return $"Las siguientes cuentas son de mayor (tienen subcuentas) y no pueden recibir movimientos directos: {string.Join(", ", deMayor)}.";
        }
        return null;
    }

    private async Task<string?> ValidarCuentaControlAsync(int cuentaControlId, PerfilFactura perfil)
    {
        var cuentaControl = await _db.CuentasContables.FindAsync(cuentaControlId);
        if (cuentaControl!.Tipo != perfil.TipoCuentaControl || cuentaControl.Naturaleza != perfil.NaturalezaCuentaControl)
        {
            return $"La cuenta de control de {perfil.Sigla} debe ser de tipo {perfil.TipoCuentaControl} y naturaleza {perfil.NaturalezaCuentaControl} (ej. \"{perfil.EjemploCuentaControl}\").";
        }
        return null;
    }

    private async Task<string?> ValidarCentrosCostoAsync(IDocumentoFactura documento)
    {
        var idsCentro = documento.Lineas.Where(l => l.CentroCostoId is not null).Select(l => l.CentroCostoId!.Value).Distinct().ToList();
        if (idsCentro.Count == 0)
        {
            return null;
        }
        var existentes = (await _db.CentrosCosto.Where(c => idsCentro.Contains(c.Id)).Select(c => c.Id).ToListAsync()).ToHashSet();
        var inexistentes = idsCentro.Where(id => !existentes.Contains(id)).ToList();
        if (inexistentes.Count > 0)
        {
            return $"Los siguientes centros de costo no existen: {string.Join(", ", inexistentes)}.";
        }
        return null;
    }
}
