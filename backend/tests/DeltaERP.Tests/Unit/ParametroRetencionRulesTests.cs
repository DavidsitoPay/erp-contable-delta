using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class ParametroRetencionRulesTests
{
    private static readonly DateOnly Desde = new(2030, 1, 1);

    private static DatosParametroRetencion Valido() => new(
        "ISR_NUEVO", "ISR nuevo", "ISR_PROVEEDOR_ROS", 5m, 30000m, 7m, 2500m, "BASE", null, "Decreto 10-2012", null, Desde, null, null);

    private static DatosParametroRetencion Plazo() => new(
        "PLAZO_NUEVO", "Plazo nuevo", "PLAZO_CONSTANCIA", null, null, null, 0m, "BASE", 5, "Decreto 10-2012", null, Desde, null, null);

    [Fact]
    public void AltaCompleta_NoDevuelveError()
    {
        Assert.Null(ParametroRetencionRules.ValidarAlta(Valido()));
        Assert.Null(ParametroRetencionRules.ValidarAlta(Plazo()));
        Assert.Null(ParametroRetencionRules.ValidarAlta(Valido() with { TramoLimite = null, TasaExcedente = null, UmbralMinimo = null, UmbralSobre = null }));
    }

    public static TheoryData<int, string> Invalidas => new()
    {
        { 0, "El código debe tener entre 3 y 40 caracteres" },
        { 1, "El código debe tener entre 3 y 40 caracteres" },
        { 2, "El nombre y la referencia legal son obligatorios." },
        { 3, "El nombre admite hasta 120 caracteres" },
        { 4, "La nota admite hasta 300 caracteres." },
        { 5, "Concepto inválido" },
        { 6, "La tasa debe ser mayor que 0" },
        { 7, "La tasa debe ser mayor que 0" },
        { 8, "La tasa debe ser mayor que 0" },
        { 9, "Solo un plazo de constancia lleva día límite." },
        { 10, "El tramo exige un límite mayor que 0" },
        { 11, "Solo la retención de ISR a proveedores admite tramo." },
        { 12, "El límite del tramo admite 2 decimales" },
        { 13, "El umbral debe aplicarse sobre uno de" },
        { 14, "El umbral mínimo debe ser mayor o igual a cero" },
        { 15, "La vigencia inicial es obligatoria." },
        { 16, "La vigencia final no puede ser anterior a la inicial." },
        { 17, "El día límite debe estar entre 1 y 28." },
        { 18, "Un plazo de constancia no lleva tasas ni tramo." },
    };

    private static DatosParametroRetencion Mutacion(int caso)
    {
        var d = Valido();
        return caso switch
        {
            0 => d with { Codigo = "AB" },
            1 => d with { Codigo = "minuscula" },
            2 => d with { Nombre = " " },
            3 => d with { Nombre = new string('N', 121) },
            4 => d with { Nota = new string('n', 301) },
            5 => d with { Concepto = "OTRO" },
            6 => d with { TasaBase = 0m },
            7 => d with { TasaBase = 100.5m },
            8 => d with { TasaBase = 5.00001m },
            9 => d with { DiaLimite = 3 },
            10 => d with { TasaExcedente = null },
            11 => d with { Concepto = "IVA_RETENIDO_CLIENTE" },
            12 => d with { TramoLimite = 30000.001m },
            13 => d with { UmbralSobre = "OTRO" },
            14 => d with { UmbralMinimo = -1m },
            15 => d with { VigenteDesde = null },
            16 => d with { VigenteHasta = Desde.AddDays(-1) },
            17 => Plazo() with { DiaLimite = 29 },
            _ => Plazo() with { TasaBase = 5m },
        };
    }

    [Theory]
    [MemberData(nameof(Invalidas))]
    public void AltaInvalida_DevuelveElMensajeDelCaso(int caso, string fragmento)
    {
        Assert.Contains(fragmento, ParametroRetencionRules.ValidarAlta(Mutacion(caso)));
    }

    [Fact]
    public void Cambio_ValidaNotaYVigenciaFinal()
    {
        Assert.Null(ParametroRetencionRules.ValidarCambio(new CambiosParametroRetencion("Nota", true, Desde.AddDays(10)), Desde));
        Assert.Null(ParametroRetencionRules.ValidarCambio(new CambiosParametroRetencion(null, null, null), Desde));
        Assert.Equal(
            "La nota admite hasta 300 caracteres.",
            ParametroRetencionRules.ValidarCambio(new CambiosParametroRetencion(new string('n', 301), null, null), Desde));
        Assert.Equal(
            "La vigencia final no puede ser anterior a la inicial.",
            ParametroRetencionRules.ValidarCambio(new CambiosParametroRetencion(null, null, Desde.AddDays(-1)), Desde));
    }
}
