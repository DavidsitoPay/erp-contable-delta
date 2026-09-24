#!/usr/bin/env bash
# =====================================================================
# run_migrations.sh — aplica los scripts SQL de database/ que aún no se
# hayan corrido contra la conexión indicada, en orden, uno por uno,
# dentro de una transacción cada uno. Lleva registro en la tabla
# schema_migrations (creada automáticamente si no existe).
#
# Uso:
#   ./run_migrations.sh "<connection string de Postgres>"
#
# Convención: los scripts van numerados database/NN_descripcion.sql
# (NN de dos dígitos, ver 01_tables.sql...06_seed_dev.sql como
# ejemplo). Un script ya aplicado nunca se vuelve a correr — por eso
# 01_tables.sql (que no es idempotente: sin IF NOT EXISTS) puede vivir
# junto a scripts futuros sin romperse en un re-run.
#
# No hay "down" migrations (rollback): es una decisión deliberada por
# simplicidad para el tamaño de este proyecto. Si un script queda mal,
# se corrige con un script NUEVO (NN+1), nunca editando uno ya aplicado
# y ya corrido en algún entorno.
# =====================================================================
set -euo pipefail

RAW_CONN_STRING="${1:?Uso: run_migrations.sh \"<connection string>\"}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# El resto del proyecto (appsettings.json, secrets de la Container App) usa
# el formato de connection string de Npgsql/.NET: "Host=...;Database=...;
# Username=...;Password=...;Ssl Mode=Require". psql no entiende ese formato
# (espera una URI libpq: postgresql://user:pass@host/db?sslmode=require, o
# pares host=... dbname=... en minúsculas). En vez de mantener el mismo
# valor duplicado en dos formatos distintos como dos secrets separados
# (se desincronizan tarde o temprano), este script convierte automáticamente
# si detecta el formato Npgsql; si ya viene como URI postgresql://, la usa
# tal cual.
if [[ "$RAW_CONN_STRING" == postgresql://* || "$RAW_CONN_STRING" == postgres://* ]]; then
    CONN_STRING="$RAW_CONN_STRING"
else
    HOST="" DBNAME="" DBUSER="" DBPASSWORD="" SSLMODE="prefer"
    IFS=';' read -ra PAIRS <<< "$RAW_CONN_STRING"
    for PAIR in "${PAIRS[@]}"; do
        [ -z "$PAIR" ] && continue
        KEY="${PAIR%%=*}"
        VAL="${PAIR#*=}"
        # normaliza la clave: minúsculas y sin espacios ("Ssl Mode" -> "sslmode")
        KEY_NORM="$(echo "$KEY" | tr '[:upper:]' '[:lower:]' | tr -d ' ')"
        case "$KEY_NORM" in
            host) HOST="$VAL" ;;
            database) DBNAME="$VAL" ;;
            username) DBUSER="$VAL" ;;
            password) DBPASSWORD="$VAL" ;;
            sslmode) SSLMODE="$(echo "$VAL" | tr '[:upper:]' '[:lower:]')" ;;
        esac
    done
    if [ -z "$HOST" ] || [ -z "$DBNAME" ] || [ -z "$DBUSER" ]; then
        echo "Error: no se pudo interpretar la connection string (¿formato inesperado?)." >&2
        exit 1
    fi
    CONN_STRING="postgresql://${DBUSER}:${DBPASSWORD}@${HOST}/${DBNAME}?sslmode=${SSLMODE}"
fi

echo "== Verificando tabla schema_migrations =="
psql "$CONN_STRING" -v ON_ERROR_STOP=1 -c "
CREATE TABLE IF NOT EXISTS schema_migrations (
    filename    TEXT PRIMARY KEY,
    applied_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);"

APPLIED_ANY=0

for FILE in "$SCRIPT_DIR"/[0-9][0-9]_*.sql; do
    NAME="$(basename "$FILE")"

    ALREADY=$(psql "$CONN_STRING" -tA -c \
        "SELECT 1 FROM schema_migrations WHERE filename = '$NAME';")

    if [ "$ALREADY" = "1" ]; then
        echo "-- $NAME ya aplicado, se omite"
        continue
    fi

    echo "== Aplicando $NAME =="
    # ON_ERROR_STOP + una sola sesión de psql por archivo: si el script
    # falla a mitad de camino, esa sesión aborta (nada queda a medias en
    # cuanto a la transacción implícita que psql abre para un -f), el
    # pipeline se detiene aquí (set -e) y el archivo NO se marca como
    # aplicado, así que el siguiente run lo vuelve a intentar.
    psql "$CONN_STRING" -v ON_ERROR_STOP=1 -f "$FILE"

    psql "$CONN_STRING" -v ON_ERROR_STOP=1 -c \
        "INSERT INTO schema_migrations (filename) VALUES ('$NAME');"

    echo "== $NAME aplicado y registrado =="
    APPLIED_ANY=1
done

if [ "$APPLIED_ANY" = "0" ]; then
    echo "== Nada nuevo que aplicar =="
fi
