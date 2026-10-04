namespace DeltaERP.Domain.Rules;

public sealed record DocumentoPagable(int Id, string Numero, int TerceroId, string Estado, decimal SaldoPendiente);

public static class PagoRules
{
    public static string? ValidarSolicitud(IReadOnlyCollection<decimal> montosAplicados, string? metodoPago)
    {
        if (montosAplicados.Count == 0)
        {
            return "El pago debe aplicarse al menos a una factura.";
        }
        if (montosAplicados.Any(m => m <= 0))
        {
            return "El monto aplicado a cada factura debe ser mayor a cero.";
        }
        if (string.IsNullOrWhiteSpace(metodoPago))
        {
            return "El método de pago es obligatorio.";
        }
        return null;
    }

    // tercero: "cliente" | "proveedor", solo para los mensajes.
    public static string? ValidarAplicaciones(
        string tercero,
        int terceroId,
        IReadOnlyCollection<(int DocumentoId, decimal Monto)> aplicaciones,
        IReadOnlyCollection<DocumentoPagable> documentos)
    {
        var idsDocumento = aplicaciones.Select(a => a.DocumentoId).Distinct().ToList();

        var inexistentes = idsDocumento.Where(id => documentos.All(d => d.Id != id)).ToList();
        if (inexistentes.Count > 0)
        {
            return $"Las siguientes facturas no existen: {string.Join(", ", inexistentes)}.";
        }
        var deOtroTercero = documentos.Where(d => d.TerceroId != terceroId).Select(d => d.Numero).ToList();
        if (deOtroTercero.Count > 0)
        {
            return $"Las siguientes facturas no pertenecen al {tercero} indicado: {string.Join(", ", deOtroTercero)}.";
        }
        var noVigentes = documentos.Where(d => d.Estado != "Vigente").Select(d => d.Numero).ToList();
        if (noVigentes.Count > 0)
        {
            return $"Las siguientes facturas no están vigentes: {string.Join(", ", noVigentes)}.";
        }

        // RN-05: varias aplicaciones a la misma factura se suman antes de comparar
        // contra el saldo (los triggers de límite de pago revalidan en la BD como respaldo).
        var aplicadoPorDocumento = aplicaciones.GroupBy(a => a.DocumentoId).ToDictionary(g => g.Key, g => g.Sum(a => a.Monto));
        var excedidos = aplicadoPorDocumento
            .Select(kv => (Aplicado: kv.Value, Documento: documentos.First(d => d.Id == kv.Key)))
            .Where(x => x.Aplicado > x.Documento.SaldoPendiente)
            .Select(x => $"{x.Documento.Numero} (saldo: {x.Documento.SaldoPendiente})")
            .ToList();
        if (excedidos.Count > 0)
        {
            return $"RN-05: el monto aplicado excede el saldo pendiente de: {string.Join(", ", excedidos)}.";
        }
        return null;
    }
}
