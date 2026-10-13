using System.Text.Json;
using DeltaERP.Api.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DeltaERP.Tests.Unit;

public class ErroresPostgresRetencionTests
{
    private static DbUpdateException Actualizacion(string sqlState, string? constraint = null) =>
        new("x", new PostgresException("mensaje de prueba", "ERROR", "ERROR", sqlState, constraintName: constraint));

    [Theory]
    [InlineData("55000", null, "mensaje de prueba")]
    [InlineData("23505", "ux_retencion_isr_documento", "La factura ya tiene retención de ISR registrada.")]
    [InlineData("23505", "ux_retencion_iva_documento", "La factura ya tiene IVA retenido registrado.")]
    [InlineData("23505", "ux_constancia_retencion", "La retención ya tiene una constancia emitida.")]
    [InlineData("23505", "ux_constancia_numero", "Ya existe una constancia emitida con ese número para el mismo tipo y retenedor.")]
    public void MensajeConflicto_TraduceTriggersEIndicesUnicosConocidos(string sqlState, string? constraint, string esperado)
    {
        Assert.Equal(esperado, ErroresPostgres.MensajeConflicto(Actualizacion(sqlState, constraint)));
    }

    [Theory]
    [InlineData("23505", "ux_otra")]
    [InlineData("23505", null)]
    [InlineData("23514", null)]
    public void MensajeConflicto_ConOtrosErrores_DevuelveNull(string sqlState, string? constraint)
    {
        Assert.Null(ErroresPostgres.MensajeConflicto(Actualizacion(sqlState, constraint)));
    }

    [Fact]
    public void MensajeConflicto_ConExcepcionSinPostgres_DevuelveNull()
    {
        Assert.Null(ErroresPostgres.MensajeConflicto(new InvalidOperationException("x")));
    }

    [Fact]
    public void TraducirActualizacion_ConIndiceUnicoConocido_DevuelveConflicto()
    {
        var resultado = ErroresPostgres.TraducirActualizacionAsync(Actualizacion("23505", "ux_constancia_numero"));

        Assert.NotNull(resultado);
        Assert.Equal(409, resultado.StatusCode);
        Assert.Contains("Ya existe una constancia emitida", JsonSerializer.Serialize(resultado.Value));
    }
}
