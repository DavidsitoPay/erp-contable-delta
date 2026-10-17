using System.Globalization;
using DeltaERP.Domain.Entities;

namespace DeltaERP.Domain.Rules;

public sealed record ParametroRetencionCalculo(
    int Id,
    string Codigo,
    decimal TasaBase,
    decimal? TramoLimite,
    decimal? TasaExcedente,
    decimal UmbralMinimo,
    string UmbralSobre);

public enum EstadoRetencionIsr
{
    Retenida,
    NoAplica,
    PendienteRevision,
}

public sealed record EvaluacionIsr(EstadoRetencionIsr Estado, string? Motivo, decimal Base, decimal Retencion);

public sealed record ErrorRegla(string Mensaje, bool EsConflicto);

public sealed record ResultadoAjusteIsr(decimal Monto, string? MotivoAjuste, string Revision, string? RevisionMotivo, ErrorRegla? Error);

public static class RetencionRules
{
    public const string EstadoPendiente = "PENDIENTE";
    public const string EstadoNoAplica = "NO_APLICA";
    public const string EstadoVencida = "VENCIDA";
    public const int LongitudMaximaMotivo = 300;

    public static string Quetzales(decimal valor) => $"Q{valor.ToString("N2", CultureInfo.InvariantCulture)}";

    public static ParametroRetencionCalculo ComoCalculo(ParametroRetencion parametro) =>
        new(parametro.Id, parametro.Codigo, parametro.TasaBase ?? 0m, parametro.TramoLimite, parametro.TasaExcedente,
            parametro.UmbralMinimo, parametro.UmbralSobre);

    public static decimal CalcularEscala(decimal baseSinIva, ParametroRetencionCalculo parametro)
    {
        var tasaBase = parametro.TasaBase / 100m;
        if (parametro.TramoLimite is not { } limite || parametro.TasaExcedente is not { } tasaExcedente)
        {
            return MotorFiscal.Redondear(baseSinIva * tasaBase);
        }
        var enTramo = Math.Min(baseSinIva, limite);
        var excedente = Math.Max(baseSinIva - limite, 0m);
        return MotorFiscal.Redondear(enTramo * tasaBase + excedente * tasaExcedente / 100m);
    }

    public static bool RequiereParametroIsr(Contraparte proveedor, string tipoDocumento) =>
        tipoDocumento == TiposDocumento.Factura
        && proveedor.EsResidente
        && proveedor.RegimenIva != Contraparte.RegimenIvaPequenoContribuyente
        && proveedor.RegimenIsr == Contraparte.RegimenIsrSimplificado;

    public static EvaluacionIsr EvaluarIsrProveedor(
        Contraparte proveedor, string tipoDocumento, decimal baseSinIva, ParametroRetencionCalculo? parametro)
    {
        var previa = EvaluarCondicionesPrevias(proveedor, tipoDocumento, baseSinIva);
        if (previa is not null)
        {
            return previa;
        }
        var calculo = parametro ?? throw new InvalidOperationException("Falta el parámetro de retención de ISR.");
        if (baseSinIva < calculo.UmbralMinimo)
        {
            return SinRetencion(
                EstadoRetencionIsr.NoAplica,
                $"La base sin IVA ({Quetzales(baseSinIva)}) es menor al umbral de {Quetzales(calculo.UmbralMinimo)}.",
                baseSinIva);
        }
        return new EvaluacionIsr(EstadoRetencionIsr.Retenida, null, baseSinIva, CalcularEscala(baseSinIva, calculo));
    }

    public static ResultadoAjusteIsr AplicarAjusteIsr(EvaluacionIsr evaluacion, AjusteIsr? ajuste, decimal totalFactura)
    {
        if (ajuste is null)
        {
            var revision = evaluacion.Estado == EstadoRetencionIsr.PendienteRevision ? DocumentoCxP.RevisionPendiente : DocumentoCxP.RevisionNinguna;
            var monto = evaluacion.Estado == EstadoRetencionIsr.Retenida ? evaluacion.Retencion : 0m;
            return new ResultadoAjusteIsr(monto, null, revision, null, null);
        }

        var error = ValidarAjuste(ajuste, totalFactura) ?? ValidarAjusteSegunEstado(evaluacion, ajuste);
        if (error is not null)
        {
            return new ResultadoAjusteIsr(0m, null, DocumentoCxP.RevisionNinguna, null, error);
        }
        var motivo = ajuste.Motivo?.Trim();
        if (evaluacion.Estado == EstadoRetencionIsr.PendienteRevision)
        {
            return new ResultadoAjusteIsr(ajuste.Monto, motivo, DocumentoCxP.RevisionResuelta, motivo, null);
        }
        var motivoAjuste = ajuste.Monto == evaluacion.Retencion ? null : motivo;
        return new ResultadoAjusteIsr(ajuste.Monto, motivoAjuste, DocumentoCxP.RevisionNinguna, null, null);
    }

    public static decimal SugerirIvaCliente(decimal ivaRetenible, decimal baseFactura, decimal totalFactura, ParametroRetencionCalculo parametro)
    {
        var montoUmbral = parametro.UmbralSobre == ParametroRetencion.UmbralBase ? baseFactura : totalFactura;
        return montoUmbral < parametro.UmbralMinimo ? 0m : MotorFiscal.Redondear(ivaRetenible * parametro.TasaBase / 100m);
    }

    public static string CodigoParametroIvaCliente(Contraparte cliente) =>
        cliente.EsAgenteRetencionIva && cliente.TipoAgenteIva == Contraparte.AgenteSectorPublico
            ? ParametroRetencion.CodigoIvaSectorPublico
            : ParametroRetencion.CodigoIvaAgentePrivado;

    public static ErrorRegla? ValidarIvaCliente(decimal monto, decimal montoAplicado, decimal ivaRetenible)
    {
        if (monto <= 0 || Math.Round(monto, 2) != monto)
        {
            return new ErrorRegla("El monto del IVA retenido debe ser mayor a cero y tener hasta 2 decimales.", false);
        }
        if (monto > ivaRetenible)
        {
            return new ErrorRegla(
                $"El IVA retenido ({Quetzales(monto)}) excede el IVA retenible de la factura ({Quetzales(ivaRetenible)}).", true);
        }
        return monto > montoAplicado
            ? new ErrorRegla($"El IVA retenido ({Quetzales(monto)}) excede el monto aplicado a la factura ({Quetzales(montoAplicado)}).", true)
            : null;
    }

    public static decimal TasaEfectiva(decimal baseRetencion, decimal monto) =>
        baseRetencion == 0m ? 0m : Math.Round(monto / baseRetencion * 100m, 4, MidpointRounding.AwayFromZero);

    public static string EstadoConstancia(
        string concepto, DateOnly fechaDocumento, DateOnly hoy, int? diaLimite, string? estadoConstanciaEmitida)
    {
        if (concepto == Retencion.ConceptoIsrNoResidente)
        {
            return EstadoNoAplica;
        }
        if (estadoConstanciaEmitida is not null)
        {
            return estadoConstanciaEmitida;
        }
        if (concepto != Retencion.ConceptoIsr || diaLimite is not { } dia)
        {
            return EstadoPendiente;
        }
        var limite = new DateOnly(fechaDocumento.Year, fechaDocumento.Month, 1).AddMonths(1).AddDays(dia - 1);
        return hoy > limite ? EstadoVencida : EstadoPendiente;
    }

    public static IReadOnlyList<string> AdvertenciasConstancia(string concepto, DateOnly fechaDocumento, DateOnly fechaConstancia) =>
        concepto == Retencion.ConceptoIsr && fechaConstancia != fechaDocumento
            ? [$"La constancia de ISR debe llevar la fecha de la factura ({VigenciaRules.Iso(fechaDocumento)}); la fecha indicada es {VigenciaRules.Iso(fechaConstancia)}."]
            : [];

    private static EvaluacionIsr? EvaluarCondicionesPrevias(Contraparte proveedor, string tipoDocumento, decimal baseSinIva)
    {
        if (tipoDocumento != TiposDocumento.Factura)
        {
            return SinRetencion(EstadoRetencionIsr.NoAplica, "Las notas de crédito y de débito no generan retención de ISR.", baseSinIva);
        }
        if (!proveedor.EsResidente)
        {
            if (proveedor.RegimenIsr == Contraparte.RegimenIsrExento)
            {
                return SinRetencion(
                    EstadoRetencionIsr.NoAplica, $"El proveedor tiene renta exenta (fundamento: {proveedor.IsrFundamento}).", baseSinIva);
            }
            return string.IsNullOrEmpty(proveedor.TipoRentaNrCodigo)
                ? SinRetencion(
                    EstadoRetencionIsr.PendienteRevision,
                    $"El proveedor {proveedor.Nombre} es no residente y no tiene tipo de renta definido; no se puede determinar la retención al pago.",
                    baseSinIva)
                : SinRetencion(EstadoRetencionIsr.NoAplica, "El proveedor es no residente: el ISR se retiene al registrar el pago.", baseSinIva);
        }
        if (proveedor.RegimenIva == Contraparte.RegimenIvaPequenoContribuyente)
        {
            return SinRetencion(EstadoRetencionIsr.NoAplica, "El proveedor es pequeño contribuyente: no se retiene ISR.", baseSinIva);
        }
        return proveedor.RegimenIsr switch
        {
            Contraparte.RegimenIsrSimplificado => null,
            Contraparte.RegimenIsrUtilidades => SinRetencion(
                EstadoRetencionIsr.NoAplica, "El proveedor tributa en el régimen sobre utilidades de actividades lucrativas.", baseSinIva),
            Contraparte.RegimenIsrPagoDirecto => SinRetencion(
                EstadoRetencionIsr.NoAplica, $"El proveedor paga el impuesto directamente (fundamento: {proveedor.IsrFundamento}).", baseSinIva),
            Contraparte.RegimenIsrExento => SinRetencion(
                EstadoRetencionIsr.NoAplica, $"El proveedor tiene renta exenta (fundamento: {proveedor.IsrFundamento}).", baseSinIva),
            _ => SinRetencion(
                EstadoRetencionIsr.PendienteRevision, $"El proveedor {proveedor.Nombre} no tiene régimen de ISR definido.", baseSinIva),
        };
    }

    private static EvaluacionIsr SinRetencion(EstadoRetencionIsr estado, string motivo, decimal baseSinIva) =>
        new(estado, motivo, baseSinIva, 0m);

    private static ErrorRegla? ValidarAjuste(AjusteIsr ajuste, decimal totalFactura)
    {
        if (string.IsNullOrWhiteSpace(ajuste.Motivo))
        {
            return new ErrorRegla("El ajuste de la retención de ISR exige un motivo.", false);
        }
        if (ajuste.Motivo.Trim().Length > LongitudMaximaMotivo)
        {
            return new ErrorRegla("El motivo del ajuste admite hasta 300 caracteres.", false);
        }
        if (ajuste.Monto < 0 || Math.Round(ajuste.Monto, 2) != ajuste.Monto)
        {
            return new ErrorRegla("El monto de la retención ajustada debe ser mayor o igual a cero y tener hasta 2 decimales.", false);
        }
        return ajuste.Monto >= totalFactura
            ? new ErrorRegla("La retención ajustada debe ser menor al total de la factura.", false)
            : null;
    }

    private static ErrorRegla? ValidarAjusteSegunEstado(EvaluacionIsr evaluacion, AjusteIsr ajuste)
    {
        if (evaluacion.Estado != EstadoRetencionIsr.NoAplica)
        {
            return null;
        }
        return ajuste.Monto == 0m
            ? new ErrorRegla("La factura no tiene retención de ISR que ajustar.", false)
            : new ErrorRegla($"No se puede imponer una retención de ISR manual: {evaluacion.Motivo}", true);
    }
}
