using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class DteRulesTests
{
    private static readonly DateOnly FechaDocumento = new(2026, 10, 9);
    private static readonly DateTimeOffset Ahora = new(2026, 10, 9, 18, 0, 0, TimeSpan.FromHours(-6));
    private static readonly DateTimeOffset Certificacion = new(2026, 10, 9, 10, 15, 0, TimeSpan.FromHours(-6));

    private static DatosDte Completo() => new(Guid.NewGuid(), "A1B2C3D4", "1234567890", Certificacion);

    private static string? Validar(DatosDte dte, bool obligatorio = true, bool exigeSoporte = false) =>
        DteRules.Validar(dte, obligatorio, exigeSoporte, FechaDocumento, Ahora);

    [Fact]
    public void DteCompleto_EsValido()
    {
        Assert.Null(Validar(Completo()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Obligatorio_ConCualquierDatoFaltante_DevuelveElMensajeDeCxC(int campoFaltante)
    {
        var dte = campoFaltante switch
        {
            0 => Completo() with { Uuid = null },
            1 => Completo() with { Serie = " " },
            2 => Completo() with { Numero = null },
            _ => Completo() with { FechaCertificacion = null },
        };

        Assert.StartsWith("Las facturas de cuentas por cobrar requieren los datos del DTE", Validar(dte));
    }

    [Fact]
    public void UuidVacio_SeTrataComoAusente()
    {
        Assert.NotNull(Validar(Completo() with { Uuid = Guid.Empty }));
    }

    [Fact]
    public void Opcional_SinNingunDato_EsValido()
    {
        Assert.Null(Validar(new DatosDte(null, null, null, null), obligatorio: false));
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Opcional_ConTrioIncompleto_DevuelveElMensajeDeProveedor(bool conUuid, bool conSerie, bool conNumero)
    {
        var dte = new DatosDte(conUuid ? Guid.NewGuid() : null, conSerie ? "S1" : null, conNumero ? "N1" : null, null);

        Assert.Equal("Los datos del DTE del proveedor deben indicar UUID, serie y número juntos.", Validar(dte, obligatorio: false));
    }

    [Fact]
    public void ExigeSoporteDeCredito_SinTrio_DevuelveMensajeDeCreditoFiscal()
    {
        var error = Validar(new DatosDte(null, null, null, null), obligatorio: false, exigeSoporte: true);

        Assert.Equal("Para registrar crédito fiscal se requieren el UUID, la serie y el número del DTE del proveedor.", error);
    }

    [Fact]
    public void ExigeSoporteDeCredito_ConTrioSinFechaDeCertificacion_EsValido()
    {
        var dte = Completo() with { FechaCertificacion = null };

        Assert.Null(Validar(dte, obligatorio: false, exigeSoporte: true));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SerieONumeroConMasDeVeinteCaracteres_SonInvalidos(bool enSerie)
    {
        var largo = new string('X', 21);
        var dte = enSerie ? Completo() with { Serie = largo } : Completo() with { Numero = largo };

        Assert.Equal("La serie y el número del DTE deben tener entre 1 y 20 caracteres.", Validar(dte));
    }

    [Fact]
    public void FechaDeCertificacionFutura_EsInvalida()
    {
        var dte = Completo() with { FechaCertificacion = Ahora.AddMinutes(6) };

        Assert.Equal("La fecha de certificación del DTE no puede ser futura.", Validar(dte));
    }

    [Fact]
    public void FechaDeCertificacionDentroDeLaToleranciaDeCincoMinutos_EsValida()
    {
        var dte = Completo() with { FechaCertificacion = Ahora.AddMinutes(4) };

        Assert.Null(Validar(dte));
    }

    [Fact]
    public void FechaDeCertificacionAnteriorAlDiaDelDocumento_EsInvalida()
    {
        var dte = Completo() with { FechaCertificacion = new DateTimeOffset(2026, 10, 8, 23, 59, 0, TimeSpan.FromHours(-6)) };

        Assert.Equal("La fecha de certificación no puede ser anterior a la fecha del documento.", Validar(dte));
    }

    [Fact]
    public void FechaDeCertificacionAlInicioDelDiaEnGuatemala_EsValida()
    {
        var dte = Completo() with { FechaCertificacion = new DateTimeOffset(2026, 10, 9, 6, 0, 0, TimeSpan.Zero) };

        Assert.Null(Validar(dte));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void InterpretarUuid_SinValor_NoEsError(string? texto)
    {
        var (uuid, error) = DteRules.InterpretarUuid(texto);

        Assert.Null(uuid);
        Assert.Null(error);
    }

    [Fact]
    public void InterpretarUuid_ConFormatoCanonico_DevuelveElGuid()
    {
        var esperado = Guid.NewGuid();

        var (uuid, error) = DteRules.InterpretarUuid(esperado.ToString("D").ToUpperInvariant());

        Assert.Equal(esperado, uuid);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("3f2504e04f8941d39a0c0305e82c3301")]
    [InlineData("{3f2504e0-4f89-41d3-9a0c-0305e82c3301}")]
    [InlineData("no-es-un-uuid")]
    public void InterpretarUuid_ConOtroFormato_DevuelveElMensaje(string texto)
    {
        var (uuid, error) = DteRules.InterpretarUuid(texto);

        Assert.Null(uuid);
        Assert.Equal("El UUID del DTE debe tener formato canónico de 36 caracteres.", error);
    }
}
