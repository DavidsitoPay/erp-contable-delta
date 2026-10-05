using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class TesoreriaRulesTests
{
    private static readonly (int DocumentoId, decimal Monto)[] Aplicaciones = { (1, 10m), (2, 5m), (3, 7m) };
    private static readonly Dictionary<int, int> ControlMixto = new() { [1] = 100, [2] = 100, [3] = 50 };
    private static readonly Dictionary<int, int> ControlUnico = new() { [1] = 100, [2] = 100, [3] = 100 };
    private static readonly (int, decimal)[] AgrupadoEsperado = { (50, 7m), (100, 15m) };
    private static readonly decimal[] MontosValidos = { 10m, 5.25m };
    private static readonly decimal[] MontosConTresDecimales = { 10m, 5.255m };

    [Fact]
    public void ValidarMonto_RechazaCeroNegativoDecimalesYExceso()
    {
        Assert.Equal("El monto debe ser mayor a cero.", TesoreriaRules.ValidarMonto(0m));
        Assert.Equal("El monto debe ser mayor a cero.", TesoreriaRules.ValidarMonto(-1m));
        Assert.Equal("El monto no puede tener más de 2 decimales.", TesoreriaRules.ValidarMonto(1.005m));
        Assert.Equal("El monto excede el máximo permitido.", TesoreriaRules.ValidarMonto(1000000000000m));
        Assert.Null(TesoreriaRules.ValidarMonto(999999999999.99m));
        Assert.Null(TesoreriaRules.ValidarMonto(0.01m));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Otro")]
    [InlineData("ingreso")]
    public void ValidarMovimiento_RechazaTipoInvalido(string? tipo)
    {
        Assert.Equal("El tipo debe ser Ingreso o Egreso.", TesoreriaRules.ValidarMovimiento(tipo, 10m, "Texto"));
    }

    [Fact]
    public void ValidarMovimiento_ValidaMontoYDescripcionEnOrden()
    {
        Assert.Equal("El monto debe ser mayor a cero.", TesoreriaRules.ValidarMovimiento("Ingreso", 0m, ""));
        Assert.Equal("La descripción es obligatoria.", TesoreriaRules.ValidarMovimiento("Egreso", 10m, "  "));
        Assert.Equal("La descripción es obligatoria.", TesoreriaRules.ValidarMovimiento("Egreso", 10m, null));
        Assert.Equal("La descripción no puede exceder 255 caracteres.", TesoreriaRules.ValidarMovimiento("Egreso", 10m, new string('x', 256)));
        Assert.Null(TesoreriaRules.ValidarMovimiento("Ingreso", 10m, new string('x', 255)));
    }

    [Fact]
    public void ValidarTransferencia_RechazaMismaCuentaYValidaMontoYDescripcion()
    {
        Assert.Equal("La cuenta de origen y la de destino deben ser distintas.", TesoreriaRules.ValidarTransferencia(1, 1, 10m, "Texto"));
        Assert.Equal("El monto debe ser mayor a cero.", TesoreriaRules.ValidarTransferencia(1, 2, 0m, "Texto"));
        Assert.Equal("La descripción es obligatoria.", TesoreriaRules.ValidarTransferencia(1, 2, 10m, ""));
        Assert.Null(TesoreriaRules.ValidarTransferencia(1, 2, 10m, "Texto"));
    }

    [Fact]
    public void ValidarCuentaBancaria_RechazaCamposVaciosLargosYTipoInvalido()
    {
        Assert.Equal("El banco es obligatorio.", TesoreriaRules.ValidarCuentaBancaria(" ", "123", "Ahorro"));
        Assert.Equal("El banco no puede exceder 100 caracteres.", TesoreriaRules.ValidarCuentaBancaria(new string('b', 101), "123", "Ahorro"));
        Assert.Equal("El número de cuenta es obligatorio.", TesoreriaRules.ValidarCuentaBancaria("Banco", null, "Ahorro"));
        Assert.Equal("El número de cuenta no puede exceder 50 caracteres.", TesoreriaRules.ValidarCuentaBancaria("Banco", new string('1', 51), "Ahorro"));
        Assert.Equal("El tipo debe ser Monetaria o Ahorro.", TesoreriaRules.ValidarCuentaBancaria("Banco", "123", "Corriente"));
        Assert.Equal("El tipo debe ser Monetaria o Ahorro.", TesoreriaRules.ValidarCuentaBancaria("Banco", "123", null));
        Assert.Null(TesoreriaRules.ValidarCuentaBancaria("Banco", "123", "Monetaria"));
    }

    [Fact]
    public void ValidarApertura_ExigeFechaYContrapartidaSoloConSaldoPositivo()
    {
        var fecha = new DateOnly(2025, 1, 1);

        Assert.Equal("El saldo de apertura no puede ser negativo.", TesoreriaRules.ValidarApertura(-1m, fecha, 1));
        Assert.Equal("El saldo de apertura no puede tener más de 2 decimales.", TesoreriaRules.ValidarApertura(1.005m, fecha, 1));
        Assert.Equal("El saldo de apertura excede el máximo permitido.", TesoreriaRules.ValidarApertura(1000000000000m, fecha, 1));
        Assert.Equal("La fecha de apertura es obligatoria cuando hay saldo de apertura.", TesoreriaRules.ValidarApertura(10m, null, 1));
        Assert.Equal("La cuenta de contrapartida de la apertura es obligatoria cuando hay saldo de apertura.", TesoreriaRules.ValidarApertura(10m, fecha, null));
        Assert.Null(TesoreriaRules.ValidarApertura(10m, fecha, 1));
        Assert.Null(TesoreriaRules.ValidarApertura(0m, null, null));
    }

    [Fact]
    public void ValidarSaldoExtracto_PermiteCeroYNegativosPeroNoDecimalesNiExceso()
    {
        Assert.Null(TesoreriaRules.ValidarSaldoExtracto(0m));
        Assert.Null(TesoreriaRules.ValidarSaldoExtracto(-250.75m));
        Assert.Equal("El saldo del extracto no puede tener más de 2 decimales.", TesoreriaRules.ValidarSaldoExtracto(1.005m));
        Assert.Equal("El saldo del extracto excede el máximo permitido.", TesoreriaRules.ValidarSaldoExtracto(-1000000000000m));
    }

    [Fact]
    public void ValidarEscalaPago_RechazaMontosConMasDeDosDecimales()
    {
        Assert.Null(TesoreriaRules.ValidarEscalaPago(MontosValidos));
        Assert.Equal("Los montos del pago no pueden tener más de 2 decimales.", TesoreriaRules.ValidarEscalaPago(MontosConTresDecimales));
    }

    [Fact]
    public void ValidarMarca_RechazaInexistenteAperturaOtraCuentaYPosteriorAlCorte()
    {
        var conciliacion = new ConciliacionBancaria { CuentaBancariaId = 5, Fecha = new DateOnly(2025, 3, 30) };
        MovimientoTesoreria Movimiento(string origen = "Manual", int cuenta = 5, int dia = 15) =>
            new() { Origen = origen, CuentaBancariaId = cuenta, Fecha = new DateOnly(2025, 3, dia) };

        Assert.Equal("El movimiento indicado no existe.", TesoreriaRules.ValidarMarca(conciliacion, null));
        Assert.Equal("El movimiento de apertura no es conciliable: ya forma parte del saldo inicial.", TesoreriaRules.ValidarMarca(conciliacion, Movimiento(origen: "Apertura")));
        Assert.Equal("El movimiento no pertenece a la cuenta bancaria de la conciliación.", TesoreriaRules.ValidarMarca(conciliacion, Movimiento(cuenta: 6)));
        Assert.Equal("El movimiento es posterior a la fecha de corte de la conciliación.", TesoreriaRules.ValidarMarca(conciliacion, Movimiento(dia: 31)));
        Assert.Null(TesoreriaRules.ValidarMarca(conciliacion, Movimiento()));
    }

    [Fact]
    public void AgruparPorCuentaControl_SumaPorCuentaYOrdenaPorCuenta()
    {
        var resultado = TesoreriaRules.AgruparPorCuentaControl(Aplicaciones, ControlMixto);

        Assert.Equal(AgrupadoEsperado, resultado.Select(r => (r.CuentaId, r.Monto)).ToArray());
    }

    [Fact]
    public void AgruparPorCuentaControl_ConUnSoloControl_DevuelveUnaLinea()
    {
        var resultado = TesoreriaRules.AgruparPorCuentaControl(Aplicaciones, ControlUnico);

        Assert.Single(resultado);
        Assert.Equal(22m, resultado[0].Monto);
    }
}
