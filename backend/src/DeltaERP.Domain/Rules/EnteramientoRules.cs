using System.Globalization;
using DeltaERP.Domain.Entities;

namespace DeltaERP.Domain.Rules;

public static class EnteramientoRules
{
    public const int LongitudMaximaBoleta = 40;
    public const int LongitudMaximaReferencia = 100;

    public static decimal Saldo(decimal retenido, decimal enterado) => retenido - enterado;

    public static string? ValidarPeriodo(DateOnly periodo) =>
        periodo == default || periodo.Day != 1 ? "El período es obligatorio y debe ser el primer día del mes (YYYY-MM-01)." : null;

    public static string? ValidarMonto(decimal monto) =>
        monto <= 0m || decimal.Round(monto, 2) != monto ? "El monto debe ser mayor que cero y tener a lo más dos decimales." : null;

    // RN-32
    public static string? ValidarSolicitud(string? formulario, DateOnly periodo, DateOnly fechaPago, DateOnly hoy, string? boleta, decimal monto)
    {
        var errorPeriodo = ValidarPeriodo(periodo);
        if (errorPeriodo is not null)
        {
            return errorPeriodo;
        }
        if (formulario is null || !Enteramiento.FormulariosValidos.Contains(formulario))
        {
            return $"Formulario inválido. Debe ser uno de: {string.Join(", ", Enteramiento.FormulariosValidos)}.";
        }
        if (fechaPago < periodo)
        {
            return "La fecha de pago no puede ser anterior al período enterado.";
        }
        if (fechaPago > hoy)
        {
            return "La fecha de pago no puede ser posterior a hoy.";
        }
        if (string.IsNullOrWhiteSpace(boleta) || boleta.Trim().Length > LongitudMaximaBoleta)
        {
            return "El número de boleta es obligatorio y admite hasta 40 caracteres.";
        }
        return ValidarMonto(monto);
    }

    // RN-32
    public static string? ValidarSaldo(decimal monto, decimal saldo)
    {
        if (saldo <= 0m)
        {
            return "El período no tiene saldo por enterar.";
        }
        return monto > saldo
            ? string.Create(CultureInfo.InvariantCulture, $"El monto excede el saldo por enterar del período (Q{saldo:N2}).")
            : null;
    }

    public static string? ValidarMotivo(string? motivo) =>
        string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length > CambioRules.LongitudMaximaMotivo
            ? "El motivo es obligatorio y no puede exceder 300 caracteres."
            : null;

    public static string? ValidarReferencia(string? referencia) =>
        string.IsNullOrWhiteSpace(referencia) || referencia.Trim().Length > LongitudMaximaReferencia
            ? "La referencia es obligatoria y no puede exceder 100 caracteres."
            : null;

    public static string Descripcion(string formulario, string boleta, DateOnly periodo) =>
        $"Enteramiento ISR no residentes {formulario} boleta {boleta} período {periodo.ToString("yyyy-MM", CultureInfo.InvariantCulture)}";
}
