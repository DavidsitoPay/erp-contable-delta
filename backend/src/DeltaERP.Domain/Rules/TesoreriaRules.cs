using DeltaERP.Domain.Entities;

namespace DeltaERP.Domain.Rules;

public static class TesoreriaRules
{
    private const decimal MontoMaximo = 999999999999.99m;

    public static string? ValidarMonto(decimal monto)
    {
        if (monto <= 0)
        {
            return "El monto debe ser mayor a cero.";
        }
        return ValidarMagnitud(monto, "El monto no puede tener más de 2 decimales.", "El monto excede el máximo permitido.");
    }

    public static string? ValidarMovimiento(string? tipo, decimal monto, string? descripcion)
    {
        if (tipo is not (MovimientoTesoreria.TipoIngreso or MovimientoTesoreria.TipoEgreso))
        {
            return "El tipo debe ser Ingreso o Egreso.";
        }
        return ValidarMonto(monto) ?? ValidarDescripcion(descripcion);
    }

    public static string? ValidarTransferencia(int origenId, int destinoId, decimal monto, string? descripcion)
    {
        if (origenId == destinoId)
        {
            return "La cuenta de origen y la de destino deben ser distintas.";
        }
        return ValidarMonto(monto) ?? ValidarDescripcion(descripcion);
    }

    public static string? ValidarTransferenciaCruzada(decimal monto, decimal? montoDestino, bool mismaMoneda)
    {
        if (mismaMoneda)
        {
            return montoDestino is null || montoDestino == monto
                ? null
                : "En una transferencia entre cuentas de la misma moneda el monto destino debe ser igual al monto.";
        }
        return montoDestino is null
            ? "El monto destino es obligatorio cuando las cuentas son de distinta moneda."
            : ValidarMonto(montoDestino.Value);
    }

    public static string? ValidarCuentaBancaria(string? banco, string? numero, string? tipo)
    {
        if (string.IsNullOrWhiteSpace(banco))
        {
            return "El banco es obligatorio.";
        }
        if (banco.Length > 100)
        {
            return "El banco no puede exceder 100 caracteres.";
        }
        if (string.IsNullOrWhiteSpace(numero))
        {
            return "El número de cuenta es obligatorio.";
        }
        if (numero.Length > 50)
        {
            return "El número de cuenta no puede exceder 50 caracteres.";
        }
        return tipo is not null && CuentaBancaria.TiposValidos.Contains(tipo) ? null : "El tipo debe ser Monetaria o Ahorro.";
    }

    public static string? ValidarApertura(decimal saldo, DateOnly? fecha, int? contrapartidaId)
    {
        if (saldo < 0)
        {
            return "El saldo de apertura no puede ser negativo.";
        }
        var errorMagnitud = ValidarMagnitud(saldo, "El saldo de apertura no puede tener más de 2 decimales.", "El saldo de apertura excede el máximo permitido.");
        if (errorMagnitud is not null)
        {
            return errorMagnitud;
        }
        if (saldo > 0 && fecha is null)
        {
            return "La fecha de apertura es obligatoria cuando hay saldo de apertura.";
        }
        if (saldo > 0 && contrapartidaId is null)
        {
            return "La cuenta de contrapartida de la apertura es obligatoria cuando hay saldo de apertura.";
        }
        return null;
    }

    public static string? ValidarSaldoExtracto(decimal saldo) =>
        ValidarMagnitud(saldo, "El saldo del extracto no puede tener más de 2 decimales.", "El saldo del extracto excede el máximo permitido.");

    public static string? ValidarEscalaPago(IEnumerable<decimal> montos) =>
        montos.Any(m => Math.Round(m, 2) != m) ? "Los montos del pago no pueden tener más de 2 decimales." : null;

    public static string? ValidarNetoPositivo(decimal totalAplicado, decimal retenido) =>
        retenido > 0 && totalAplicado - retenido <= 0
            ? "El neto (total aplicado menos retenciones) debe ser mayor a cero."
            : null;

    public static string? ValidarMarca(ConciliacionBancaria conciliacion, MovimientoTesoreria? movimiento)
    {
        if (movimiento is null)
        {
            return "El movimiento indicado no existe.";
        }
        if (movimiento.Origen == MovimientoTesoreria.OrigenApertura)
        {
            return "El movimiento de apertura no es conciliable: ya forma parte del saldo inicial.";
        }
        if (movimiento.CuentaBancariaId != conciliacion.CuentaBancariaId)
        {
            return "El movimiento no pertenece a la cuenta bancaria de la conciliación.";
        }
        return movimiento.Fecha > conciliacion.Fecha ? "El movimiento es posterior a la fecha de corte de la conciliación." : null;
    }

    public static List<(int CuentaId, decimal Monto)> AgruparPorCuentaControl(
        IReadOnlyCollection<(int DocumentoId, decimal Monto)> aplicaciones,
        IReadOnlyDictionary<int, int> controlPorDocumento) =>
        aplicaciones
            .GroupBy(a => controlPorDocumento[a.DocumentoId], a => a.Monto)
            .OrderBy(g => g.Key)
            .Select(g => (CuentaId: g.Key, Monto: g.Sum()))
            .ToList();

    public static string? ValidarMonedaCuenta(string? codigo)
    {
        var normalizado = codigo?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalizado) || normalizado is "GTQ" or "USD")
        {
            return null;
        }
        return normalizado == "EUR"
            ? "Las cuentas bancarias en EUR no están disponibles en esta versión."
            : "La moneda de la cuenta bancaria debe ser GTQ o USD.";
    }

    private static string? ValidarMagnitud(decimal valor, string mensajeDecimales, string mensajeExcede)
    {
        if (Math.Round(valor, 2) != valor)
        {
            return mensajeDecimales;
        }
        return Math.Abs(valor) > MontoMaximo ? mensajeExcede : null;
    }

    private static string? ValidarDescripcion(string? descripcion)
    {
        if (string.IsNullOrWhiteSpace(descripcion))
        {
            return "La descripción es obligatoria.";
        }
        return descripcion.Length > 255 ? "La descripción no puede exceder 255 caracteres." : null;
    }
}
