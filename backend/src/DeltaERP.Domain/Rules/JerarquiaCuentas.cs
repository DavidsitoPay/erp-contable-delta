namespace DeltaERP.Domain.Rules;

public static class JerarquiaCuentas
{
    public static HashSet<int> IdsDescendientes(IEnumerable<(int Id, int? PadreId)> catalogo, int raizId)
    {
        var hijosPorPadre = catalogo
            .Where(c => c.PadreId.HasValue)
            .ToLookup(c => c.PadreId.GetValueOrDefault(), c => c.Id);
        var resultado = new HashSet<int>();
        var pendientes = new Queue<int>();
        pendientes.Enqueue(raizId);
        while (pendientes.TryDequeue(out var actual))
        {
            foreach (var hijo in hijosPorPadre[actual].Where(resultado.Add))
            {
                pendientes.Enqueue(hijo);
            }
        }
        resultado.Remove(raizId);
        return resultado;
    }
}
