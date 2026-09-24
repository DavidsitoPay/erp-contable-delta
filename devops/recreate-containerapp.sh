#!/usr/bin/env bash
# =====================================================================
# recreate-containerapp.sh — fuente de verdad para (re)crear
# ca-delta-erp-backend desde cero.
#
# Por qué existe: el tier "Consumption / express environment" de Azure
# Container Apps que usa este proyecto no recicla la réplica en vivo de
# forma confiable cuando solo cambia un secret o una env var (probado
# repetidas veces: `az containerapp update`, `revision restart` y
# `revision deactivate` fallan o no aplican el cambio). La única forma
# confiable de aplicar un cambio de config (CORS, connection string, JWT
# key) es borrar y recrear la Container App. `az containerapp update
# --image ...` (lo que hace .github/workflows/backend-deploy.yml en cada
# merge a main) SÍ es confiable y NO requiere este script — este script
# es solo para cuando cambia la CONFIGURACIÓN, no el código.
#
# Antes de este archivo, la lista completa de env vars/CORS solo vivía en
# el historial de la conversación — se olvidó un dominio más de una vez
# al recrear a mano. Esto es la fuente única de verdad, versionada.
#
# Uso:
#   export NEON_PROD_CONNECTION_STRING="Host=...;Database=...;Username=...;Password=...;Ssl Mode=Require"
#   export JWT_SIGNING_KEY="..."
#   ./recreate-containerapp.sh
#
# Ambos valores están en CREDENTIALS.md (no se sube al repo). Si agregas
# un dominio nuevo de frontend, agrégalo también a CORS_ORIGINS abajo.
# =====================================================================
set -euo pipefail

: "${NEON_PROD_CONNECTION_STRING:?Falta NEON_PROD_CONNECTION_STRING (ver CREDENTIALS.md)}"
: "${JWT_SIGNING_KEY:?Falta JWT_SIGNING_KEY (ver CREDENTIALS.md)}"

RESOURCE_GROUP="rg-delta-erp"
APP_NAME="ca-delta-erp-backend"
ENVIRONMENT="cae-delta-erp"
# Imagen: la que ya esté corriendo hoy, o la que pases como primer
# argumento (útil si estás recreando justo después de un deploy nuevo).
IMAGE="${1:-ghcr.io/davidsitopay/erp-contable-delta-backend:35d5ebff7daacf55bebf6b83c4cf117be1c3a252}"

# Todos los orígenes de frontend válidos. Vercel genera automáticamente el
# dominio largo (-davidsito-team) y los de rama (-git-main-/-git-dev-); el
# dominio corto (delta-erp-contable.vercel.app) lo gestiona
# .github/workflows/frontend-alias.yml.
CORS_ORIGINS="http://localhost:3000,https://delta-erp-contable.vercel.app,https://delta-erp-contable-davidsito-team.vercel.app,https://delta-erp-contable-git-main-davidsito-team.vercel.app,https://delta-erp-contable-git-dev-davidsito-team.vercel.app"

echo "== Borrando $APP_NAME (si existe) =="
az containerapp delete --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" --yes || true
sleep 10

echo "== Creando $APP_NAME con imagen $IMAGE =="
az containerapp create \
  --name "$APP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --environment "$ENVIRONMENT" \
  --image "$IMAGE" \
  --target-port 5000 \
  --ingress external \
  --min-replicas 0 \
  --max-replicas 2 \
  --cpu 0.25 --memory 0.5Gi \
  --secrets \
    "connectionstring-default=${NEON_PROD_CONNECTION_STRING}" \
    "jwt-key=${JWT_SIGNING_KEY}" \
  --env-vars \
    "ASPNETCORE_ENVIRONMENT=Production" \
    "ConnectionStrings__Default=secretref:connectionstring-default" \
    "Jwt__Key=secretref:jwt-key" \
    "Cors__AllowedOrigins=${CORS_ORIGINS}"

echo "== Listo. Verificando =="
FQDN=$(az containerapp show --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" --query "properties.configuration.ingress.fqdn" -o tsv)
sleep 15
curl -s -o /dev/null -w "health: HTTP %{http_code}\n" "https://${FQDN}/health"
