using System.Text.Json.Serialization;

namespace DeltaERP.Domain.Entities;

public class PeriodoContable
{
    public const string EstadoAbierto = "Abierto";
    public const string EstadoCerrado = "Cerrado";
    public static readonly string[] EstadosValidos = { EstadoAbierto, EstadoCerrado };

    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public string Estado { get; set; } = "Abierto";

    // Lo incrementa sp_cerrar_periodo; no se serializa en los endpoints de periodos.
    [JsonIgnore]
    public int Cierres { get; set; }
}
