using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class PagoRulesNotaCreditoTests
{
    [Fact]
    public void AplicarPagoAUnaNotaDeCredito_SeRechazaAntesDeEvaluarElSaldo()
    {
        var documentos = new[] { new DocumentoPagable(1, "NC-1", 1, "Vigente", 0m, null, true) };

        var error = PagoRules.ValidarAplicaciones("cliente", 1, new[] { (1, 5m) }, documentos);

        Assert.Equal("Una nota de crédito no admite pagos ni cobros; solo reduce el saldo contable.", error);
    }

    [Fact]
    public void AplicarPagoAUnaFacturaConSaldo_NoDevuelveError()
    {
        var documentos = new[] { new DocumentoPagable(1, "F-1", 1, "Vigente", 100m) };

        Assert.Null(PagoRules.ValidarAplicaciones("cliente", 1, new[] { (1, 5m) }, documentos));
    }
}
