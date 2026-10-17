using DeltaERP.Api.Models;
using DeltaERP.Domain.Entities;
using DeltaERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Api.Services;

public class ConstanciaRetencionService
{
    public const string MensajeNumeroDuplicado = "Ya existe una constancia emitida con ese número para el mismo tipo y retenedor.";
    public const string MensajeSinConstanciaNr = "Las retenciones de ISR a no residentes no admiten constancia en esta version.";
    private const int LongitudMaximaNumero = 30;
    private const int LongitudMaximaMotivo = 300;

    private readonly DeltaErpDbContext _db;

    public ConstanciaRetencionService(DeltaErpDbContext db)
    {
        _db = db;
    }

    public static string? ValidarNumero(string? numero)
    {
        if (string.IsNullOrWhiteSpace(numero))
        {
            return "El número de la constancia es obligatorio.";
        }
        return numero.Trim().Length > LongitudMaximaNumero ? "El número de la constancia admite hasta 30 caracteres." : null;
    }

    public static string? ValidarMotivo(string? motivo, string de)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            return $"El motivo {de} es obligatorio.";
        }
        return motivo.Trim().Length > LongitudMaximaMotivo ? "El motivo admite hasta 300 caracteres." : null;
    }

    public static ConstanciaRetencion Nueva(Retencion retencion, string numero, DateOnly fecha, int usuarioId)
    {
        var esIsr = retencion.Concepto == Retencion.ConceptoIsr;
        var ahora = DateTimeOffset.UtcNow;
        return new ConstanciaRetencion
        {
            RetencionId = retencion.Id,
            Tipo = esIsr ? ConstanciaRetencion.TipoIsr : ConstanciaRetencion.TipoIva,
            RetenedorContraparteId = esIsr ? null : retencion.ContraparteId,
            Numero = numero.Trim(),
            Fecha = fecha,
            CreadoEn = ahora,
            CreadoPor = usuarioId,
            ActualizadoEn = ahora,
            ActualizadoPor = usuarioId,
        };
    }

    public Task<bool> ExisteNumeroEmitidoAsync(string tipo, int? retenedorId, string numero) =>
        _db.ConstanciasRetencion.AnyAsync(c =>
            c.Estado == ConstanciaRetencion.EstadoEmitida && c.Tipo == tipo && c.RetenedorContraparteId == retenedorId && c.Numero == numero);

    public async Task<ErrorValidacion?> ValidarNuevaAsync(Retencion retencion, SolicitudConstancia datos)
    {
        if (retencion.Concepto == Retencion.ConceptoIsrNoResidente)
        {
            return ErrorValidacion.Conflicto(MensajeSinConstanciaNr);
        }
        var error = ValidarNumero(datos.Numero) ?? ValidarDatosOpcionales(datos);
        if (error is not null)
        {
            return ErrorValidacion.Solicitud(error);
        }
        if (await _db.ConstanciasRetencion.AnyAsync(c => c.RetencionId == retencion.Id && c.Estado == ConstanciaRetencion.EstadoEmitida))
        {
            return ErrorValidacion.Conflicto("La retención ya tiene una constancia emitida.");
        }
        var nueva = Nueva(retencion, datos.Numero, datos.Fecha, 0);
        return await ExisteNumeroEmitidoAsync(nueva.Tipo, nueva.RetenedorContraparteId, nueva.Numero)
            ? ErrorValidacion.Conflicto(MensajeNumeroDuplicado)
            : null;
    }

    public static ConstanciaRetencion Registrar(Retencion retencion, SolicitudConstancia datos, int usuarioId)
    {
        var constancia = Nueva(retencion, datos.Numero, datos.Fecha, usuarioId);
        constancia.Formulario = datos.Formulario;
        constancia.FechaPagoFormulario = datos.FechaPagoFormulario;
        constancia.PeriodoDeclarado = datos.PeriodoDeclarado;
        return constancia;
    }

    public static void Anular(ConstanciaRetencion constancia, string motivo, int usuarioId)
    {
        constancia.Estado = ConstanciaRetencion.EstadoAnulada;
        constancia.MotivoAnulacion = motivo.Trim();
        constancia.ActualizadoEn = DateTimeOffset.UtcNow;
        constancia.ActualizadoPor = usuarioId;
    }

    private static string? ValidarDatosOpcionales(SolicitudConstancia datos)
    {
        if (datos.Formulario is not null && !ConstanciaRetencion.FormulariosValidos.Contains(datos.Formulario))
        {
            return $"Formulario inválido. Debe ser uno de: {string.Join(", ", ConstanciaRetencion.FormulariosValidos)}.";
        }
        return datos.PeriodoDeclarado?.Day > 1 ? "El período declarado debe indicarse con el día 1 del mes." : null;
    }
}
