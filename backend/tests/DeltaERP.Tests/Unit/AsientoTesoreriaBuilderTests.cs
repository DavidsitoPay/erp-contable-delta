using System.Text.RegularExpressions;
using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class AsientoTesoreriaBuilderTests
{
    private static readonly DateOnly Fecha = new(2025, 3, 15);
    private static readonly (int CuentaId, decimal Monto)[] UnaContrapartida = { (20, 100m) };
    private static readonly (int CuentaId, decimal Monto)[] DosContrapartidas = { (20, 60m), (30, 40.50m) };

    private static AsientoContable Construir(string tipo, (int CuentaId, decimal Monto)[] contrapartidas, string prefijo = "TES-MAN") =>
        AsientoTesoreriaBuilder.Construir(prefijo, Fecha, 7, 9, tipo, 10, contrapartidas);

    [Fact]
    public void Ingreso_DebitaElBancoYAcreditaLaContrapartida()
    {
        var asiento = Construir(MovimientoTesoreria.TipoIngreso, UnaContrapartida);

        Assert.Equal(2, asiento.Lineas.Count);
        Assert.Equal(10, asiento.Lineas[0].CuentaId);
        Assert.Equal(100m, asiento.Lineas[0].Debito);
        Assert.Equal(0m, asiento.Lineas[0].Credito);
        Assert.Equal(20, asiento.Lineas[1].CuentaId);
        Assert.Equal(0m, asiento.Lineas[1].Debito);
        Assert.Equal(100m, asiento.Lineas[1].Credito);
    }

    [Fact]
    public void Egreso_AcreditaElBancoYDebitaLaContrapartida()
    {
        var asiento = Construir(MovimientoTesoreria.TipoEgreso, UnaContrapartida);

        Assert.Equal(0m, asiento.Lineas[0].Debito);
        Assert.Equal(100m, asiento.Lineas[0].Credito);
        Assert.Equal(100m, asiento.Lineas[1].Debito);
        Assert.Equal(0m, asiento.Lineas[1].Credito);
    }

    [Fact]
    public void VariasContrapartidas_GeneranUnaLineaPorCuentaYCuadran()
    {
        var asiento = Construir(MovimientoTesoreria.TipoEgreso, DosContrapartidas);

        Assert.Equal(3, asiento.Lineas.Count);
        Assert.Equal(100.50m, asiento.Monto);
        Assert.Equal(asiento.Lineas.Sum(l => l.Debito), asiento.Lineas.Sum(l => l.Credito));
        Assert.True(PartidaDobleValidator.EsValido(asiento, out _));
    }

    [Fact]
    public void Transferencia_DebitaElDestinoYAcreditaElOrigen()
    {
        var asiento = Construir(MovimientoTesoreria.TipoIngreso, UnaContrapartida, "TES-TRF");

        Assert.Equal(100m, asiento.Lineas.Single(l => l.CuentaId == 10).Debito);
        Assert.Equal(100m, asiento.Lineas.Single(l => l.CuentaId == 20).Credito);
    }

    [Fact]
    public void Asiento_QuedaConfirmadoConLosDatosDeCabecera()
    {
        var asiento = Construir(MovimientoTesoreria.TipoIngreso, UnaContrapartida);

        Assert.Equal(AsientoContable.EstadoConfirmado, asiento.Estado);
        Assert.Equal(Fecha, asiento.Fecha);
        Assert.Equal(7, asiento.PeriodoId);
        Assert.Equal(9, asiento.UsuarioId);
        Assert.Equal(100m, asiento.Monto);
        Assert.Null(asiento.TipoCambioAplicado);
    }

    [Theory]
    [InlineData("TES-MAN")]
    [InlineData("TES-TRF")]
    [InlineData("TES-CXC")]
    [InlineData("TES-CXP")]
    [InlineData("TES-APE")]
    public void Numero_TieneFormatoPrefijoFechaYSufijoAleatorio(string prefijo)
    {
        var asiento = Construir(MovimientoTesoreria.TipoIngreso, UnaContrapartida, prefijo);

        Assert.Matches(new Regex($"^{prefijo}-20250315-[0-9a-f]{{8}}$"), asiento.Numero);
        Assert.True(asiento.Numero.Length <= 30);
    }
}
