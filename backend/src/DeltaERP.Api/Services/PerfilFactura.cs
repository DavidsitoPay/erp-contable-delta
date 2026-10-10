using DeltaERP.Domain.Rules;

namespace DeltaERP.Api.Services;

public sealed record PerfilFactura(
    string Sigla,
    string TipoTercero,
    string[] TiposDocumentoValidos,
    string TipoCuentaControl,
    string NaturalezaCuentaControl,
    string EjemploCuentaControl,
    LadoControl LadoControl,
    string AplicaImpuestoA,
    string EtiquetaCuentaIva,
    bool DteObligatorio)
{
    public bool EsVenta => LadoControl == LadoControl.Debito;

    public string ClaveTercero => EsVenta ? "clienteId" : "proveedorId";
}
