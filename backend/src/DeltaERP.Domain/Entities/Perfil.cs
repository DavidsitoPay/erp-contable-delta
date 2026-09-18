namespace DeltaERP.Domain.Entities;

/// <summary>
/// Perfil de acceso (rol) de un usuario. Controla autorización por módulo (RN-09)
/// y qué operaciones sensibles puede ejecutar (ver sp_cerrar_periodo,
/// sp_finalizar_conciliacion en database/04_procedures.sql).
/// </summary>
public class Perfil
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}
