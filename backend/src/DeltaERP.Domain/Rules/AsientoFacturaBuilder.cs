using DeltaERP.Domain.Entities;

namespace DeltaERP.Domain.Rules;

public enum LadoControl
{
    Debito,
    Credito,
}

public static class AsientoFacturaBuilder
{
    // RN-01: cuadra por construcción (Base + IVA = MontoLinea en cada línea).
    public static AsientoContable Construir(
        string prefijo, IDocumentoFactura documento, ResumenFiscal resumen, LadoControl ladoControl, int usuarioId, int? cuentaIvaId)
    {
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
        asiento.Lineas.Add(NuevaLinea(documento.CuentaControlId, null, resumen.Total, controlEsDebito));
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
