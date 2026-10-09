using System.Globalization;
using DeltaERP.Domain.Entities;

namespace DeltaERP.Domain.Rules;

public sealed record DatosImpuesto(
    string? Codigo,
    string? Nombre,
    string? Tipo,
    decimal Tasa,
    string? AplicaA,
    bool? GeneraCredito,
    string? ArticuloLegal,
    DateOnly? VigenteDesde,
    DateOnly? VigenteHasta,
    bool? Activo,
    string? Nota);

public static class ImpuestoRules
{
    private const int LongitudMinimaCodigo = 3;
    private const int LongitudMaximaCodigo = 30;
    private const int LongitudMaximaNombre = 100;
    private const int LongitudMaximaArticulo = 200;
    private const decimal TasaMaxima = 100m;
    private const int DecimalesTasa = 4;

    public static bool EstaVigente(DateOnly desde, DateOnly? hasta, DateOnly fecha) =>
        fecha >= desde && (hasta is null || fecha <= hasta);

    public static bool ResolverGeneraCredito(DatosImpuesto datos) =>
        datos.GeneraCredito ?? datos.Tipo == Impuesto.TipoIvaGeneral;

    public static string? ValidarAlta(DatosImpuesto datos) =>
        ValidarTextos(datos) ?? ValidarTipoYTasa(datos) ?? ValidarCreditoYAmbito(datos) ?? ValidarVigencia(datos);

    public static string? ValidarParaFactura(Impuesto impuesto, string aplicaImpuestoA, string siglaFactura, DateOnly fecha)
    {
        if (!impuesto.Activo || impuesto.Tipo == Impuesto.TipoLegado)
        {
            return $"El impuesto '{impuesto.Nombre}' está inactivo y no puede usarse en documentos nuevos.";
        }
        if (!EstaVigente(impuesto.VigenteDesde, impuesto.VigenteHasta, fecha))
        {
            var hasta = impuesto.VigenteHasta is { } fin ? Iso(fin) : "sin fecha final";
            return $"El impuesto '{impuesto.Nombre}' no está vigente el {Iso(fecha)} (vigencia: {Iso(impuesto.VigenteDesde)} a {hasta}).";
        }
        if (impuesto.AplicaA != Impuesto.AmbitoAmbos && impuesto.AplicaA != aplicaImpuestoA)
        {
            return $"El impuesto '{impuesto.Nombre}' solo aplica a {impuesto.AplicaA.ToLowerInvariant()} y no puede usarse en una factura de {siglaFactura}.";
        }
        return null;
    }

    public static string? ValidarConsistenciaRegimen(IEnumerable<Impuesto> impuestos, Contraparte tercero)
    {
        var lista = impuestos.ToList();
        if (tercero.RegimenIva == Contraparte.RegimenIvaExento
            && lista.Any(i => i.Tipo is not (Impuesto.TipoExento or Impuesto.TipoNoAfecto)))
        {
            return $"El {tercero.Tipo.ToLowerInvariant()} está registrado como exento de IVA: solo puede usar impuestos exentos o no afectos.";
        }
        if (tercero.Tipo != "Proveedor")
        {
            return null;
        }
        var esPequeno = tercero.RegimenIva == Contraparte.RegimenIvaPequenoContribuyente;
        if (!esPequeno && lista.Any(i => i.Tipo == Impuesto.TipoPequenoContribuyente))
        {
            return "El impuesto 'Pequeño contribuyente' solo puede usarse con proveedores inscritos como pequeño contribuyente.";
        }
        if (esPequeno && lista.Any(i => i.Tipo == Impuesto.TipoIvaGeneral))
        {
            return "El proveedor es pequeño contribuyente: sus facturas no discriminan IVA ni generan crédito fiscal. Use el impuesto de pequeño contribuyente.";
        }
        return null;
    }

    public static ParametroImpuesto ComoParametro(Impuesto impuesto)
    {
        var tipo = impuesto.Tipo switch
        {
            Impuesto.TipoIvaGeneral => TipoImpuesto.IvaGeneral,
            Impuesto.TipoExento => TipoImpuesto.Exento,
            Impuesto.TipoNoAfecto => TipoImpuesto.NoAfecto,
            Impuesto.TipoPequenoContribuyente => TipoImpuesto.PequenoContribuyente,
            _ => TipoImpuesto.Legado,
        };
        return new ParametroImpuesto(impuesto.Id, tipo, impuesto.Tasa, impuesto.GeneraCredito);
    }

    public static string Iso(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string? ValidarTextos(DatosImpuesto datos)
    {
        var codigo = datos.Codigo ?? string.Empty;
        if (codigo.Length is < LongitudMinimaCodigo or > LongitudMaximaCodigo
            || !codigo.All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c == '_'))
        {
            return "El código debe tener entre 3 y 30 caracteres: mayúsculas, dígitos o guion bajo.";
        }
        if (string.IsNullOrWhiteSpace(datos.Nombre))
        {
            return "El nombre es obligatorio.";
        }
        if (string.IsNullOrWhiteSpace(datos.ArticuloLegal))
        {
            return "La referencia legal es obligatoria.";
        }
        return datos.Nombre.Length > LongitudMaximaNombre || datos.ArticuloLegal.Length > LongitudMaximaArticulo
            ? "El nombre admite hasta 100 caracteres y la referencia legal hasta 200."
            : null;
    }

    private static string? ValidarTipoYTasa(DatosImpuesto datos)
    {
        if (datos.Tipo is null || !Impuesto.TiposCreables.Contains(datos.Tipo))
        {
            return $"Tipo de impuesto inválido. Debe ser uno de: {string.Join(", ", Impuesto.TiposCreables)}.";
        }
        if (datos.Tasa is < 0 or > TasaMaxima || decimal.Round(datos.Tasa, DecimalesTasa) != datos.Tasa)
        {
            return "La tasa debe estar entre 0 y 100 y tener hasta 4 decimales.";
        }
        var sinTasa = datos.Tipo is Impuesto.TipoExento or Impuesto.TipoNoAfecto;
        if (sinTasa && datos.Tasa != 0)
        {
            return "Un impuesto exento o no afecto debe tener tasa 0.";
        }
        return !sinTasa && datos.Tasa == 0 ? "La tasa debe ser mayor que 0." : null;
    }

    private static string? ValidarCreditoYAmbito(DatosImpuesto datos)
    {
        if (ResolverGeneraCredito(datos) && datos.Tipo != Impuesto.TipoIvaGeneral)
        {
            return "Solo el IVA general puede generar crédito fiscal.";
        }
        if (datos.Tipo == Impuesto.TipoPequenoContribuyente && datos.AplicaA != Impuesto.AmbitoCompras)
        {
            return "El pequeño contribuyente solo aplica a compras.";
        }
        return datos.AplicaA is null || !Impuesto.AmbitosValidos.Contains(datos.AplicaA)
            ? $"Ámbito inválido. Debe ser uno de: {string.Join(", ", Impuesto.AmbitosValidos)}."
            : null;
    }

    private static string? ValidarVigencia(DatosImpuesto datos)
    {
        if (datos.VigenteDesde is null)
        {
            return "La vigencia inicial es obligatoria.";
        }
        return datos.VigenteHasta < datos.VigenteDesde ? "La vigencia final no puede ser anterior a la inicial." : null;
    }
}
