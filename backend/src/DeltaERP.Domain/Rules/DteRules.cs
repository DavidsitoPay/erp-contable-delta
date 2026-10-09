namespace DeltaERP.Domain.Rules;

public sealed record DatosDte(Guid? Uuid, string? Serie, string? Numero, DateTimeOffset? FechaCertificacion);

public static class DteRules
{
    private const int LongitudMaxima = 20;
    private static readonly TimeSpan ToleranciaFutura = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan OffsetGuatemala = TimeSpan.FromHours(-6);

    public static string? Validar(DatosDte dte, bool obligatorio, bool exigeSoporteCredito, DateOnly fechaDocumento, DateTimeOffset ahora)
    {
        var tieneTrio = TieneTrio(dte);
        if (obligatorio && !(tieneTrio && dte.FechaCertificacion is not null))
        {
            return "Las facturas de cuentas por cobrar requieren los datos del DTE: UUID, serie, número y fecha de certificación.";
        }
        if (!obligatorio && TieneAlguno(dte) && !tieneTrio)
        {
            return "Los datos del DTE del proveedor deben indicar UUID, serie y número juntos.";
        }
        if (exigeSoporteCredito && !tieneTrio)
        {
            return "Para registrar crédito fiscal se requieren el UUID, la serie y el número del DTE del proveedor.";
        }
        if (dte.Serie?.Trim().Length > LongitudMaxima || dte.Numero?.Trim().Length > LongitudMaxima)
        {
            return "La serie y el número del DTE deben tener entre 1 y 20 caracteres.";
        }
        return ValidarFechaCertificacion(dte.FechaCertificacion, fechaDocumento, ahora);
    }

    public static (Guid? Uuid, string? Error) InterpretarUuid(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return (null, null);
        }
        return Guid.TryParseExact(texto.Trim(), "D", out var uuid)
            ? (uuid, null)
            : (null, "El UUID del DTE debe tener formato canónico de 36 caracteres.");
    }

    private static bool TieneTrio(DatosDte dte) =>
        dte.Uuid is { } uuid && uuid != Guid.Empty
        && !string.IsNullOrWhiteSpace(dte.Serie)
        && !string.IsNullOrWhiteSpace(dte.Numero);

    private static bool TieneAlguno(DatosDte dte) =>
        (dte.Uuid is { } uuid && uuid != Guid.Empty)
        || !string.IsNullOrWhiteSpace(dte.Serie)
        || !string.IsNullOrWhiteSpace(dte.Numero);

    private static string? ValidarFechaCertificacion(DateTimeOffset? certificacion, DateOnly fechaDocumento, DateTimeOffset ahora)
    {
        if (certificacion is not { } fecha)
        {
            return null;
        }
        if (fecha > ahora + ToleranciaFutura)
        {
            return "La fecha de certificación del DTE no puede ser futura.";
        }
        var inicioDelDia = new DateTimeOffset(fechaDocumento.ToDateTime(TimeOnly.MinValue), OffsetGuatemala);
        return fecha < inicioDelDia ? "La fecha de certificación no puede ser anterior a la fecha del documento." : null;
    }
}
