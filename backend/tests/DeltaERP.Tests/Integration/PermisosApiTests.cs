using DeltaERP.Tests.Support;
using Npgsql;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class PermisosApiTests
{
    private static readonly string[] RutinasPermitidas =
    {
        "fn_reporte_saldos(integer,boolean)",
        "fn_usuario_tiene_perfil_autorizado(integer,character varying[])",
        "fn_perfil_contador()",
        "fn_perfil_administrador_sistema()",
        "sp_registrar_auditoria(integer,character varying,character varying,text)",
        "sp_cerrar_periodo(integer,integer)",
        "sp_reabrir_periodo(integer,integer)",
        "sp_reversar_asiento(integer,integer,text)",
        "sp_finalizar_conciliacion(integer,integer)",
    };

    private static readonly Dictionary<string, string> PermisosEsperados = new()
    {
        ["perfil"] = "S",
        ["usuario"] = "S",
        ["bitacoraauditoria"] = "I",
        ["moneda"] = "S",
        ["historialtipocambio"] = "",
        ["periodocontable"] = "SIU",
        ["contraparte"] = "SIU",
        ["cuentacontable"] = "SIU",
        ["centrocosto"] = "SIU",
        ["asientocontable"] = "SIU",
        ["lineaasiento"] = "SI",
        ["plantillaasiento"] = "",
        ["lineaplantillaasiento"] = "",
        ["documentocxc"] = "SIU",
        ["lineadocumentocxc"] = "SI",
        ["recibopagocliente"] = "SI",
        ["aplicacionpagocliente"] = "SI",
        ["documentocxp"] = "SIU",
        ["lineadocumentocxp"] = "SI",
        ["pagoproveedorcabecera"] = "SI",
        ["aplicacionpagoproveedor"] = "SI",
        ["cuentabancaria"] = "SIU",
        ["movimientotesoreria"] = "SI",
        ["conciliacionbancaria"] = "SIU",
        ["detalleconciliacion"] = "SID",
        ["saldocuentaperiodo"] = "I",
        ["impuesto"] = "SIU",
        ["configuracionfiscal"] = "SU",
    };

    private static readonly (char Letra, string Privilegio)[] Privilegios =
    {
        ('S', "SELECT"),
        ('I', "INSERT"),
        ('U', "UPDATE"),
        ('D', "DELETE"),
    };

    private readonly PostgresFixture _pg;

    public PermisosApiTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    [Theory]
    [InlineData("UPDATE bitacoraauditoria SET id = id WHERE false")]
    [InlineData("UPDATE lineaasiento SET id = id WHERE false")]
    [InlineData("UPDATE movimientotesoreria SET id = id WHERE false")]
    [InlineData("DELETE FROM bitacoraauditoria WHERE false")]
    [InlineData("DELETE FROM asientocontable WHERE false")]
    [InlineData("SELECT count(*) FROM bitacoraauditoria")]
    [InlineData("SELECT count(*) FROM saldocuentaperiodo")]
    [InlineData("INSERT INTO usuario (nombre) VALUES ('x')")]
    [InlineData("SELECT fn_bitacora_inmutable()")]
    [InlineData("DROP TABLE perfil")]
    [InlineData("CREATE TABLE tabla_no_permitida (id int)")]
    public async Task SentenciasDenegadas_FallanConPermisoInsuficiente(string sql)
    {
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, await EjecutarComoApiAsync(sql));
    }

    [Theory]
    [InlineData("SELECT count(*) FROM vw_balance_saldos")]
    [InlineData("SELECT count(*) FROM vw_saldocuentaperiodo_vigente")]
    [InlineData("UPDATE periodocontable SET estado = estado WHERE false")]
    [InlineData("UPDATE documentocxc SET id = id WHERE false")]
    [InlineData("DELETE FROM detalleconciliacion WHERE false")]
    [InlineData("SELECT fn_perfil_contador()")]
    [InlineData("SELECT count(*) FROM fn_reporte_saldos(0, false)")]
    public async Task SentenciasPermitidas_NoFallan(string sql)
    {
        Assert.Null(await EjecutarComoApiAsync(sql));
    }

    [Fact]
    public async Task LlamadaAProcedimientoPermitido_Funciona()
    {
        var usuario = await _pg.Data.CrearUsuarioAsync();

        Assert.Null(await EjecutarComoApiAsync(
            $"CALL sp_registrar_auditoria({usuario}, 'prueba_permisos', 'perfil', 'x')"));
    }

    [Fact]
    public async Task SoloLasNueveRutinasPermitidasTienenExecute()
    {
        var reales = await ConsultarListaAsync(
            "SELECT p.oid::regprocedure::text FROM pg_proc p " +
            "WHERE p.pronamespace = 'public'::regnamespace AND has_function_privilege('delta_api', p.oid, 'EXECUTE')");

        Assert.Equal(
            RutinasPermitidas.Select(firma => firma.Replace(" ", "")).Order(),
            reales.Select(firma => firma.Replace(" ", "")).Order());
    }

    [Fact]
    public async Task TodaTablaDePublic_TieneDecisionExplicitaDePermisos()
    {
        var reales = await ConsultarListaAsync("SELECT tablename FROM pg_tables WHERE schemaname = 'public'");
        var tablas = reales.Where(tabla => tabla != "schema_migrations").ToList();

        Assert.Empty(tablas.Except(PermisosEsperados.Keys));
        Assert.Empty(PermisosEsperados.Keys.Except(tablas));

        var discrepancias = new List<string>();
        foreach (var (tabla, letrasEsperadas) in PermisosEsperados)
        {
            foreach (var (letra, privilegio) in Privilegios)
            {
                var concedido = await _pg.Data.ScalarAsync<bool>(
                    "SELECT has_table_privilege('delta_api', ('public.' || $1)::regclass, $2)", tabla, privilegio);
                if (concedido != letrasEsperadas.Contains(letra))
                {
                    discrepancias.Add($"{tabla}:{privilegio}");
                }
            }
        }

        Assert.Empty(discrepancias);
    }

    private async Task<List<string>> ConsultarListaAsync(string sql)
    {
        var filas = new List<string>();
        await using var command = _pg.DataSource.CreateCommand(sql);
        await using var lector = await command.ExecuteReaderAsync();
        while (await lector.ReadAsync())
        {
            filas.Add(lector.GetString(0));
        }
        return filas;
    }

    private async Task<string?> EjecutarComoApiAsync(string sql)
    {
        try
        {
            await using var command = _pg.ApiDataSource.CreateCommand(sql);
            await command.ExecuteNonQueryAsync();
            return null;
        }
        catch (PostgresException ex)
        {
            return ex.SqlState;
        }
    }
}
