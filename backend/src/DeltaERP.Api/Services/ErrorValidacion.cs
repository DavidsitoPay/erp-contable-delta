namespace DeltaERP.Api.Services;

public sealed record ErrorValidacion(int Estado, string Mensaje)
{
    public static ErrorValidacion Solicitud(string mensaje) => new(StatusCodes.Status400BadRequest, mensaje);

    public static ErrorValidacion Conflicto(string mensaje) => new(StatusCodes.Status409Conflict, mensaje);

    public static ErrorValidacion? SolicitudSi(string? mensaje) => mensaje is null ? null : Solicitud(mensaje);
}
