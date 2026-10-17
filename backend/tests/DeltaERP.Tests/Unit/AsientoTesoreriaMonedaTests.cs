using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class AsientoTesoreriaMonedaTests
{
    private const int Banco = 10;
    private const int Control = 20;
    private const int Ganancia = 30;
    private const int Perdida = 40;

    private static CabeceraAsiento Cab() => new("TES-CXC", new DateOnly(2026, 11, 10), 1, 1);

    private static List<(int CuentaId, decimal Debito, decimal Credito)> Lineas(AsientoContable asiento) =>
        asiento.Lineas.Select(l => (l.CuentaId, l.Debito, l.Credito)).ToList();

    [Fact]
    public void Cobro_ConGanancia()
    {
        var asiento = AsientoTesoreriaBuilder.ConstruirLiquidacion(
            Cab(), true, Banco, 3080m, [(Control, 3040m)], new DiferencialLiquidacion(Ganancia, Perdida, 40m, 0m));

        Assert.Equal(
            new List<(int, decimal, decimal)> { (Banco, 3080m, 0m), (Control, 0m, 3040m), (Ganancia, 0m, 40m) },
            Lineas(asiento));
        Assert.Equal(3080m, asiento.Monto);
        Assert.True(PartidaDobleValidator.EsValido(asiento, out _));
    }

    [Fact]
    public void Cobro_ConPerdida()
    {
        var asiento = AsientoTesoreriaBuilder.ConstruirLiquidacion(
            Cab(), true, Banco, 5400m, [(Control, 5472m)], new DiferencialLiquidacion(Ganancia, Perdida, 0m, 72m));

        Assert.Equal(
            new List<(int, decimal, decimal)> { (Banco, 5400m, 0m), (Control, 0m, 5472m), (Perdida, 72m, 0m) },
            Lineas(asiento));
        Assert.Equal(5472m, asiento.Monto);
    }

    [Fact]
    public void Pago_ConPerdida()
    {
        var asiento = AsientoTesoreriaBuilder.ConstruirLiquidacion(
            Cab(), false, Banco, 7315m, [(Control, 7220m)], new DiferencialLiquidacion(Ganancia, Perdida, 0m, 95m));

        Assert.Equal(
            new List<(int, decimal, decimal)> { (Banco, 0m, 7315m), (Control, 7220m, 0m), (Perdida, 95m, 0m) },
            Lineas(asiento));
        Assert.Equal(7315m, asiento.Monto);
    }

    [Fact]
    public void Pago_ConGanancia()
    {
        var asiento = AsientoTesoreriaBuilder.ConstruirLiquidacion(
            Cab(), false, Banco, 7000m, [(Control, 7220m)], new DiferencialLiquidacion(Ganancia, Perdida, 220m, 0m));

        Assert.Contains((Ganancia, 0m, 220m), Lineas(asiento));
        Assert.True(PartidaDobleValidator.EsValido(asiento, out _));
    }

    [Fact]
    public void DiferencialCero_NoGeneraLinea()
    {
        var asiento = AsientoTesoreriaBuilder.ConstruirLiquidacion(
            Cab(), true, Banco, 100m, [(Control, 100m)], DiferencialLiquidacion.Ninguno);

        Assert.Equal(2, asiento.Lineas.Count);
    }

    [Fact]
    public void VariasCuentasDeControl_UnaLineaPorCuenta()
    {
        var asiento = AsientoTesoreriaBuilder.ConstruirLiquidacion(
            Cab(), true, Banco, 100m, [(Control, 60m), (21, 40m)], DiferencialLiquidacion.Ninguno);

        Assert.Equal(3, asiento.Lineas.Count);
    }

    [Fact]
    public void NoCuadra_LanzaInvalidOperation()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AsientoTesoreriaBuilder.ConstruirLiquidacion(Cab(), true, Banco, 100m, [(Control, 90m)], DiferencialLiquidacion.Ninguno));
    }

    [Fact]
    public void GananciaSinCuenta_LanzaInvalidOperation()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AsientoTesoreriaBuilder.ConstruirLiquidacion(Cab(), true, Banco, 100m, [(Control, 90m)], new DiferencialLiquidacion(null, Perdida, 10m, 0m)));
    }

    private static AsientoContable Cruzada(decimal destino, decimal origen, int? ganancia = Ganancia, int? perdida = Perdida) =>
        AsientoTesoreriaBuilder.ConstruirTransferenciaCruzada(Cab(), Banco, destino, Control, origen, ganancia, perdida);

    [Fact]
    public void TransferenciaCruzada_CompraDeDolares_RegistraPerdidaRealizada()
    {
        var asiento = Cruzada(7641.57m, 7700m);

        Assert.Equal(
            new List<(int, decimal, decimal)> { (Banco, 7641.57m, 0m), (Control, 0m, 7700m), (Perdida, 58.43m, 0m) },
            Lineas(asiento));
        Assert.Equal(7700m, asiento.Monto);
        Assert.True(PartidaDobleValidator.EsValido(asiento, out _));
    }

    [Fact]
    public void TransferenciaCruzada_VentaDeDolares_RegistraPerdidaRealizada()
    {
        var asiento = Cruzada(7600m, 7641.57m);

        Assert.Equal(
            new List<(int, decimal, decimal)> { (Banco, 7600m, 0m), (Control, 0m, 7641.57m), (Perdida, 41.57m, 0m) },
            Lineas(asiento));
        Assert.True(PartidaDobleValidator.EsValido(asiento, out _));
    }

    [Fact]
    public void TransferenciaCruzada_ConGanancia_UsaLaCuentaDeGanancia()
    {
        var asiento = Cruzada(7700m, 7641.57m);

        Assert.Contains((Ganancia, 0m, 58.43m), Lineas(asiento));
        Assert.True(PartidaDobleValidator.EsValido(asiento, out _));
    }

    [Fact]
    public void TransferenciaCruzada_SinDiferencia_TieneDosLineas()
    {
        var asiento = Cruzada(7700m, 7700m);

        Assert.Equal(2, asiento.Lineas.Count);
    }

    [Fact]
    public void TransferenciaCruzada_SinCuentaDeDiferencial_Lanza()
    {
        Assert.Throws<InvalidOperationException>(() => Cruzada(7600m, 7641.57m, perdida: null));
        Assert.Throws<InvalidOperationException>(() => Cruzada(7700m, 7641.57m, ganancia: null));
    }
}
