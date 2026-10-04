using DeltaERP.Domain.Entities;

namespace DeltaERP.Domain.Rules;

public enum LadoControl
{
    Debito,
    Credito,
}

// Único lugar donde CxC (debita la cuenta de control, acredita las líneas) y CxP
// (acredita la cuenta de control, debita las líneas) se diferencian.
public static class AsientoFacturaBuilder
{
    // Redondeo por línea antes de sumar: el total coincide centavo a centavo con
    // la suma de las líneas, evitando falsos "no cuadra" en trg_validar_partida_doble.
    public static decimal MontoLinea(ILineaFactura linea) =>
        Math.Round(linea.Cantidad * linea.PrecioUnitario * (1 + linea.PorcentajeImpuesto / 100m), 2, MidpointRounding.AwayFromZero);

    // RN-01: cuadra por construcción (el lado de control suma lo que el otro lado).
    public static AsientoContable Construir(string prefijo, IDocumentoFactura documento, LadoControl ladoControl, int usuarioId)
    {
        var montosLinea = documento.Lineas.Select(MontoLinea).ToList();
        var montoTotal = montosLinea.Sum();
        var controlEsDebito = ladoControl == LadoControl.Debito;

        var asiento = new AsientoContable
        {
            Numero = $"{prefijo}-{documento.Numero}",
            Fecha = documento.Fecha,
            PeriodoId = documento.PeriodoId,
            Estado = "Confirmado",
            UsuarioId = usuarioId,
            TipoCambioAplicado = documento.TipoCambioAplicado,
            Monto = montoTotal,
        };
        asiento.Lineas.Add(new LineaAsiento
        {
            CuentaId = documento.CuentaControlId,
            Debito = controlEsDebito ? montoTotal : 0,
            Credito = controlEsDebito ? 0 : montoTotal,
        });
        for (var i = 0; i < montosLinea.Count; i++)
        {
            asiento.Lineas.Add(new LineaAsiento
            {
                CuentaId = documento.Lineas[i].CuentaContableId,
                CentroCostoId = documento.Lineas[i].CentroCostoId,
                Debito = controlEsDebito ? 0 : montosLinea[i],
                Credito = controlEsDebito ? montosLinea[i] : 0,
            });
        }
        return asiento;
    }
}
