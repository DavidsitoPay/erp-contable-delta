using DeltaERP.Domain.Entities;

namespace DeltaERP.Api.Services;

public sealed record VinculosAsiento(bool CxC, bool CxP, bool Tesoreria);

public static class ReversaAsiento
{
    public const int MotivoMaximo = 250;
    public const string MotivoObligatorio = "El motivo de la reversa es obligatorio.";
    public const string MotivoDemasiadoLargo = "El motivo no puede superar 250 caracteres.";

    // RN-03
    public static string? ObtenerBloqueo(AsientoContable asiento, PeriodoContable periodo, VinculosAsiento vinculos)
    {
        if (asiento.Estado != AsientoContable.EstadoConfirmado)
        {
            return $"El asiento {asiento.Id} no está Confirmado (estado: {asiento.Estado}); no se puede reversar.";
        }
        if (asiento.ReversaDeId is not null)
        {
            return $"El asiento {asiento.Id} es una reversa; no se puede reversar.";
        }
        if (periodo.Estado != PeriodoContable.EstadoAbierto)
        {
            return $"El asiento {asiento.Id} pertenece al periodo {periodo.Nombre} en estado '{periodo.Estado}'; solo se puede reversar un asiento de un periodo Abierto.";
        }
        if (vinculos.CxC)
        {
            return $"El asiento {asiento.Id} está vinculado a un documento de cuentas por cobrar; no se puede reversar.";
        }
        if (vinculos.CxP)
        {
            return $"El asiento {asiento.Id} está vinculado a un documento de cuentas por pagar; no se puede reversar.";
        }
        if (vinculos.Tesoreria)
        {
            return $"El asiento {asiento.Id} está vinculado a un movimiento de tesorería (cobros, pagos, transferencias o apertura); reversarlo desincronizaría los libros contables del banco.";
        }
        return null;
    }
}
