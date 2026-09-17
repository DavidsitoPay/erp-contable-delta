-- =====================================================================
-- 04_procedures.sql — procedimientos almacenados (ampliar en la Etapa 3 Backend)
-- =====================================================================

-- Ejemplo: cierre de periodo contable.
CREATE OR REPLACE PROCEDURE sp_cerrar_periodo(p_periodo_id INT, p_usuario_id INT)
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE periodocontable SET estado = 'Cerrado' WHERE id = p_periodo_id;

    -- El registro en bitacoraauditoria lo realiza la API (RN-08), no este procedimiento,
    -- para mantener consistente el origen del usuario_id.
END;
$$;
