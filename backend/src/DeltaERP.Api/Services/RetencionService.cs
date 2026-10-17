using DeltaERP.Api.Models;
using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;
using DeltaERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Api.Services;

public sealed record EntradaIsrNr(int DocumentoId, decimal Monto, decimal? ValorEfectivoGtq, string? TipoRentaCodigo);

public sealed record LineaIsrNr(
    int DocumentoId, DateOnly FechaDocumento, ParametroRetencion Parametro, int MonedaId, string MonedaCodigo, decimal MontoOrigen,
    decimal TipoCambio, DateOnly TipoCambioFecha, decimal BaseGtq, decimal RetencionGtq, decimal RetencionOrigen, decimal ValorGtq, int Indice);

public sealed record PreparacionIsrNr(bool Aplica, string? MotivoNoAplica, IReadOnlyList<LineaIsrNr> Lineas, int? CuentaId, IReadOnlyList<string> Advertencias)
{
    public static PreparacionIsrNr NoAplica(string motivo) => new(false, motivo, [], null, []);

    public decimal TotalRetencionGtq => Lineas.Sum(l => l.RetencionGtq);

    public decimal TotalRetencionOrigen => Lineas.Sum(l => l.RetencionOrigen);

    public IReadOnlyList<(int CuentaId, decimal Monto)> ParaAsiento() =>
        CuentaId is { } cuenta && Lineas.Count > 0 ? [(cuenta, TotalRetencionGtq)] : [];
}

public sealed record RetencionIsrPreparada(
    EstadoRetencionIsr Estado,
    string? Motivo,
    decimal Base,
    decimal Tasa,
    decimal MontoCalculado,
    decimal Monto,
    ParametroRetencion? Parametro,
    int? CuentaId,
    string? MotivoAjuste,
    string Revision,
    string? RevisionMotivo,
    IReadOnlyList<string> Advertencias)
{
    public static readonly RetencionIsrPreparada Vacia = new(
        EstadoRetencionIsr.NoAplica, null, 0m, 0m, 0m, 0m, null, null, null, DocumentoCxP.RevisionNinguna, null, []);

    public RetencionAsiento? ParaAsiento => Monto > 0 && CuentaId is { } cuenta ? new RetencionAsiento(cuenta, Monto) : null;

    public RetencionIsrRespuesta ComoRespuesta(int? retencionId, decimal montoTotal) => new(
        Estado.ToString(), Motivo ?? MotivoAjuste, Base, Tasa, Monto, montoTotal - Monto, retencionId);
}

public sealed record RetencionIvaPreparada(
    AplicacionPagoCliente Aplicacion,
    DocumentoCxC Documento,
    ParametroRetencion Parametro,
    decimal Base,
    decimal Sugerido,
    decimal Monto,
    ConstanciaEntrada? Constancia);

public sealed record PreparacionIva(IReadOnlyList<RetencionIvaPreparada> Retenciones, int? CuentaId, IReadOnlyList<string> Advertencias)
{
    public static readonly PreparacionIva Vacia = new([], null, []);

    public IReadOnlyList<(int CuentaId, decimal Monto)> ParaAsiento() =>
        CuentaId is { } cuenta ? Retenciones.Select(r => (CuentaId: cuenta, r.Monto)).ToList() : [];
}

public class RetencionService
{
    public const string AvisoAgenteIva = "La retención de IVA como agente de retención no está disponible en esta versión; no se calculó.";
    public const string MensajeIvaYaRetenido = "La factura ya tiene IVA retenido registrado.";
    private const string MensajeClienteNoAgente =
        "El cliente no está marcado como agente de retención de IVA; confirme que retuvo el impuesto (confirmaClienteNoAgente) para registrarlo.";
    private const string MensajeIvaMonedaExtranjera =
        "El IVA retenido por clientes sobre facturas en moneda extranjera no está disponible en esta versión.";

    private readonly DeltaErpDbContext _db;
    private readonly ConfiguracionFiscalService _configuracion;
    private readonly ConstanciaRetencionService _constancias;
    private readonly TipoCambioService _tiposCambio;
    private readonly TimeProvider _reloj;

    public RetencionService(
        DeltaErpDbContext db, ConfiguracionFiscalService configuracion, ConstanciaRetencionService constancias,
        TipoCambioService tiposCambio, TimeProvider reloj)
    {
        _db = db;
        _configuracion = configuracion;
        _constancias = constancias;
        _tiposCambio = tiposCambio;
        _reloj = reloj;
    }

    // RN-29, RN-30 y RN-31; unico punto de calculo para retencion-previa y para CrearPago.
    public async Task<(PreparacionIsrNr Preparacion, ErrorValidacion? Error)> PrepararIsrNoResidenteAsync(
        Contraparte proveedor, DateOnly fecha, IReadOnlyCollection<DocumentoPagable> documentos, IReadOnlyList<EntradaIsrNr> entradas)
    {
        if (!RetencionNoResidenteRules.Aplica(proveedor))
        {
            return (PreparacionIsrNr.NoAplica(proveedor.EsResidente ? "El proveedor es residente." : "El proveedor tiene renta exenta."), null);
        }
        var ids = entradas.Select(e => e.DocumentoId).Distinct().ToList();
        var facturas = documentos.Where(d => ids.Contains(d.Id) && !d.EsNotaCredito).ToDictionary(d => d.Id);
        var fechas = await _db.DocumentosCxP.AsNoTracking().Where(d => ids.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Fecha);
        var monedas = await _db.Monedas.AsNoTracking().ToDictionaryAsync(m => m.Id, m => m.Codigo);
        var cache = new Dictionary<string, ParametroRetencion>();
        var lineas = new List<LineaIsrNr>();
        for (var i = 0; i < entradas.Count; i++)
        {
            var e = entradas[i];
            if (!facturas.TryGetValue(e.DocumentoId, out var documento))
            {
                continue;
            }
            var codigo = RetencionNoResidenteRules.CodigoRenta(proveedor, e.TipoRentaCodigo);
            if (codigo is null)
            {
                return (PreparacionIsrNr.NoAplica(string.Empty),
                    ErrorValidacion.Conflicto($"El proveedor {proveedor.Nombre} es no residente: indique el tipo de renta (art. 104) para retener el ISR."));
            }
            var (parametro, errorParametro) = await ResolverParametroAsync(codigo, fecha, cache);
            if (parametro is null)
            {
                return (PreparacionIsrNr.NoAplica(string.Empty), errorParametro);
            }
            // RN-31
            var (cambio, errorCambio) = await _tiposCambio.ResolverParaDocumentoAsync(monedas[documento.MonedaId], fecha);
            if (cambio is null)
            {
                return (PreparacionIsrNr.NoAplica(string.Empty), errorCambio);
            }
            lineas.Add(ConstruirLinea(e, documento.Id, documento.MonedaId, fechas[documento.Id], parametro, cambio, i));
        }
        if (lineas.Count == 0)
        {
            return (PreparacionIsrNr.NoAplica("Las notas de crédito no generan retención."), null);
        }
        var configuracion = await _configuracion.ObtenerAsync();
        var (cuentaId, errorCuenta) = await _configuracion.ResolverCuentaAsync(
            configuracion.CuentaIsrNrRetenidoPagarId, "ISR retenido a no residentes por pagar", "pagos a proveedores no residentes");
        if (errorCuenta is not null)
        {
            return (PreparacionIsrNr.NoAplica(string.Empty), errorCuenta);
        }
        return (new PreparacionIsrNr(true, null, lineas, cuentaId, []), null);
    }

    private async Task<(ParametroRetencion? Parametro, ErrorValidacion? Error)> ResolverParametroAsync(
        string codigo, DateOnly fecha, Dictionary<string, ParametroRetencion> cache)
    {
        if (cache.TryGetValue(codigo, out var enCache))
        {
            return (enCache, null);
        }
        var encontrado = await _db.ParametrosRetencion.FirstOrDefaultAsync(p =>
            p.Codigo == codigo && p.Concepto == ParametroRetencion.ConceptoIsrNoResidente && p.Activo && p.VigenteDesde <= fecha
            && (p.VigenteHasta == null || p.VigenteHasta >= fecha));
        if (encontrado is not null)
        {
            cache[codigo] = encontrado;
            return (encontrado, null);
        }
        var existe = await _db.ParametrosRetencion.AnyAsync(p => p.Codigo == codigo && p.Concepto == ParametroRetencion.ConceptoIsrNoResidente);
        return (null, existe
            ? ErrorValidacion.Conflicto($"No hay un parametro de retencion de ISR a no residentes ({codigo}) vigente el {VigenciaRules.Iso(fecha)}. Solicite al Administrador del sistema que lo configure.")
            : ErrorValidacion.Solicitud($"El tipo de renta {codigo} no existe en el catalogo de ISR a no residentes."));
    }

    private static LineaIsrNr ConstruirLinea(
        EntradaIsrNr e, int documentoId, int monedaId, DateOnly fechaDocumento, ParametroRetencion parametro, TipoCambioAplicable cambio, int indice)
    {
        var baseGtq = RetencionNoResidenteRules.BaseGtq(e.Monto, cambio.Tasa);
        var tasa = parametro.TasaBase ?? 0m;
        return new LineaIsrNr(
            documentoId, fechaDocumento, parametro, monedaId, cambio.MonedaCodigo, e.Monto, cambio.Tasa, cambio.Fecha, baseGtq,
            RetencionNoResidenteRules.Retencion(baseGtq, tasa), RetencionNoResidenteRules.Retencion(e.Monto, tasa), e.ValorEfectivoGtq ?? baseGtq, indice);
    }

    public async Task<IReadOnlyList<RetencionPagoRespuesta>> RegistrarIsrNoResidenteAsync(
        PreparacionIsrNr preparacion, IReadOnlyList<AplicacionPagoProveedor> aplicaciones, int proveedorId, DateOnly fecha, int asientoId, int usuarioId)
    {
        if (!preparacion.Aplica || preparacion.CuentaId is not { } cuentaId)
        {
            return [];
        }
        var retenciones = preparacion.Lineas.Select(l => new Retencion
        {
            Concepto = Retencion.ConceptoIsrNoResidente, ContraparteId = proveedorId, DocumentoCxpId = l.DocumentoId,
            AplicacionPagoProveedorId = aplicaciones[l.Indice].Id, ParametroId = l.Parametro.Id,
            FechaDocumento = l.FechaDocumento, FechaOperacion = fecha, CuentaId = cuentaId, Base = l.BaseGtq, Tasa = l.Parametro.TasaBase ?? 0m,
            MontoCalculado = l.RetencionGtq, MontoRetenido = l.RetencionGtq, MontoRetenidoOriginal = l.RetencionOrigen, MonedaId = l.MonedaId,
            MontoOrigen = l.MontoOrigen, TipoCambio = l.TipoCambio, TipoCambioFecha = l.TipoCambioFecha, TipoRentaCodigo = l.Parametro.Codigo,
            AsientoId = asientoId, RegistradoPor = usuarioId, CreadoEn = DateTimeOffset.UtcNow,
        }).ToList();
        _db.Retenciones.AddRange(retenciones);
        await _db.SaveChangesAsync();
        return retenciones.Select(r => new RetencionPagoRespuesta(r.Id, r.DocumentoCxpId!.Value, r.MontoRetenido)).ToList();
    }

    public async Task<string?> ValidarTipoRentaNrAsync(Contraparte c)
    {
        if (string.IsNullOrWhiteSpace(c.TipoRentaNrCodigo))
        {
            return null;
        }
        var hoy = DateOnly.FromDateTime(_reloj.GetUtcNow().UtcDateTime);
        var existe = await _db.ParametrosRetencion.AnyAsync(p =>
            p.Codigo == c.TipoRentaNrCodigo && p.Concepto == ParametroRetencion.ConceptoIsrNoResidente && p.Activo
            && p.VigenteDesde <= hoy && (p.VigenteHasta == null || p.VigenteHasta >= hoy));
        return existe ? null : "El tipo de renta indicado no existe o no esta vigente.";
    }

    public async Task<ParametroRetencion?> ParametroVigenteAsync(string codigo, DateOnly fecha) =>
        await _db.ParametrosRetencion.FirstOrDefaultAsync(p =>
            p.Codigo == codigo && p.Activo && p.VigenteDesde <= fecha && (p.VigenteHasta == null || p.VigenteHasta >= fecha));

    public async Task<(RetencionIsrPreparada Retencion, ErrorValidacion? Error)> PrepararIsrAsync(
        DocumentoCxP documento, Contraparte proveedor, ResumenFiscal resumen)
    {
        var (evaluacion, parametro, errorEvaluacion) = await EvaluarIsrAsync(proveedor, documento.TipoDocumento, documento.Fecha, resumen.Base);
        if (evaluacion is null)
        {
            return (RetencionIsrPreparada.Vacia, errorEvaluacion);
        }
        var ajuste = RetencionRules.AplicarAjusteIsr(evaluacion, documento.RetencionIsrAjuste, resumen.Total);
        if (ajuste.Error is { } errorAjuste)
        {
            return (RetencionIsrPreparada.Vacia, ErrorDe(errorAjuste));
        }

        int? cuentaId = null;
        if (ajuste.Monto > 0)
        {
            if (parametro is null)
            {
                return (RetencionIsrPreparada.Vacia, ErrorValidacion.Conflicto(MensajeSinParametro("ISR", documento.Fecha)));
            }
            var (cuenta, errorCuenta) = await ResolverCuentaIsrAsync(documento.CuentaControlId);
            if (errorCuenta is not null)
            {
                return (RetencionIsrPreparada.Vacia, errorCuenta);
            }
            cuentaId = cuenta;
        }

        var advertencias = new List<string>();
        if (ajuste.Revision == DocumentoCxP.RevisionPendiente)
        {
            advertencias.Add(AvisoRevision(evaluacion));
        }
        var preparada = new RetencionIsrPreparada(
            evaluacion.Estado, evaluacion.Motivo, evaluacion.Base, parametro?.TasaBase ?? 0m, evaluacion.Retencion, ajuste.Monto,
            parametro, cuentaId, ajuste.MotivoAjuste, ajuste.Revision, ajuste.RevisionMotivo, advertencias);
        return (preparada, null);
    }

    public async Task<Retencion?> RegistrarIsrAsync(IDocumentoFactura documento, RetencionIsrPreparada? preparada, int asientoId, int usuarioId)
    {
        if (preparada is not { Monto: > 0, Parametro: { } parametro, CuentaId: { } cuentaId })
        {
            return null;
        }
        var retencion = new Retencion
        {
            Concepto = Retencion.ConceptoIsr,
            ContraparteId = documento.TerceroId,
            DocumentoCxpId = documento.Id,
            ParametroId = parametro.Id,
            FechaDocumento = documento.Fecha,
            FechaOperacion = documento.Fecha,
            CuentaId = cuentaId,
            Base = preparada.Base,
            Tasa = preparada.Tasa,
            MontoCalculado = preparada.MontoCalculado,
            MontoRetenido = preparada.Monto,
            MontoRetenidoOriginal = OriginalDe(documento, preparada.Monto),
            MotivoAjuste = preparada.MotivoAjuste,
            AsientoId = asientoId,
            RegistradoPor = usuarioId,
            CreadoEn = DateTimeOffset.UtcNow,
        };
        _db.Retenciones.Add(retencion);
        await _db.SaveChangesAsync();
        return retencion;
    }

    private static decimal OriginalDe(IDocumentoFactura documento, decimal monto) =>
        documento.MontoTotal == 0 ? monto : MotorFiscal.Redondear(monto * documento.MontoTotalOriginal / documento.MontoTotal);

    public async Task<(VistaPreviaIsr? Resultado, ErrorValidacion? Error)> VistaPreviaIsrAsync(
        Contraparte proveedor, DateOnly fecha, decimal montoBase, decimal montoTotal)
    {
        var (evaluacion, parametro, errorEvaluacion) = await EvaluarIsrAsync(proveedor, TiposDocumento.Factura, fecha, montoBase);
        if (evaluacion is null)
        {
            return (null, errorEvaluacion);
        }
        if (evaluacion.Retencion > 0)
        {
            var (_, errorCuenta) = await ResolverCuentaIsrAsync(null);
            if (errorCuenta is not null)
            {
                return (null, errorCuenta);
            }
        }

        var advertencias = new List<string>();
        if ((await _configuracion.ObtenerAsync()).AgenteRetencionIva)
        {
            advertencias.Add(AvisoAgenteIva);
        }
        if (evaluacion.Estado == EstadoRetencionIsr.PendienteRevision)
        {
            advertencias.Add(AvisoRevision(evaluacion));
        }
        var aplicado = evaluacion.Estado == EstadoRetencionIsr.PendienteRevision ? null : parametro;
        var vista = new VistaPreviaIsr(
            evaluacion.Estado.ToString(), evaluacion.Motivo, evaluacion.Base, aplicado?.TasaBase, aplicado?.TramoLimite,
            aplicado?.TasaExcedente, aplicado?.UmbralMinimo, evaluacion.Retencion, montoTotal - evaluacion.Retencion, aplicado?.Id,
            true, advertencias);
        return (vista, null);
    }

    public async Task<(PreparacionIva Preparacion, ErrorValidacion? Error)> PrepararIvaClienteAsync(Contraparte cliente, ReciboPagoCliente recibo)
    {
        var solicitadas = recibo.Aplicaciones.Where(a => a.RetencionIva is not null).ToList();
        if (solicitadas.Count == 0)
        {
            return (PreparacionIva.Vacia, null);
        }

        var ids = solicitadas.Select(a => a.DocumentoId).ToList();
        var error = ValidarSolicitudesIva(cliente, solicitadas) ?? await ValidarNoRetenidasAsync(ids);
        if (error is not null)
        {
            return (PreparacionIva.Vacia, error);
        }
        var (cuentaId, errorCuenta) = await _configuracion.ResolverCuentaAsync(
            (await _configuracion.ObtenerAsync()).CuentaIvaRetenidoClientesId, "IVA retenido por clientes", "cobros con IVA retenido");
        if (errorCuenta is not null)
        {
            return (PreparacionIva.Vacia, errorCuenta);
        }

        var documentos = await _db.DocumentosCxC.Where(d => ids.Contains(d.Id)).ToDictionaryAsync(d => d.Id);
        var monedaFuncionalId = (await _configuracion.ObtenerAsync()).MonedaFuncionalId;
        if (documentos.Values.Any(d => d.MonedaId != monedaFuncionalId))
        {
            return (PreparacionIva.Vacia, ErrorValidacion.Conflicto(MensajeIvaMonedaExtranjera));
        }
        var ivaNotas = await IvaNotasCreditoAsync(ids);
        var retenciones = new List<RetencionIvaPreparada>();
        var advertencias = new List<string>();
        foreach (var aplicacion in solicitadas)
        {
            var (preparada, errorLinea) = await PrepararLineaIvaAsync(
                cliente, aplicacion, documentos[aplicacion.DocumentoId], ivaNotas.GetValueOrDefault(aplicacion.DocumentoId), advertencias);
            if (preparada is null)
            {
                return (PreparacionIva.Vacia, errorLinea);
            }
            retenciones.Add(preparada);
        }
        return (new PreparacionIva(retenciones, cuentaId, advertencias), null);
    }

    public async Task<IReadOnlyList<RetencionCobroRespuesta>> RegistrarIvaClienteAsync(
        PreparacionIva preparacion, ReciboPagoCliente recibo, int asientoId, int usuarioId)
    {
        if (preparacion.CuentaId is not { } cuentaId)
        {
            return [];
        }
        var pares = preparacion.Retenciones
            .Select(p => (Preparada: p, Retencion: NuevaRetencionIva(p, recibo, cuentaId, asientoId, usuarioId)))
            .ToList();
        _db.Retenciones.AddRange(pares.Select(p => p.Retencion));
        await _db.SaveChangesAsync();

        var constancias = new List<(Retencion Retencion, ConstanciaRetencion Constancia)>();
        foreach (var (preparada, retencion) in pares)
        {
            if (preparada.Constancia is { } entrada)
            {
                constancias.Add((retencion, ConstanciaRetencionService.Nueva(retencion, entrada.Numero, entrada.Fecha, usuarioId)));
            }
        }
        _db.ConstanciasRetencion.AddRange(constancias.Select(c => c.Constancia));
        await _db.SaveChangesAsync();

        return pares.Select(p => new RetencionCobroRespuesta(
            p.Retencion.Id,
            p.Preparada.Documento.Id,
            p.Retencion.MontoRetenido,
            constancias.FirstOrDefault(c => c.Retencion == p.Retencion).Constancia?.Id)).ToList();
    }

    public async Task<SugerenciaIvaRespuesta?> SugerirIvaAsync(int documentoId)
    {
        var documento = await _db.DocumentosCxC.FindAsync(documentoId);
        if (documento is null)
        {
            return null;
        }
        var cliente = await _db.Contrapartes.FirstAsync(c => c.Id == documento.ClienteId);
        var ivaNotas = (await IvaNotasCreditoAsync([documento.Id])).GetValueOrDefault(documento.Id);
        var yaRetenida = await _db.Retenciones.AnyAsync(r => r.Concepto == Retencion.ConceptoIva && r.DocumentoCxcId == documento.Id);
        var parametro = await ParametroVigenteAsync(RetencionRules.CodigoParametroIvaCliente(cliente), documento.Fecha);

        var ivaRetenible = documento.MontoIva - ivaNotas;
        var sugerido = parametro is null || yaRetenida || documento.TipoDocumento != TiposDocumento.Factura
            ? 0m
            : RetencionRules.SugerirIvaCliente(ivaRetenible, documento.MontoBase, documento.MontoTotal, RetencionRules.ComoCalculo(parametro));
        var motivo = MotivoSinSugerencia(documento, parametro, yaRetenida, sugerido);
        return new SugerenciaIvaRespuesta(
            motivo is null, motivo, cliente.EsAgenteRetencionIva, cliente.TipoAgenteIva, parametro?.TasaBase, documento.MontoIva, ivaNotas,
            ivaRetenible, parametro?.UmbralMinimo, documento.MontoTotal, sugerido, yaRetenida, parametro?.Id);
    }

    public static string MensajeSinParametro(string impuesto, DateOnly fecha) =>
        $"No hay un parámetro de retención de {impuesto} vigente el {VigenciaRules.Iso(fecha)}. Solicite al Administrador del sistema que lo configure.";

    private static ErrorValidacion ErrorDe(ErrorRegla error) =>
        error.EsConflicto ? ErrorValidacion.Conflicto(error.Mensaje) : ErrorValidacion.Solicitud(error.Mensaje);

    private static string AvisoRevision(EvaluacionIsr evaluacion) =>
        $"{evaluacion.Motivo} La factura se registró sin retención y quedó marcada para revisión.";

    private static string? MotivoSinSugerencia(DocumentoCxC documento, ParametroRetencion? parametro, bool yaRetenida, decimal sugerido)
    {
        if (documento.TipoDocumento != TiposDocumento.Factura || documento.Estado != "Vigente")
        {
            return "Solo una factura vigente admite retención de IVA.";
        }
        if (yaRetenida)
        {
            return MensajeIvaYaRetenido;
        }
        if (parametro is null)
        {
            return MensajeSinParametro("IVA", documento.Fecha);
        }
        return sugerido > 0
            ? null
            : $"El total de la factura no alcanza el umbral de retención de {RetencionRules.Quetzales(parametro.UmbralMinimo)}.";
    }

    private async Task<(EvaluacionIsr? Evaluacion, ParametroRetencion? Parametro, ErrorValidacion? Error)> EvaluarIsrAsync(
        Contraparte proveedor, string tipoDocumento, DateOnly fecha, decimal baseSinIva)
    {
        var parametro = await ParametroVigenteAsync(ParametroRetencion.CodigoIsrProveedor, fecha);
        if (parametro is null && RetencionRules.RequiereParametroIsr(proveedor, tipoDocumento))
        {
            return (null, null, ErrorValidacion.Conflicto(MensajeSinParametro("ISR", fecha)));
        }
        var calculo = parametro is null ? null : RetencionRules.ComoCalculo(parametro);
        return (RetencionRules.EvaluarIsrProveedor(proveedor, tipoDocumento, baseSinIva, calculo), parametro, null);
    }

    private async Task<(int? CuentaId, ErrorValidacion? Error)> ResolverCuentaIsrAsync(int? cuentaControlId)
    {
        var configuracion = await _configuracion.ObtenerAsync();
        var (cuentaId, error) = await _configuracion.ResolverCuentaAsync(
            configuracion.CuentaIsrRetenidoPagarId, "ISR retenido por pagar", "facturas con retención de ISR");
        return error is null && cuentaId == cuentaControlId
            ? (null, ErrorValidacion.Solicitud("La cuenta de control no puede ser la cuenta de ISR retenido por pagar."))
            : (cuentaId, error);
    }

    private static ErrorValidacion? ValidarSolicitudesIva(Contraparte cliente, List<AplicacionPagoCliente> solicitadas)
    {
        if (solicitadas.GroupBy(a => a.DocumentoId).Any(g => g.Count() > 1))
        {
            return ErrorValidacion.Conflicto(MensajeIvaYaRetenido);
        }
        return !cliente.EsAgenteRetencionIva && solicitadas.Any(a => a.RetencionIva?.ConfirmaClienteNoAgente != true)
            ? ErrorValidacion.Conflicto(MensajeClienteNoAgente)
            : null;
    }

    private async Task<ErrorValidacion?> ValidarNoRetenidasAsync(List<int> ids)
    {
        var idsNulos = ids.Select(id => (int?)id).ToList();
        var yaRetenida = await _db.Retenciones.AnyAsync(r => r.Concepto == Retencion.ConceptoIva && idsNulos.Contains(r.DocumentoCxcId));
        return yaRetenida ? ErrorValidacion.Conflicto(MensajeIvaYaRetenido) : null;
    }

    private async Task<Dictionary<int, decimal>> IvaNotasCreditoAsync(List<int> ids)
    {
        var idsNulos = ids.Select(id => (int?)id).ToList();
        var notas = await _db.DocumentosCxC
            .Where(n => n.TipoDocumento == TiposDocumento.NotaCredito && n.Estado == "Vigente" && idsNulos.Contains(n.DocumentoOrigenId))
            .Select(n => new { Origen = n.DocumentoOrigenId ?? 0, n.MontoIva })
            .ToListAsync();
        return notas.GroupBy(n => n.Origen).ToDictionary(g => g.Key, g => g.Sum(n => n.MontoIva));
    }

    private async Task<(RetencionIvaPreparada? Retencion, ErrorValidacion? Error)> PrepararLineaIvaAsync(
        Contraparte cliente, AplicacionPagoCliente aplicacion, DocumentoCxC documento, decimal ivaNotas, List<string> advertencias)
    {
        var entrada = aplicacion.RetencionIva ?? throw new InvalidOperationException("La aplicación no solicita retención de IVA.");
        var ivaRetenible = documento.MontoIva - ivaNotas;
        var errorRegla = RetencionRules.ValidarIvaCliente(entrada.Monto, aplicacion.MontoAplicado, ivaRetenible);
        if (errorRegla is not null)
        {
            return (null, ErrorDe(errorRegla));
        }
        var errorConstancia = await ValidarConstanciaIvaAsync(cliente, entrada.Constancia);
        if (errorConstancia is not null)
        {
            return (null, errorConstancia);
        }
        var parametro = await ParametroVigenteAsync(RetencionRules.CodigoParametroIvaCliente(cliente), documento.Fecha);
        if (parametro is null)
        {
            return (null, ErrorValidacion.Conflicto(MensajeSinParametro("IVA", documento.Fecha)));
        }

        var sugerido = RetencionRules.SugerirIvaCliente(ivaRetenible, documento.MontoBase, documento.MontoTotal, RetencionRules.ComoCalculo(parametro));
        if (sugerido != entrada.Monto)
        {
            advertencias.Add(
                $"El IVA retenido digitado ({RetencionRules.Quetzales(entrada.Monto)}) difiere del sugerido ({RetencionRules.Quetzales(sugerido)}) para la factura {documento.Numero}.");
        }
        if (entrada.Constancia is { } constancia && constancia.Fecha != documento.Fecha)
        {
            advertencias.Add($"La fecha de la constancia de la factura {documento.Numero} difiere de la fecha de la factura ({VigenciaRules.Iso(documento.Fecha)}).");
        }
        return (new RetencionIvaPreparada(aplicacion, documento, parametro, ivaRetenible, sugerido, entrada.Monto, entrada.Constancia), null);
    }

    private async Task<ErrorValidacion?> ValidarConstanciaIvaAsync(Contraparte cliente, ConstanciaEntrada? constancia)
    {
        if (constancia is null)
        {
            return null;
        }
        var error = ConstanciaRetencionService.ValidarNumero(constancia.Numero);
        if (error is not null)
        {
            return ErrorValidacion.Solicitud(error);
        }
        return await _constancias.ExisteNumeroEmitidoAsync(ConstanciaRetencion.TipoIva, cliente.Id, constancia.Numero.Trim())
            ? ErrorValidacion.Conflicto(ConstanciaRetencionService.MensajeNumeroDuplicado)
            : null;
    }

    private static Retencion NuevaRetencionIva(RetencionIvaPreparada preparada, ReciboPagoCliente recibo, int cuentaId, int asientoId, int usuarioId) => new()
    {
        Concepto = Retencion.ConceptoIva,
        ContraparteId = recibo.ClienteId,
        DocumentoCxcId = preparada.Documento.Id,
        AplicacionPagoClienteId = preparada.Aplicacion.Id,
        ParametroId = preparada.Parametro.Id,
        FechaDocumento = preparada.Documento.Fecha,
        FechaOperacion = recibo.Fecha,
        CuentaId = cuentaId,
        Base = preparada.Base,
        Tasa = preparada.Parametro.TasaBase ?? 0m,
        MontoCalculado = preparada.Sugerido,
        MontoRetenido = preparada.Monto,
        MontoRetenidoOriginal = preparada.Monto,
        AsientoId = asientoId,
        RegistradoPor = usuarioId,
        CreadoEn = DateTimeOffset.UtcNow,
    };
}
