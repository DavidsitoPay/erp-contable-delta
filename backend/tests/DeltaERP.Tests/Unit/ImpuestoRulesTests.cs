using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class ImpuestoRulesTests
{
    private static readonly DateOnly Fecha = new(2026, 10, 9);

    private static DatosImpuesto Valido() => new(
        "IVA_ESPECIAL", "IVA especial", Impuesto.TipoIvaGeneral, 12m, Impuesto.AmbitoAmbos, null, "Decreto 27-92, art. 10",
        new DateOnly(2026, 1, 1), null, null, null);

    private static Impuesto Catalogo(string tipo = Impuesto.TipoIvaGeneral, string aplicaA = Impuesto.AmbitoAmbos) => new()
    {
        Id = 1,
        Codigo = "X",
        Nombre = "Impuesto X",
        Tipo = tipo,
        Tasa = tipo is Impuesto.TipoExento or Impuesto.TipoNoAfecto ? 0m : 12m,
        AplicaA = aplicaA,
        GeneraCredito = tipo == Impuesto.TipoIvaGeneral,
        VigenteDesde = new DateOnly(2020, 1, 1),
    };

    private static Contraparte Tercero(string tipo, string regimenIva) => new() { Tipo = tipo, RegimenIva = regimenIva };

    [Theory]
    [InlineData("2026-10-09", "2026-10-09", null, true)]
    [InlineData("2026-10-08", "2026-10-09", null, false)]
    [InlineData("2026-10-09", "2026-01-01", "2026-10-09", true)]
    [InlineData("2026-10-10", "2026-01-01", "2026-10-09", false)]
    public void EstaVigente_ConLimitesInclusivosYHastaOpcional(string fecha, string desde, string? hasta, bool esperado)
    {
        var resultado = ImpuestoRules.EstaVigente(DateOnly.Parse(desde), hasta is null ? null : DateOnly.Parse(hasta), DateOnly.Parse(fecha));

        Assert.Equal(esperado, resultado);
    }

    [Fact]
    public void ValidarAlta_ConDatosCorrectos_NoDevuelveError()
    {
        Assert.Null(ImpuestoRules.ValidarAlta(Valido()));
    }

    public static TheoryData<int, string> AltasInvalidas => new()
    {
        { 0, "El código debe tener entre 3 y 30 caracteres" },
        { 1, "El código debe tener entre 3 y 30 caracteres" },
        { 2, "El nombre es obligatorio." },
        { 3, "La referencia legal es obligatoria." },
        { 4, "El nombre admite hasta 100 caracteres" },
        { 5, "Tipo de impuesto inválido." },
        { 6, "Tipo de impuesto inválido." },
        { 7, "La tasa debe estar entre 0 y 100 y tener hasta 4 decimales." },
        { 8, "La tasa debe estar entre 0 y 100 y tener hasta 4 decimales." },
        { 9, "Un impuesto exento o no afecto debe tener tasa 0." },
        { 10, "La tasa debe ser mayor que 0." },
        { 11, "Solo el IVA general puede generar crédito fiscal." },
        { 12, "El pequeño contribuyente solo aplica a compras." },
        { 13, "Ámbito inválido." },
        { 14, "La vigencia inicial es obligatoria." },
        { 15, "La vigencia final no puede ser anterior a la inicial." },
    };

    private static DatosImpuesto Mutacion(int caso)
    {
        var d = Valido();
        return caso switch
        {
            0 => d with { Codigo = "ab" },
            1 => d with { Codigo = "minuscula" },
            2 => d with { Nombre = " " },
            3 => d with { ArticuloLegal = null },
            4 => d with { Nombre = new string('N', 101) },
            5 => d with { Tipo = "LEGADO" },
            6 => d with { Tipo = null },
            7 => d with { Tasa = 100.5m },
            8 => d with { Tasa = 12.12345m },
            9 => d with { Tipo = Impuesto.TipoExento, Tasa = 5m },
            10 => d with { Tasa = 0m },
            11 => d with { Tipo = Impuesto.TipoNoAfecto, Tasa = 0m, GeneraCredito = true },
            12 => d with { Tipo = Impuesto.TipoPequenoContribuyente, Tasa = 5m, AplicaA = Impuesto.AmbitoVentas },
            13 => d with { AplicaA = "OTRO" },
            14 => d with { VigenteDesde = null },
            _ => d with { VigenteHasta = new DateOnly(2025, 12, 31) },
        };
    }

    [Theory]
    [MemberData(nameof(AltasInvalidas))]
    public void ValidarAlta_ConDatosInvalidos_DevuelveElMensajeDelCaso(int caso, string inicioDelMensaje)
    {
        Assert.StartsWith(inicioDelMensaje, ImpuestoRules.ValidarAlta(Mutacion(caso)));
    }

    [Theory]
    [InlineData(null, Impuesto.TipoIvaGeneral, true)]
    [InlineData(null, Impuesto.TipoExento, false)]
    [InlineData(false, Impuesto.TipoIvaGeneral, false)]
    public void ResolverGeneraCredito_UsaElValorEnviadoOElDefaultPorTipo(bool? enviado, string tipo, bool esperado)
    {
        Assert.Equal(esperado, ImpuestoRules.ResolverGeneraCredito(Valido() with { GeneraCredito = enviado, Tipo = tipo }));
    }

    [Fact]
    public void ValidarParaFactura_ConImpuestoAplicable_NoDevuelveError()
    {
        Assert.Null(ImpuestoRules.ValidarParaFactura(Catalogo(), Impuesto.AmbitoVentas, "CxC", Fecha));
    }

    [Fact]
    public void ValidarParaFactura_InactivoOHistorico_SeRechaza()
    {
        var inactivo = Catalogo();
        inactivo.Activo = false;

        Assert.Contains("está inactivo", ImpuestoRules.ValidarParaFactura(inactivo, Impuesto.AmbitoVentas, "CxC", Fecha));
        Assert.Contains("está inactivo", ImpuestoRules.ValidarParaFactura(Catalogo(Impuesto.TipoLegado), Impuesto.AmbitoVentas, "CxC", Fecha));
    }

    [Fact]
    public void ValidarParaFactura_FueraDeVigencia_IndicaElRango()
    {
        var vencido = Catalogo();
        vencido.VigenteHasta = new DateOnly(2025, 12, 31);

        var error = ImpuestoRules.ValidarParaFactura(vencido, Impuesto.AmbitoVentas, "CxC", Fecha);

        Assert.Equal("El impuesto 'Impuesto X' no está vigente el 2026-10-09 (vigencia: 2020-01-01 a 2025-12-31).", error);
        Assert.Contains("a sin fecha final", ImpuestoRules.ValidarParaFactura(Catalogo(), Impuesto.AmbitoVentas, "CxC", new DateOnly(2019, 1, 1)));
    }

    [Fact]
    public void ValidarParaFactura_DeOtroAmbito_IndicaElAmbito()
    {
        var error = ImpuestoRules.ValidarParaFactura(Catalogo(aplicaA: Impuesto.AmbitoCompras), Impuesto.AmbitoVentas, "CxC", Fecha);

        Assert.Equal("El impuesto 'Impuesto X' solo aplica a compras y no puede usarse en una factura de CxC.", error);
    }

    [Fact]
    public void Regimen_ClienteOProveedorExento_SoloAdmiteImpuestosExentosONoAfectos()
    {
        var conIva = new[] { Catalogo() };
        var sinIva = new[] { Catalogo(Impuesto.TipoExento), Catalogo(Impuesto.TipoNoAfecto) };

        Assert.Equal(
            "El cliente está registrado como exento de IVA: solo puede usar impuestos exentos o no afectos.",
            ImpuestoRules.ValidarConsistenciaRegimen(conIva, Tercero("Cliente", Contraparte.RegimenIvaExento)));
        Assert.Null(ImpuestoRules.ValidarConsistenciaRegimen(sinIva, Tercero("Proveedor", Contraparte.RegimenIvaExento)));
    }

    [Fact]
    public void Regimen_ImpuestoPequenoContribuyente_SoloConProveedorPequeno()
    {
        var pequeno = new[] { Catalogo(Impuesto.TipoPequenoContribuyente, Impuesto.AmbitoCompras) };

        Assert.StartsWith("El impuesto 'Pequeño contribuyente' solo puede usarse", ImpuestoRules.ValidarConsistenciaRegimen(pequeno, Tercero("Proveedor", Contraparte.RegimenIvaGeneral)));
        Assert.Null(ImpuestoRules.ValidarConsistenciaRegimen(pequeno, Tercero("Proveedor", Contraparte.RegimenIvaPequenoContribuyente)));
    }

    [Fact]
    public void Regimen_ProveedorPequenoContribuyente_NoAdmiteIvaGeneral()
    {
        var error = ImpuestoRules.ValidarConsistenciaRegimen(new[] { Catalogo() }, Tercero("Proveedor", Contraparte.RegimenIvaPequenoContribuyente));

        Assert.StartsWith("El proveedor es pequeño contribuyente", error);
    }

    [Fact]
    public void Regimen_ClienteGeneralConImpuestoExento_EsValido()
    {
        Assert.Null(ImpuestoRules.ValidarConsistenciaRegimen(new[] { Catalogo(Impuesto.TipoExento), Catalogo() }, Tercero("Cliente", Contraparte.RegimenIvaGeneral)));
    }

    [Theory]
    [InlineData(Impuesto.TipoIvaGeneral, TipoImpuesto.IvaGeneral)]
    [InlineData(Impuesto.TipoExento, TipoImpuesto.Exento)]
    [InlineData(Impuesto.TipoNoAfecto, TipoImpuesto.NoAfecto)]
    [InlineData(Impuesto.TipoPequenoContribuyente, TipoImpuesto.PequenoContribuyente)]
    [InlineData(Impuesto.TipoLegado, TipoImpuesto.Legado)]
    public void ComoParametro_TraduceElTipoDelCatalogo(string tipo, TipoImpuesto esperado)
    {
        var parametro = ImpuestoRules.ComoParametro(Catalogo(tipo));

        Assert.Equal((1, esperado), (parametro.Id, parametro.Tipo));
    }
}
