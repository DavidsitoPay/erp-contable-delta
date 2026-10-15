namespace DeltaERP.Domain.Rules;

public enum TipoPartida
{
    CxC,
    CxP,
    Banco,
}

public sealed record OrigenPartida(TipoPartida Tipo, int? DocumentoId, int? CuentaBancariaId, int CuentaId, string Referencia);

public sealed record PartidaRevaluacion(
    TipoPartida Tipo, int? DocumentoId, int? CuentaBancariaId, int CuentaId, string Referencia,
    decimal SaldoOriginal, decimal ValorLibrosGtq, decimal ValorCierreGtq)
{
    public decimal Ajuste => ValorCierreGtq - ValorLibrosGtq;

    public bool EsGanancia => Tipo == TipoPartida.CxP ? Ajuste < 0 : Ajuste > 0;
}

public sealed record LineaRevaluacion(int CuentaId, decimal Debito, decimal Credito);

public sealed record AsientoRevaluacionLineas(IReadOnlyList<LineaRevaluacion> Lineas, decimal Ganancia, decimal Perdida);

public static class RevaluacionRules
{
    public const string Leyenda = "Diferencia no realizada; sin efecto fiscal automático, validar con asesor.";
    private const int AnioMaximo = 9998;

    public static PartidaRevaluacion Crear(OrigenPartida origen, decimal saldoOriginal, decimal valorLibrosGtq, decimal tasaCierre) =>
        new(origen.Tipo, origen.DocumentoId, origen.CuentaBancariaId, origen.CuentaId, origen.Referencia, saldoOriginal, valorLibrosGtq,
            CambioRules.ConvertirAGtq(saldoOriginal, tasaCierre));

    public static (decimal Ganancia, decimal Perdida) Totales(IEnumerable<PartidaRevaluacion> partidas)
    {
        var lista = partidas.Where(p => p.Ajuste != 0).ToList();
        return (lista.Where(p => p.EsGanancia).Sum(p => Math.Abs(p.Ajuste)), lista.Where(p => !p.EsGanancia).Sum(p => Math.Abs(p.Ajuste)));
    }

    // RN-01: cada partida aporta el mismo importe a su cuenta de balance y a la cuenta de resultado.
    public static AsientoRevaluacionLineas ConstruirLineas(IReadOnlyList<PartidaRevaluacion> partidas, int? cuentaGananciaId, int? cuentaPerdidaId)
    {
        var (ganancia, perdida) = Totales(partidas);
        var efectoPorCuenta = new SortedDictionary<int, decimal>();
        foreach (var partida in partidas.Where(p => p.Ajuste != 0))
        {
            var firmado = partida.Tipo == TipoPartida.CxP ? -partida.Ajuste : partida.Ajuste;
            efectoPorCuenta[partida.CuentaId] = efectoPorCuenta.GetValueOrDefault(partida.CuentaId) + firmado;
        }

        var lineas = efectoPorCuenta
            .Where(e => e.Value != 0)
            .Select(e => e.Value > 0 ? new LineaRevaluacion(e.Key, e.Value, 0m) : new LineaRevaluacion(e.Key, 0m, -e.Value))
            .ToList();
        if (ganancia > 0)
        {
            lineas.Add(new LineaRevaluacion(cuentaGananciaId ?? throw CuentaFaltante("ganancia"), 0m, ganancia));
        }
        if (perdida > 0)
        {
            lineas.Add(new LineaRevaluacion(cuentaPerdidaId ?? throw CuentaFaltante("pérdida"), perdida, 0m));
        }
        return new AsientoRevaluacionLineas(lineas, ganancia, perdida);
    }

    public static IReadOnlyList<LineaRevaluacion> Invertir(IReadOnlyList<LineaRevaluacion> lineas) =>
        lineas.Select(l => new LineaRevaluacion(l.CuentaId, l.Credito, l.Debito)).ToList();

    public static DateOnly UltimoDiaDelMes(int anio, int mes) => new(anio, mes, DateTime.DaysInMonth(anio, mes));

    public static DateOnly PrimerDiaDelMesSiguiente(int anio, int mes) => new DateOnly(anio, mes, 1).AddMonths(1);

    public static ErrorRegla? ValidarMes(int anio, int mes, DateOnly hoyGuatemala)
    {
        if (mes is < 1 or > 12 || anio is < 1 or > AnioMaximo)
        {
            return new ErrorRegla("El mes debe estar entre 1 y 12 y el año entre 1 y 9998.", false);
        }
        return UltimoDiaDelMes(anio, mes) > hoyGuatemala
            ? new ErrorRegla($"El mes {mes:00}/{anio} aún no termina; la revaluación se ejecuta desde su último día.", true)
            : null;
    }

    private static InvalidOperationException CuentaFaltante(string lado) =>
        new($"Falta la cuenta de {lado} por revaluación.");
}
