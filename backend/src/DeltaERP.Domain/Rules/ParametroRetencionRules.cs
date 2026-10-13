using DeltaERP.Domain.Entities;

namespace DeltaERP.Domain.Rules;

public sealed record DatosParametroRetencion(
    string? Codigo,
    string? Nombre,
    string? Concepto,
    decimal? TasaBase,
    decimal? TramoLimite,
    decimal? TasaExcedente,
    decimal? UmbralMinimo,
    string? UmbralSobre,
    short? DiaLimite,
    string? ArticuloLegal,
    string? Nota,
    DateOnly? VigenteDesde,
    DateOnly? VigenteHasta,
    bool? Activo);

public sealed record CambiosParametroRetencion(
    string? Nota,
    bool? Activo,
    DateOnly? VigenteHasta);

public static class ParametroRetencionRules
{
    private const int LongitudMinimaCodigo = 3;
    private const int LongitudMaximaCodigo = 40;
    private const int LongitudMaximaNombre = 120;
    private const int LongitudMaximaArticulo = 200;
    private const int LongitudMaximaNota = 300;
    private const int DiaLimiteMaximo = 28;
    private const decimal TasaMaxima = 100m;
    private const decimal MontoMaximo = 999999999999.99m;

    public static string? ValidarAlta(DatosParametroRetencion datos) =>
        ValidarTextos(datos) ?? ValidarForma(datos) ?? ValidarTramo(datos) ?? ValidarUmbral(datos)
        ?? VigenciaRules.Validar(datos.VigenteDesde, datos.VigenteHasta);

    public static string? ValidarCambio(CambiosParametroRetencion cambios, DateOnly vigenteDesde) =>
        cambios.Nota?.Length > LongitudMaximaNota
            ? "La nota admite hasta 300 caracteres."
            : VigenciaRules.Validar(vigenteDesde, cambios.VigenteHasta);

    private static string? ValidarTextos(DatosParametroRetencion datos)
    {
        var codigo = datos.Codigo ?? string.Empty;
        if (codigo.Length is < LongitudMinimaCodigo or > LongitudMaximaCodigo
            || !codigo.All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c == '_'))
        {
            return "El código debe tener entre 3 y 40 caracteres: mayúsculas, dígitos o guion bajo.";
        }
        if (string.IsNullOrWhiteSpace(datos.Nombre) || string.IsNullOrWhiteSpace(datos.ArticuloLegal))
        {
            return "El nombre y la referencia legal son obligatorios.";
        }
        if (datos.Nombre.Length > LongitudMaximaNombre || datos.ArticuloLegal.Length > LongitudMaximaArticulo)
        {
            return "El nombre admite hasta 120 caracteres y la referencia legal hasta 200.";
        }
        return datos.Nota?.Length > LongitudMaximaNota ? "La nota admite hasta 300 caracteres." : null;
    }

    private static string? ValidarForma(DatosParametroRetencion datos)
    {
        if (datos.Concepto is null || !ParametroRetencion.ConceptosValidos.Contains(datos.Concepto))
        {
            return $"Concepto inválido. Debe ser uno de: {string.Join(", ", ParametroRetencion.ConceptosValidos)}.";
        }
        if (datos.Concepto == ParametroRetencion.ConceptoPlazoConstancia)
        {
            if (datos.DiaLimite is < 1 or > DiaLimiteMaximo or null)
            {
                return "El día límite debe estar entre 1 y 28.";
            }
            return datos.TasaBase is not null || datos.TramoLimite is not null || datos.TasaExcedente is not null
                ? "Un plazo de constancia no lleva tasas ni tramo."
                : null;
        }
        if (datos.DiaLimite is not null)
        {
            return "Solo un plazo de constancia lleva día límite.";
        }
        return datos.TasaBase is not > 0 or > TasaMaxima || !TieneEscala(datos.TasaBase.Value, 4)
            ? "La tasa debe ser mayor que 0 y hasta 100, con hasta 4 decimales."
            : null;
    }

    private static string? ValidarTramo(DatosParametroRetencion datos)
    {
        if (datos.TramoLimite is null && datos.TasaExcedente is null)
        {
            return null;
        }
        if (datos.TramoLimite is not > 0 or > MontoMaximo || datos.TasaExcedente is not > 0 or > TasaMaxima)
        {
            return "El tramo exige un límite mayor que 0 y una tasa del excedente mayor que 0 y hasta 100.";
        }
        if (datos.Concepto != ParametroRetencion.ConceptoIsrProveedor)
        {
            return "Solo la retención de ISR a proveedores admite tramo.";
        }
        return TieneEscala(datos.TramoLimite.Value, 2) && TieneEscala(datos.TasaExcedente.Value, 4)
            ? null
            : "El límite del tramo admite 2 decimales y la tasa del excedente 4.";
    }

    private static string? ValidarUmbral(DatosParametroRetencion datos)
    {
        if (datos.UmbralSobre is not null && !ParametroRetencion.UmbralesValidos.Contains(datos.UmbralSobre))
        {
            return $"El umbral debe aplicarse sobre uno de: {string.Join(", ", ParametroRetencion.UmbralesValidos)}.";
        }
        var umbral = datos.UmbralMinimo ?? 0m;
        return umbral < 0 || umbral > MontoMaximo || !TieneEscala(umbral, 2)
            ? "El umbral mínimo debe ser mayor o igual a cero y tener hasta 2 decimales."
            : null;
    }

    private static bool TieneEscala(decimal valor, int decimales) => decimal.Round(valor, decimales) == valor;
}
