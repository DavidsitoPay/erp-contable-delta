using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Tests.Support;
using static DeltaERP.Tests.Support.RetencionPayloads;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class FeriadosControllerTests
{
    private const string Ruta = "/api/feriados";
    private const int Anio = 2090;

    private readonly PostgresFixture _pg;

    public FeriadosControllerTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    private async Task<HttpClient> AdminAsync() => _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync(), Roles.Administrador);

    [Fact]
    public async Task Administrador_CreaListaActualizaYElimina()
    {
        var client = await AdminAsync();
        var fecha = new DateOnly(Anio, 3, 1).Dia();

        var (creado, _) = await client.EnviarAsync(Ruta, new { fecha, nombre = "Feriado de prueba", medioDia = false });
        var (duplicado, _) = await client.EnviarAsync(Ruta, new { fecha, nombre = "Otro", medioDia = false });
        var actualizado = await client.PutAsJsonAsync($"{Ruta}/{fecha}", new { fecha, nombre = "Medio día de prueba", medioDia = true });
        var listado = (await client.GetFromJsonAsync<JsonElement>($"{Ruta}?anio={Anio}")).EnumerateArray().Single(f => f.GetProperty("fecha").GetString() == fecha);
        var eliminado = await client.DeleteAsync($"{Ruta}/{fecha}");
        var otraVez = await client.DeleteAsync($"{Ruta}/{fecha}");
        var inexistente = await client.PutAsJsonAsync($"{Ruta}/{new DateOnly(Anio, 3, 2).Dia()}", new { nombre = "x", medioDia = false });

        Assert.Equal(
            new[] { HttpStatusCode.Created, HttpStatusCode.Conflict, HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.NotFound, HttpStatusCode.NotFound },
            new[] { creado, duplicado, actualizado.StatusCode, eliminado.StatusCode, otraVez.StatusCode, inexistente.StatusCode });
        Assert.Equal(("Medio día de prueba", true), (listado.GetProperty("nombre").GetString(), listado.GetProperty("medioDia").GetBoolean()));
    }

    [Fact]
    public async Task Crear_SinNombre_Responde400()
    {
        var (estado, _) = await (await AdminAsync()).EnviarAsync(Ruta, new { fecha = new DateOnly(Anio, 4, 1).Dia(), nombre = " ", medioDia = false });

        Assert.Equal(HttpStatusCode.BadRequest, estado);
    }

    [Fact]
    public async Task Contador_LeePeroNoEscribe()
    {
        var contador = _pg.CreateApiClient(await _pg.Data.CrearUsuarioAsync());
        var fecha = new DateOnly(Anio, 5, 1).Dia();

        var lectura = await contador.GetAsync($"{Ruta}?anio={Anio}");
        var (alta, _) = await contador.EnviarAsync(Ruta, new { fecha, nombre = "x", medioDia = false });
        var cambio = await contador.PutAsJsonAsync($"{Ruta}/{fecha}", new { nombre = "x", medioDia = false });
        var baja = await contador.DeleteAsync($"{Ruta}/{fecha}");

        Assert.Equal(
            new[] { HttpStatusCode.OK, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden },
            new[] { lectura.StatusCode, alta, cambio.StatusCode, baja.StatusCode });
    }

    [Fact]
    public async Task Listar_SinAnioDevuelveTodosYAnioFueraDeRangoResponde400()
    {
        var client = await AdminAsync();

        var todos = await client.GetFromJsonAsync<JsonElement>(Ruta);
        var invalido = await client.GetAsync($"{Ruta}?anio=1800");

        Assert.True(todos.GetArrayLength() > 0);
        Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);
    }
}
