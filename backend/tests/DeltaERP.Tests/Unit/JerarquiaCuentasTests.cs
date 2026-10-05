using DeltaERP.Domain.Rules;

namespace DeltaERP.Tests.Unit;

public class JerarquiaCuentasTests
{
    private static readonly (int Id, int? PadreId)[] ArbolTresNiveles =
    {
        (1, null), (2, 1), (3, 1), (4, 2), (5, null),
    };

    private static readonly (int Id, int? PadreId)[] ArbolConCiclo =
    {
        (1, 2), (2, 1),
    };

    [Fact]
    public void IdsDescendientes_ArbolDeTresNiveles_DevuelveHijosYNietos()
    {
        Assert.Equal(new HashSet<int> { 2, 3, 4 }, JerarquiaCuentas.IdsDescendientes(ArbolTresNiveles, 1));
        Assert.Equal(new HashSet<int> { 4 }, JerarquiaCuentas.IdsDescendientes(ArbolTresNiveles, 2));
    }

    [Fact]
    public void IdsDescendientes_CuentaHojaOSinRelacion_DevuelveVacio()
    {
        Assert.Empty(JerarquiaCuentas.IdsDescendientes(ArbolTresNiveles, 4));
        Assert.Empty(JerarquiaCuentas.IdsDescendientes(ArbolTresNiveles, 5));
    }

    [Fact]
    public void IdsDescendientes_ConCiclo_TerminaYNoIncluyeLaRaiz()
    {
        Assert.Equal(new HashSet<int> { 2 }, JerarquiaCuentas.IdsDescendientes(ArbolConCiclo, 1));
    }
}
