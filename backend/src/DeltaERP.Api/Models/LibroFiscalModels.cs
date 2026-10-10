namespace DeltaERP.Api.Models;

public sealed record FilaLibroFiscal(
    DateOnly Fecha,
    string TipoDocumento,
    string? Serie,
    string? Numero,
    string NumeroInterno,
    string Nit,
    string Nombre,
    string Estado,
    bool Legado,
    decimal BaseBienes,
    decimal BaseServicios,
    decimal Exento,
    decimal Iva,
    decimal Total,
    string? ReferenciaSerie,
    string? ReferenciaNumero,
    Guid? ReferenciaUuid);

public sealed record TotalesLibroFiscal(decimal BaseBienes, decimal BaseServicios, decimal Exento, decimal Iva, decimal Total);

public sealed record ContribuyenteLibro(string? Nit, string? Nombre);

public sealed record LibroFiscalRespuesta(
    string Tipo,
    int Anio,
    int Mes,
    ContribuyenteLibro Contribuyente,
    IReadOnlyList<FilaLibroFiscal> Filas,
    TotalesLibroFiscal Totales,
    IReadOnlyList<string> Advertencias);
