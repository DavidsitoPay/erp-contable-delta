using DeltaERP.Domain.Entities;

namespace DeltaERP.Domain.Rules;

public enum LadoControl
{
    Debito,
    Credito,
}

public sealed record RetencionAsiento(int CuentaId, decimal Monto);

public static class AsientoFacturaBuilder
{
    // RN-01: cuadra por construcción (Base + IVA = MontoLinea en cada línea; control + retención = Total).
    public static AsientoContable Construir(
        string prefijo, IDocumentoFactura documento, ResumenFiscal resumen, LadoControl ladoControl, int usuarioId, int? cuentaIvaId,
        RetencionAsiento? retencion = null)
    {
        if (retencion is not null && (ladoControl != LadoControl.Credito || documento.TipoDocumento != TiposDocumento.Factura))
        {
            throw new InvalidOperationException("La retención solo aplica a facturas de compra.");
        }
        var esNotaCredito = documento.TipoDocumento == TiposDocumento.NotaCredito;
        var controlEsDebito = (ladoControl == LadoControl.Debito) != esNotaCredito;
        var ivaSeparado = (ResultadoLinea r) => ladoControl == LadoControl.Debito || r.IvaAcreditable;

        var asiento = new AsientoContable
        {
            Numero = $"{prefijo}-{documento.Numero}",
            Fecha = documento.Fecha,
            PeriodoId = documento.PeriodoId,
            Estado = "Confirmado",
            UsuarioId = usuarioId,
            TipoCambioAplicado = documento.TipoCambioAplicado,
            Monto = resumen.Total,
        };
        asiento.Lineas.Add(NuevaLinea(documento.CuentaControlId, null, resumen.Total - (retencion?.Monto ?? 0m), controlEsDebito));
        for (var i = 0; i < resumen.Lineas.Count; i++)
        {
            var calculo = resumen.Lineas[i];
            var monto = ivaSeparado(calculo) ? calculo.Base : calculo.Base + calculo.Iva;
            asiento.Lineas.Add(NuevaLinea(documento.Lineas[i].CuentaContableId, documento.Lineas[i].CentroCostoId, monto, !controlEsDebito));
        }

        var iva = resumen.Lineas.Where(ivaSeparado).Sum(r => r.Iva);
        if (iva > 0)
        {
            var cuentaIva = cuentaIvaId ?? throw new InvalidOperationException("Falta la cuenta de IVA para asentar la factura.");
            asiento.Lineas.Add(NuevaLinea(cuentaIva, null, iva, !controlEsDebito));
        }
        if (retencion is not null)
        {
            asiento.Lineas.Add(NuevaLinea(retencion.CuentaId, null, retencion.Monto, false));
        }
        return asiento;
    }

    private static LineaAsiento NuevaLinea(int cuentaId, int? centroCostoId, decimal monto, bool esDebito) => new()
    {
        CuentaId = cuentaId,
        CentroCostoId = centroCostoId,
        Debito = esDebito ? monto : 0,
        Credito = esDebito ? 0 : monto,
    };
}
