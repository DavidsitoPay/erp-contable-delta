-- =====================================================================
-- 06_seed_dev.sql
-- Datos de arranque para desarrollo/demo local. NO usar en producción.
-- Contraseñas rotadas (las originales quedaron expuestas en texto plano en
-- README.md, commit 37278ab, repo público) — ver CREDENTIALS.md, que no se
-- sube al repositorio. Los hashes son PBKDF2
-- (Microsoft.AspNetCore.Identity.PasswordHasher, el mismo que usa
-- AuthController.cs), no se pueden editar a mano.
-- =====================================================================

INSERT INTO perfil (nombre) VALUES
    ('Administrador del sistema'),
    ('Contador'),
    ('Vendedor'),
    ('Técnico');

INSERT INTO usuario (nombre, email, password_hash, perfil_id, activo) VALUES
    ('Administradora Delta', 'admin@delta.com.gt',
     'AQAAAAIAAYagAAAAEDMa/YlzTCyZLnyyyat8+7VUKawhuKUdXUEEQ05r0Ud3jZPGoPbWhzr9noBLcM5RkA==',
     (SELECT id FROM perfil WHERE nombre = 'Administrador del sistema'), TRUE),
    ('Contador Delta', 'contador@delta.com.gt',
     'AQAAAAIAAYagAAAAEEuBbOyV53SI0+jcaqi6hAb02wS/VtSRPr4zBpbnu5n5UVFiS7m1iJ3r6PIaFgQcEg==',
     (SELECT id FROM perfil WHERE nombre = 'Contador'), TRUE),
    ('Vendedor Delta', 'vendedor@delta.com.gt',
     'AQAAAAIAAYagAAAAEOu1CV5Zvjy4jH8Rh1DFVa7M4n3W4uoYpiW64EWy25rDgissmtXw+rxYnVQilUurIw==',
     (SELECT id FROM perfil WHERE nombre = 'Vendedor'), TRUE),
    ('Técnico Delta', 'tecnico@delta.com.gt',
     'AQAAAAIAAYagAAAAENfrqvGl2ZOidUskyWPHwS1lv6Pj08SQUcBm6DgF2Y3pmHO77GvbXNucV5/OyDGdwg==',
     (SELECT id FROM perfil WHERE nombre = 'Técnico'), TRUE);
