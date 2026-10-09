namespace DeltaERP.Domain.Entities;

public class Moneda
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool EsFuncional { get; set; }
    public bool Activa { get; set; } = true;
}
