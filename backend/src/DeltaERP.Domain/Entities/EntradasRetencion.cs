using System.Text.Json.Serialization;

namespace DeltaERP.Domain.Entities;

public sealed record AjusteIsr(
    [property: JsonRequired] decimal Monto,
    string? Motivo);

public sealed record ConstanciaEntrada(
    [property: JsonRequired] string Numero,
    [property: JsonRequired] DateOnly Fecha);

public sealed record RetencionIvaEntrada(
    [property: JsonRequired] decimal Monto,
    bool ConfirmaClienteNoAgente,
    ConstanciaEntrada? Constancia);
