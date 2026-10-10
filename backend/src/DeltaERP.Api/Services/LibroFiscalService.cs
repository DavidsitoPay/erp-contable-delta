using DeltaERP.Api.Models;
using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;
using DeltaERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Api.Services;

public class LibroFiscalService
{
    private readonly DeltaErpDbContext _db;
    private readonly ValidacionContable _validacion;
    private readonly ConfiguracionFiscalService _configuracion;

    public LibroFiscalService(DeltaErpDbContext db, ValidacionContable validacion, ConfiguracionFiscalService configuracion)
    {
        _db = db;
        _validacion = validacion;
        _configuracion = configuracion;
    }

    public async Task<LibroFiscalRespuesta> GenerarAsync(bool esVenta, int anio, int mes)
    {
        var inicio = new DateOnly(anio, mes, 1);
        var documentos = (await CargarDocumentosAsync(esVenta, inicio, inicio.AddMonths(1)))
            .OrderBy(d => d.Fecha).ThenBy(d => d.Numero, StringComparer.Ordinal).ToList();
        var impuestos = await _db.Impuestos.ToDictionaryAsync(i => i.Id);
        var idsTercero = documentos.Select(d => d.TerceroId).Distinct().ToList();
        var terceros = await _db.Contrapartes.Where(c => idsTercero.Contains(c.Id)).ToDictionaryAsync(c => c.Id);

        var filas = new List<FilaLibroFiscal>();
        foreach (var documento in documentos)
        {
            var origen = documento.DocumentoOrigenId is { } origenId ? await _validacion.ObtenerDocumentoAsync(esVenta, origenId) : null;
            filas.Add(ConstruirFila(documento, terceros[documento.TerceroId], impuestos, esVenta, origen));
        }

        var configuracion = await _configuracion.ObtenerAsync();
        var legados = documentos.Count(d => d.CalculoLegado);
        var advertencias = legados == 0
            ? new List<string>()
            : new List<string> { $"El libro incluye {legados} documento(s) anteriores a la configuración fiscal (calculo legado)." };
        return new LibroFiscalRespuesta(
            esVenta ? "VENTAS" : "COMPRAS", anio, mes,
            new ContribuyenteLibro(configuracion.NitEmpresa, configuracion.NombreLegal),
            filas, Totalizar(filas), advertencias);
    }

    private async Task<List<IDocumentoFactura>> CargarDocumentosAsync(bool esVenta, DateOnly inicio, DateOnly fin)
    {
        if (esVenta)
        {
            var ventas = await _db.DocumentosCxC.Include(d => d.Lineas).Where(d => d.Fecha >= inicio && d.Fecha < fin).ToListAsync();
            return ventas.Cast<IDocumentoFactura>().ToList();
        }
        var compras = await _db.DocumentosCxP.Include(d => d.Lineas).Where(d => d.Fecha >= inicio && d.Fecha < fin).ToListAsync();
        return compras.Cast<IDocumentoFactura>().ToList();
    }

    private static FilaLibroFiscal ConstruirFila(
        IDocumentoFactura documento, Contraparte tercero, IReadOnlyDictionary<int, Impuesto> impuestos, bool esVenta, IDocumentoFactura? origen)
    {
        var esNotaCredito = documento.TipoDocumento == TiposDocumento.NotaCredito;
        var signo = esNotaCredito ? -1m : 1m;
        var importes = documento.Estado == "Anulado"
            ? new ImportesLibro(0m, 0m, 0m, 0m)
            : SumarLineas(documento.Lineas, impuestos, esVenta);
        var total = importes.BaseBienes + importes.BaseServicios + importes.Exento + importes.Iva;
        return new FilaLibroFiscal(
            documento.Fecha, documento.TipoDocumento, documento.DteSerie,
            esVenta ? documento.DteUuid?.ToString() : documento.DteNumero,
            documento.Numero,
            string.IsNullOrWhiteSpace(tercero.Nit) ? NitRules.ConsumidorFinal : tercero.Nit,
            tercero.Nombre, documento.Estado, documento.CalculoLegado,
            importes.BaseBienes * signo, importes.BaseServicios * signo, importes.Exento * signo, importes.Iva * signo, total * signo,
            origen?.DteSerie, origen?.DteNumero, origen?.DteUuid);
    }

    private static ImportesLibro SumarLineas(IReadOnlyList<ILineaFactura> lineas, IReadOnlyDictionary<int, Impuesto> impuestos, bool esVenta)
    {
        var distribuidas = lineas.Select(l =>
        {
            var impuesto = impuestos[l.ImpuestoId];
            var resultado = new ResultadoLinea(l.MontoLinea, l.MontoBase, l.MontoIva, l.TasaAplicada, l.MontoIva > 0 && impuesto.GeneraCredito, l.TipoBienServicio);
            return MotorFiscal.DistribuirParaLibro(ImpuestoRules.ComoParametro(impuesto), resultado, esVenta);
        }).ToList();
        return new ImportesLibro(
            distribuidas.Sum(i => i.BaseBienes), distribuidas.Sum(i => i.BaseServicios), distribuidas.Sum(i => i.Exento), distribuidas.Sum(i => i.Iva));
    }

    private static TotalesLibroFiscal Totalizar(IReadOnlyList<FilaLibroFiscal> filas) => new(
        filas.Sum(f => f.BaseBienes), filas.Sum(f => f.BaseServicios), filas.Sum(f => f.Exento), filas.Sum(f => f.Iva), filas.Sum(f => f.Total));
}
