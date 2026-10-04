using System.Net.Http.Json;
using System.Text.Json;

namespace DeltaERP.Tests.Support;

public static class JsonExtensions
{
    public static async Task<JsonElement> LeerJsonAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    public static async Task<string?> LeerErrorAsync(this HttpResponseMessage response) =>
        (await response.LeerJsonAsync()).GetProperty("error").GetString();

    public static HashSet<int> Ids(this JsonElement arreglo) =>
        arreglo.EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).ToHashSet();
}
