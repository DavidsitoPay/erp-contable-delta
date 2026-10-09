using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class EstadosFinancierosTests
{
    private static readonly string[] CodigosOrdenados = { "1", "10", "11", "2" };

    private static readonly SaldoReporteCuenta[] Cuadrado =
    {
        Fila(1, "1", "Activo", 1600m, esHoja: false),
        Fila(2, "11", "Activo", 1000m, nivel: 2),
        Fila(3, "12", "Activo", 600m, nivel: 2),
        Fila(4, "2", "Pasivo", 300m),
        Fila(5, "3", "Capital", 1000m),
        Fila(6, "4", "Ingreso", 500m),
        Fila(7, "5", "Gasto", 200m),
    };

    private static SaldoReporteCuenta Fila(int id, string codigo, string tipo, decimal saldo, int nivel = 1, bool esHoja = true) => new()
    {
        CuentaId = id,
        Codigo = codigo,
        Nombre = $"Cuenta {codigo}",
        Tipo = tipo,
        Nivel = nivel,
        EsHoja = esHoja,
        Saldo = saldo,
    };

    [Fact]
    public void Seccionar_FiltraPorTipoOrdenaPorCodigoYSumaSoloNivelUno()
    {
        var filas = new[]
        {
            Fila(1, "10", "Activo", 70m),
            Fila(2, "2", "Activo", 30m),
            Fila(3, "1", "Activo", 150m, esHoja: false),
            Fila(4, "11", "Activo", 150m, nivel: 2),
            Fila(5, "9", "Pasivo", 999m),
        };

        var seccion = EstadosFinancieros.Seccionar(filas, "Activo");

        Assert.Equal(CodigosOrdenados, seccion.Cuentas.Select(c => c.Codigo).ToArray());
        Assert.Equal(250m, seccion.Total);
        Assert.False(seccion.Cuentas[0].EsHoja);
    }

    [Fact]
    public void BalanceGeneral_IncluyeElResultadoEnElCapitalYCuadra()
    {
        var balance = EstadosFinancieros.BalanceGeneral(Cuadrado);

        Assert.Equal(1600m, balance.Activo.Total);
        Assert.Equal(300m, balance.Pasivo.Total);
        Assert.Equal(1000m, balance.Capital.Total);
        Assert.Equal(300m, balance.ResultadoEjercicio);
        Assert.Equal(1300m, balance.TotalCapital);
        Assert.Equal(1600m, balance.TotalPasivoCapital);
        Assert.Equal(0m, balance.Diferencia);
        Assert.True(balance.Cuadra);
    }

    [Fact]
    public void BalanceGeneral_ConDescuadre_ReportaLaDiferenciaConSigno()
    {
        var filas = Cuadrado.Append(Fila(8, "13", "Activo", 25m)).ToArray();

        var balance = EstadosFinancieros.BalanceGeneral(filas);

        Assert.Equal(25m, balance.Diferencia);
        Assert.False(balance.Cuadra);
    }

    [Fact]
    public void BalanceGeneral_ConPerdida_RestaDelCapital()
    {
        var filas = new[]
        {
            Fila(1, "1", "Activo", 900m),
            Fila(2, "3", "Capital", 1000m),
            Fila(3, "4", "Ingreso", 100m),
            Fila(4, "5", "Gasto", 200m),
        };

        var balance = EstadosFinancieros.BalanceGeneral(filas);

        Assert.Equal(-100m, balance.ResultadoEjercicio);
        Assert.Equal(900m, balance.TotalCapital);
        Assert.True(balance.Cuadra);
    }

    [Fact]
    public void BalanceGeneral_SinCuentas_QuedaEnCeroYCuadra()
    {
        var balance = EstadosFinancieros.BalanceGeneral(Array.Empty<SaldoReporteCuenta>());

        Assert.Empty(balance.Activo.Cuentas);
        Assert.Equal(0m, balance.TotalPasivoCapital);
        Assert.True(balance.Cuadra);
    }

    [Theory]
    [InlineData(500, 200, 300)]
    [InlineData(100, 250, -150)]
    [InlineData(0, 0, 0)]
    public void EstadoResultados_UtilidadNetaEsIngresosMenosGastos(int ingresos, int gastos, int esperada)
    {
        var filas = new[]
        {
            Fila(1, "4", "Ingreso", ingresos),
            Fila(2, "5", "Gasto", gastos),
            Fila(3, "1", "Activo", 9999m),
        };

        var estado = EstadosFinancieros.EstadoResultados(filas);

        Assert.Equal((decimal)ingresos, estado.Ingresos.Total);
        Assert.Equal((decimal)gastos, estado.Gastos.Total);
        Assert.Equal((decimal)esperada, estado.UtilidadNeta);
        Assert.Single(estado.Ingresos.Cuentas);
        Assert.Single(estado.Gastos.Cuentas);
    }
}
