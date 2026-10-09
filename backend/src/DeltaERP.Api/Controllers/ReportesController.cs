using DeltaERP.Api.Auth;
using DeltaERP.Domain.Entities;
using DeltaERP.Domain.Rules;
using DeltaERP.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Api.Controllers;

[ApiController]
[Route("api/reportes")]
[Authorize(Roles = Roles.GestionCatalogo)]
public class ReportesController : ControllerBase
{
    private const string FuenteCierre = "Cierre";
    private const string FuentePreliminar = "Preliminar";

    private readonly DeltaErpDbContext _db;

    public ReportesController(DeltaErpDbContext db)
    {
        _db = db;
    }

    [HttpGet("balance-general")]
    public async Task<IActionResult> BalanceGeneral([FromQuery] int periodoId)
    {
        var periodo = await BuscarPeriodoAsync(periodoId);
        if (periodo is null)
        {
            return PeriodoInexistente();
        }

        var balance = EstadosFinancieros.BalanceGeneral(await LeerSaldosAsync(periodo.Id, acumulado: true));
        var incluyePeriodosAbiertos = await _db.PeriodosContables.AnyAsync(p =>
            p.Id != periodo.Id && p.FechaFin <= periodo.FechaFin && p.Estado != PeriodoContable.EstadoCerrado);

        return Ok(new
        {
            Periodo = ResumenPeriodo(periodo),
            Fuente = FuenteDe(periodo),
            IncluyePeriodosAbiertos = incluyePeriodosAbiertos,
            balance.Activo,
            balance.Pasivo,
            balance.Capital,
            balance.ResultadoEjercicio,
            balance.TotalCapital,
            balance.TotalPasivoCapital,
            balance.Diferencia,
            balance.Cuadra,
        });
    }

    [HttpGet("estado-resultados")]
    public async Task<IActionResult> EstadoResultados([FromQuery] int periodoId)
    {
        var periodo = await BuscarPeriodoAsync(periodoId);
        if (periodo is null)
        {
            return PeriodoInexistente();
        }

        var estado = EstadosFinancieros.EstadoResultados(await LeerSaldosAsync(periodo.Id, acumulado: false));

        return Ok(new
        {
            Periodo = ResumenPeriodo(periodo),
            Fuente = FuenteDe(periodo),
            estado.Ingresos,
            estado.Gastos,
            estado.UtilidadNeta,
        });
    }

    private Task<PeriodoContable?> BuscarPeriodoAsync(int periodoId) =>
        _db.PeriodosContables.AsNoTracking().FirstOrDefaultAsync(p => p.Id == periodoId);

    private Task<List<SaldoReporteCuenta>> LeerSaldosAsync(int periodoId, bool acumulado) =>
        _db.SaldosReporte
            .FromSqlInterpolated($"SELECT * FROM fn_reporte_saldos({periodoId}, {acumulado})")
            .ToListAsync();

    private static string FuenteDe(PeriodoContable periodo) =>
        periodo.Estado == PeriodoContable.EstadoCerrado ? FuenteCierre : FuentePreliminar;

    private static object ResumenPeriodo(PeriodoContable p) =>
        new { p.Id, p.Nombre, p.FechaInicio, p.FechaFin, p.Estado, p.Cierres };

    private BadRequestObjectResult PeriodoInexistente() =>
        BadRequest(new { error = "El periodo indicado no existe." });
}
