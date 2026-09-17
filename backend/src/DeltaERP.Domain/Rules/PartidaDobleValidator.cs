using DeltaERP.Domain.Entities;

namespace DeltaERP.Domain.Rules;

/// <summary>
/// RN-01: todo asiento contable debe cumplir partida doble
/// (suma de débitos = suma de créditos) antes de poder registrarse.
/// Ver docs/business-rules.md.
/// </summary>
public static class PartidaDobleValidator
{
    public static bool EsValido(AsientoContable asiento, out string? mensajeError)
    {
        if (asiento.Lineas is null || asiento.Lineas.Count < 2)
        {
            mensajeError = "Un asiento contable debe tener al menos dos líneas.";
            return false;
        }

        var totalDebito = asiento.Lineas.Sum(l => l.Debito);
        var totalCredito = asiento.Lineas.Sum(l => l.Credito);

        if (totalDebito != totalCredito)
        {
            mensajeError =
                $"El asiento no cumple partida doble: débitos {totalDebito:F2} " +
                $"!= créditos {totalCredito:F2}.";
            return false;
        }

        mensajeError = null;
        return true;
    }
}
