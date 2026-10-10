using System.Security.Cryptography;
using DeltaERP.Api.Auth;
using Npgsql;

namespace DeltaERP.Tests.Support;

public sealed class PostgresFixture : IAsyncLifetime
{
    public const string ConnectionVariable = "TEST_PG_CONN";

    private static readonly string[] ScriptsSql =
    {
        "01_tables.sql",
        "02_functions.sql",
        "03_triggers.sql",
        "04_procedures.sql",
        "05_views.sql",
        "07_perfil_autorizado.sql",
        "08_correcciones_balance_y_cierre.sql",
        "09_tesoreria.sql",
        "10_balance_jerarquico.sql",
        "11_reportes.sql",
        "12_reversa_asientos.sql",
        "13_fiscal_iva.sql",
        "14_rol_api.sql",
    };

    public NpgsqlDataSource DataSource { get; private set; } = null!;
    public NpgsqlDataSource ApiDataSource { get; private set; } = null!;
    public ApiFactory Factory { get; private set; } = null!;
    public TestData Data { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"La variable de entorno {ConnectionVariable} no está definida. Las pruebas de integración " +
                "requieren un PostgreSQL real; excluya la categoría con --filter \"Category!=Integration\".");
        }

        var database = new NpgsqlConnectionStringBuilder(connectionString).Database ?? string.Empty;
        if (!database.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{ConnectionVariable} apunta a la base '{database}'; el nombre debe contener 'test' porque el esquema public se recrea.");
        }

        DataSource = NpgsqlDataSource.Create(connectionString);

        await EjecutarAsync("DROP SCHEMA IF EXISTS public CASCADE; CREATE SCHEMA public;");
        var directorioSql = LocalizarDirectorioDatabase();
        foreach (var script in ScriptsSql)
        {
            var sql = await File.ReadAllTextAsync(Path.Combine(directorioSql, script));
            if (TieneSentencias(sql))
            {
                await EjecutarAsync(sql);
            }
        }

        var contrasenaApi = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        await EjecutarAsync($"ALTER ROLE delta_api WITH LOGIN PASSWORD '{contrasenaApi}'");
        var cadenaApi = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Username = "delta_api",
            Password = contrasenaApi,
        }.ConnectionString;
        ApiDataSource = NpgsqlDataSource.Create(cadenaApi);

        Data = new TestData(DataSource);
        await Data.SembrarConfiguracionFiscalAsync();
        Factory = new ApiFactory(cadenaApi);
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }
        if (ApiDataSource is not null)
        {
            await ApiDataSource.DisposeAsync();
        }
        if (DataSource is not null)
        {
            await DataSource.DisposeAsync();
        }
    }

    public HttpClient CreateApiClient(int usuarioId, string rol = Roles.Contador)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, usuarioId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, rol);
        return client;
    }

    private async Task EjecutarAsync(string sql)
    {
        await using var command = DataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }

    private static bool TieneSentencias(string sql) =>
        sql.Split('\n').Any(linea => linea.Trim().Length > 0 && !linea.TrimStart().StartsWith("--", StringComparison.Ordinal));

    private static string LocalizarDirectorioDatabase()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null)
        {
            var candidato = Path.Combine(directorio.FullName, "database");
            if (File.Exists(Path.Combine(candidato, "01_tables.sql")))
            {
                return candidato;
            }
            directorio = directorio.Parent;
        }
        throw new DirectoryNotFoundException("No se encontró el directorio database/ subiendo desde " + AppContext.BaseDirectory);
    }
}
