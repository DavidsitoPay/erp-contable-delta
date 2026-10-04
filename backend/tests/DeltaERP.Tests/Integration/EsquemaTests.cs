using DeltaERP.Domain.Entities;
using DeltaERP.Infrastructure.Data;
using DeltaERP.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class EsquemaTests
{
    private static readonly Type[] EntidadesMapeadas = { typeof(AsientoContable), typeof(LineaAsiento) };

    private readonly PostgresFixture _pg;

    public EsquemaTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    [Fact]
    public async Task ColumnasMapeadasDeAsientoYLineas_ExistenEnLaBaseReal()
    {
        using var scope = _pg.Factory.Services.CreateScope();
        var modelo = scope.ServiceProvider.GetRequiredService<DeltaErpDbContext>().Model;
        var faltantes = new List<string>();

        foreach (var tipo in EntidadesMapeadas)
        {
            var entidad = modelo.FindEntityType(tipo)!;
            var tabla = entidad.GetTableName()!;
            var esquema = entidad.GetSchema() ?? "public";
            var objeto = StoreObjectIdentifier.Table(tabla, entidad.GetSchema());
            var propiedades = entidad.GetProperties().ToList();
            Assert.NotEmpty(propiedades);

            foreach (var propiedad in propiedades)
            {
                var columna = propiedad.GetColumnName(objeto)!;
                var existe = await _pg.Data.ScalarAsync<long>(
                    "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = $1 AND table_name = $2 AND column_name = $3",
                    esquema, tabla, columna);
                if (existe != 1)
                {
                    faltantes.Add($"{tabla}.{columna}");
                }
            }
        }

        Assert.Empty(faltantes);
    }
}
