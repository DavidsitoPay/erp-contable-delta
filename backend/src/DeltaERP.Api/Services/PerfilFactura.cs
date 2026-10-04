using DeltaERP.Domain.Rules;

namespace DeltaERP.Api.Services;

// Todo lo que distingue una factura de CxC de una de CxP. Sigla es "CxC" o "CxP";
// de ella se derivan el prefijo del asiento, la acción y la tabla de auditoría.
public sealed record PerfilFactura(
    string Sigla,
    string TipoTercero,
    string[] TiposDocumentoValidos,
    string TipoCuentaControl,
    string NaturalezaCuentaControl,
    string EjemploCuentaControl,
    LadoControl LadoControl);
