using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class EnteramientoRulesTests
{
    [Theory]
    [InlineData("SAT-1352", "2026-09-01", "2026-10-01", "B-1", 10, null)]
    [InlineData("X", "2026-09-01", "2026-10-01", "B-1", 10, "Formulario")]
    [InlineData("SAT-1331", "2026-09-15", "2026-10-01", "B-1", 10, "primer día")]
    [InlineData("SAT-1331", "0001-01-01", "2026-10-01", "B-1", 10, "obligatorio")]
    [InlineData("SAT-1331", "2026-09-01", "2026-08-31", "B-1", 10, "anterior")]
    [InlineData("SAT-1331", "2026-09-01", "2026-10-01", "  ", 10, "boleta")]
    [InlineData("SAT-1331", "2026-09-01", "2026-10-01", "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB", 10, "boleta")]
    [InlineData("SAT-1331", "2026-09-01", "2026-10-01", "B-1", 0, "monto")]
    public void ValidarSolicitud_DetectaCadaDatoInvalido(string? formulario, string periodo, string fechaPago, string? boleta, int monto, string? fragmento)
    {
        var error = EnteramientoRules.ValidarSolicitud(formulario, DateOnly.Parse(periodo), DateOnly.Parse(fechaPago), boleta, monto);

        if (fragmento is null)
        {
            Assert.Null(error);
        }
        else
        {
            Assert.Contains(fragmento, error);
        }
    }

    [Fact]
    public void ValidarMonto_RechazaMasDeDosDecimales() =>
        Assert.NotNull(EnteramientoRules.ValidarMonto(10.005m));

    [Theory]
    [InlineData(10, 100, null)]
    [InlineData(100, 100, null)]
    [InlineData(101, 100, "excede")]
    [InlineData(1, 0, "no tiene saldo")]
    public void ValidarSaldo_ExigeMontoDentroDelSaldo(int monto, int saldo, string? fragmento)
    {
        var error = EnteramientoRules.ValidarSaldo(monto, saldo);

        Assert.Equal(fragmento is null, error is null);
        Assert.True(fragmento is null || error!.Contains(fragmento));
    }

    [Fact]
    public void MotivoYReferencia_SonObligatoriosYAcotados()
    {
        Assert.Null(EnteramientoRules.ValidarMotivo("Boleta duplicada"));
        Assert.NotNull(EnteramientoRules.ValidarMotivo(" "));
        Assert.NotNull(EnteramientoRules.ValidarMotivo(new string('m', 301)));
        Assert.Null(EnteramientoRules.ValidarReferencia("Boleta 99"));
        Assert.NotNull(EnteramientoRules.ValidarReferencia(null));
        Assert.NotNull(EnteramientoRules.ValidarReferencia(new string('r', 101)));
    }

    [Fact]
    public void SaldoYDescripcion_SeArmanConElFormato()
    {
        Assert.Equal(60m, EnteramientoRules.Saldo(100m, 40m));
        Assert.Equal("Enteramiento ISR no residentes SAT-1352 boleta B-1 período 2026-09", EnteramientoRules.Descripcion("SAT-1352", "B-1", new DateOnly(2026, 9, 1)));
    }
}
