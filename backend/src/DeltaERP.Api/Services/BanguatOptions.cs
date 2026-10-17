namespace DeltaERP.Api.Services;

public sealed class BanguatOptions
{
    public const string SectionName = "Banguat";

    public string Endpoint { get; set; } = "https://www.banguat.gob.gt/variables/ws/TipoCambio.asmx";
    public int TimeoutSegundos { get; set; } = 5;
    public int Reintentos { get; set; } = 1;
    public int EsperaReintentoMs { get; set; } = 300;
    public int VentanaDias { get; set; } = 10;
}
