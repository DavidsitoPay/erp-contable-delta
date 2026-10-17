-- 19_feriado_sat_inhabil_2026.sql
-- Dia inhabil SAT del lunes 2026-10-19 (Resolucion SAT-DSI-1648-2026). No se edita un script ya aplicado.

INSERT INTO feriado (fecha, nombre, medio_dia) VALUES
    (DATE '2026-10-19', 'Dia inhabil SAT (Resolucion SAT-DSI-1648-2026)', FALSE)
ON CONFLICT (fecha) DO NOTHING;
