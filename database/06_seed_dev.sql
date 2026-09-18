-- =====================================================================
-- 06_seed_dev.sql
-- Datos de arranque para desarrollo/demo local. NO usar en producción.
-- Contraseñas de ejemplo (mismo criterio "changeme_local_only" que el resto
-- del stack local, ver devops/docker-compose.yml):
--   admin@delta.com.gt     | AdminDelta26*
--   contador@delta.com.gt  | ContadorDelta26*
--   vendedor@delta.com.gt  | VendedorDelta26*
--   tecnico@delta.com.gt   | TecnicoDelta26*
-- Los hashes son PBKDF2 (Microsoft.AspNetCore.Identity.PasswordHasher, el
-- mismo que usa AuthController.cs), no se pueden editar a mano.
-- =====================================================================

INSERT INTO perfil (nombre) VALUES
    ('Administrador del sistema'),
    ('Contador'),
    ('Vendedor'),
    ('Técnico');

INSERT INTO usuario (nombre, email, password_hash, perfil_id, activo) VALUES
    ('Administradora Delta', 'admin@delta.com.gt',
     'AQAAAAIAAYagAAAAEGKCvKIj399G6a77sD9zwumqwwCTKYGil/nN5pf49utD/QxMOltZFqyu5IWQHId7rQ==',
     (SELECT id FROM perfil WHERE nombre = 'Administrador del sistema'), TRUE),
    ('Contador Delta', 'contador@delta.com.gt',
     'AQAAAAIAAYagAAAAEHXZGzgbV+cidI0mSYWmmDGoMr+UFn9prT9cKj5LeezLIlVB+dbikLpUOC7QXmZA/w==',
     (SELECT id FROM perfil WHERE nombre = 'Contador'), TRUE),
    ('Vendedor Delta', 'vendedor@delta.com.gt',
     'AQAAAAIAAYagAAAAEA/on4sGiCsRWOTlFynaCEdqyScorqe+gPMJ1qi7u6sydGQUIuxia5IoFSzfXNcTdg==',
     (SELECT id FROM perfil WHERE nombre = 'Vendedor'), TRUE),
    ('Técnico Delta', 'tecnico@delta.com.gt',
     'AQAAAAIAAYagAAAAEMlItHSAYRu4zNmuK0BeyXfF9tRECjSIzvM6skcbpqiCMIZz1uPB/3dfCr89kiPzsA==',
     (SELECT id FROM perfil WHERE nombre = 'Técnico'), TRUE);
