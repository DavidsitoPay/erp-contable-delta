namespace DeltaERP.Domain.Entities;

// MedioDia = true: dia habil (informativo); false: dia inhabil.
public class Feriado
{
    public DateOnly Fecha { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool MedioDia { get; set; }
    public int? CreadoPor { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}
