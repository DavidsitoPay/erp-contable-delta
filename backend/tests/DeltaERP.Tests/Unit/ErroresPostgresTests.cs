using System.Text.Json;
using DeltaERP.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DeltaERP.Tests.Unit;

public class ErroresPostgresTests
{
    private static PostgresException Pg(string sqlState, string? constraint = null) =>
        new("mensaje de prueba", "ERROR", "ERROR", sqlState, constraintName: constraint);

    [Fact]
    public async Task Traducir_SinError_DevuelveNull()
    {
        var resultado = await ErroresPostgres.TraducirProcedimientoAsync(() => Task.CompletedTask);

        Assert.Null(resultado);
    }

    [Theory]
    [InlineData("P0001", 403)]
    [InlineData("P0002", 404)]
    [InlineData("55000", 409)]
    [InlineData("23514", 400)]
    public async Task Traducir_ConSqlStateConocido_DevuelveEstadoYMensaje(string sqlState, int estadoEsperado)
    {
        var resultado = await ErroresPostgres.TraducirProcedimientoAsync(() => Task.FromException(Pg(sqlState)));

        Assert.NotNull(resultado);
        Assert.Equal(estadoEsperado, resultado.StatusCode);
        Assert.Contains("mensaje de prueba", JsonSerializer.Serialize(resultado.Value));
    }

    [Fact]
    public async Task Traducir_ConOtroSqlState_PropagaLaExcepcion()
    {
        await Assert.ThrowsAsync<PostgresException>(
            () => ErroresPostgres.TraducirProcedimientoAsync(() => Task.FromException(Pg("23505"))));
    }

    [Fact]
    public void EsViolacionUnica_SoloEsVerdaderaParaElSqlStateYLaConstraintIndicados()
    {
        Assert.True(ErroresPostgres.EsViolacionUnica(new DbUpdateException("x", Pg("23505", "uq_a")), "uq_a"));
        Assert.False(ErroresPostgres.EsViolacionUnica(new DbUpdateException("x", Pg("23505", "uq_b")), "uq_a"));
        Assert.False(ErroresPostgres.EsViolacionUnica(new DbUpdateException("x", Pg("23503", "uq_a")), "uq_a"));
        Assert.False(ErroresPostgres.EsViolacionUnica(new DbUpdateException("x", new InvalidOperationException()), "uq_a"));
        Assert.False(ErroresPostgres.EsViolacionUnica(new DbUpdateException("x"), "uq_a"));
    }

    [Theory]
    [InlineData("23514", 400)]
    [InlineData("55000", 409)]
    public void TraducirActualizacion_ConSqlStateConocido_DevuelveEstadoYMensaje(string sqlState, int estadoEsperado)
    {
        var resultado = ErroresPostgres.TraducirActualizacionAsync(new DbUpdateException("x", Pg(sqlState)));

        Assert.NotNull(resultado);
        Assert.Equal(estadoEsperado, resultado.StatusCode);
        Assert.Contains("mensaje de prueba", JsonSerializer.Serialize(resultado.Value));
    }

    [Fact]
    public void TraducirActualizacion_ConOtroSqlState_DevuelveNull()
    {
        Assert.Null(ErroresPostgres.TraducirActualizacionAsync(new DbUpdateException("x", Pg("23505"))));
    }

    [Fact]
    public void TraducirActualizacion_Con55000EnvueltoEnInvalidOperation_DevuelveConflicto()
    {
        var resultado = ErroresPostgres.TraducirActualizacionAsync(
            new InvalidOperationException("x", new DbUpdateException("x", Pg("55000"))));

        Assert.NotNull(resultado);
        Assert.Equal(409, resultado.StatusCode);
    }

    [Fact]
    public void TraducirActualizacion_Con23505EnvueltoEnInvalidOperation_DevuelveNull()
    {
        Assert.Null(ErroresPostgres.TraducirActualizacionAsync(
            new InvalidOperationException("x", new DbUpdateException("x", Pg("23505")))));
    }

    [Theory]
    [InlineData("55000", 409)]
    [InlineData("23514", 400)]
    public void ComoErrorValidacion_SqlStateDeNegocio_DevuelveEstadoYMensaje(string sqlState, int estadoEsperado)
    {
        var resultado = ErroresPostgres.ComoErrorValidacion(Pg(sqlState));

        Assert.NotNull(resultado);
        Assert.Equal(estadoEsperado, resultado.Estado);
        Assert.Equal("mensaje de prueba", resultado.Mensaje);
    }

    [Fact]
    public void ComoErrorValidacion_Con55000EnvueltoEnInvalidOperation_DevuelveConflicto()
    {
        var resultado = ErroresPostgres.ComoErrorValidacion(
            new InvalidOperationException("x", new DbUpdateException("x", Pg("55000"))));

        Assert.NotNull(resultado);
        Assert.Equal(409, resultado.Estado);
    }

    [Fact]
    public void ComoErrorValidacion_UnicaConocida_DevuelveConflictoConElMensaje()
    {
        var resultado = ErroresPostgres.ComoErrorValidacion(
            new DbUpdateException("x", Pg("23505", "ux_revaluacion_vigente")));

        Assert.NotNull(resultado);
        Assert.Equal(409, resultado.Estado);
        Assert.Contains("revaluación vigente", resultado.Mensaje);
    }

    [Fact]
    public void ComoErrorValidacion_SinTraduccion_DevuelveNull()
    {
        Assert.Null(ErroresPostgres.ComoErrorValidacion(Pg("P0001")));
        Assert.Null(ErroresPostgres.ComoErrorValidacion(new DbUpdateException("x", Pg("23505", "uq_desconocida"))));
    }
}
