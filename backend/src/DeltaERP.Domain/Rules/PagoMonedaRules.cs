using DeltaERP.Domain.Entities;

namespace DeltaERP.Domain.Rules;

public sealed record DocumentoMonedaInfo(int DocumentoId, string Numero, int MonedaId, string MonedaCodigo, bool EsFuncional);

public sealed record CuentaMonedaInfo(int MonedaId, string MonedaCodigo, bool EsFuncional);

public sealed record AplicacionEntrada(int DocumentoId, decimal Monto, decimal? ValorEfectivoGtq)
{
    public static List<AplicacionEntrada> Desde(IEnumerable<IAplicacionPago> aplicaciones) =>
        aplicaciones.Select(a => new AplicacionEntrada(a.DocumentoId, a.MontoAplicado, a.ValorEfectivoGtq)).ToList();
}

public static class PagoMonedaRules
{
    public static ErrorRegla? Validar(
        CuentaMonedaInfo cuenta, IReadOnlyCollection<DocumentoMonedaInfo> documentos, IReadOnlyCollection<AplicacionEntrada> aplicaciones)
    {
        var usados = documentos.Where(d => aplicaciones.Any(a => a.DocumentoId == d.DocumentoId)).ToList();
        return MonedasMezcladas(usados) ?? FacturaAplicadaVariasVeces(usados, aplicaciones)
            ?? CompatibilidadConCuenta(cuenta, usados) ?? ValorEfectivo(cuenta, usados, aplicaciones);
    }

    private static ErrorRegla? MonedasMezcladas(List<DocumentoMonedaInfo> usados)
    {
        var codigos = usados.Select(d => d.MonedaCodigo).Distinct().ToList();
        return codigos.Count > 1
            ? new ErrorRegla($"Un cobro o pago aplica facturas de una sola moneda; separe las facturas en {codigos[0]} y en {codigos[1]}.", false)
            : null;
    }

    private static ErrorRegla? FacturaAplicadaVariasVeces(List<DocumentoMonedaInfo> usados, IReadOnlyCollection<AplicacionEntrada> aplicaciones)
    {
        var repetida = usados.FirstOrDefault(d => !d.EsFuncional && aplicaciones.Count(a => a.DocumentoId == d.DocumentoId) > 1);
        return repetida is null
            ? null
            : new ErrorRegla($"La factura {repetida.Numero} en moneda extranjera admite una sola aplicación por cobro o pago.", false);
    }

    private static ErrorRegla? CompatibilidadConCuenta(CuentaMonedaInfo cuenta, List<DocumentoMonedaInfo> usados)
    {
        if (cuenta.EsFuncional)
        {
            return null;
        }
        var enQuetzales = usados.FirstOrDefault(d => d.EsFuncional);
        if (enQuetzales is not null)
        {
            return new ErrorRegla(
                $"RN-27: una factura en quetzales no puede cobrarse ni pagarse con una cuenta en {cuenta.MonedaCodigo} en esta versión.", true);
        }
        var distinta = usados.FirstOrDefault(d => d.MonedaId != cuenta.MonedaId);
        return distinta is null
            ? null
            : new ErrorRegla(
                $"La moneda de la factura {distinta.Numero} ({distinta.MonedaCodigo}) no coincide con la de la cuenta bancaria ({cuenta.MonedaCodigo}).", true);
    }

    private static ErrorRegla? ValorEfectivo(
        CuentaMonedaInfo cuenta, List<DocumentoMonedaInfo> usados, IReadOnlyCollection<AplicacionEntrada> aplicaciones)
    {
        foreach (var aplicacion in aplicaciones)
        {
            var documento = usados.First(d => d.DocumentoId == aplicacion.DocumentoId);
            var requiere = !documento.EsFuncional && cuenta.EsFuncional;
            if (requiere && !ValorValido(aplicacion.ValorEfectivoGtq))
            {
                return new ErrorRegla($"Indique el efectivo en quetzales (valorEfectivoGtq) de la factura {documento.Numero}.", false);
            }
            if (!requiere && aplicacion.ValorEfectivoGtq is not null)
            {
                return new ErrorRegla(
                    "valorEfectivoGtq solo aplica a facturas en moneda extranjera cobradas o pagadas desde una cuenta en quetzales.", false);
            }
        }
        return null;
    }

    private static bool ValorValido(decimal? valor) => valor is > 0 && Math.Round(valor.Value, 2) == valor.Value;
}
