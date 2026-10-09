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
}
