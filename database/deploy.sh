#!/usr/bin/env bash
# Deploys the NorthstarGoods database with sqlcmd. Safe to re-run.
#
#   1. 000_create_database.sql      every run (idempotent)
#   2. migrations/*.sql             once each, recorded in dbo.SchemaVersions
#   3. programmability/*.sql        every run (CREATE OR ALTER views and procedures)
#   4. security/*.sql               every run (application login, roles, grants)
#   5. seed/*.sql                   once each, only when SEED_DEMO_DATA=true
#
# Environment: DB_HOST, DB_NAME, SQLCMDPASSWORD (sa password), APP_LOGIN, APP_LOGIN_PASSWORD, SEED_DEMO_DATA
set -euo pipefail

cd "$(dirname "$0")"
SQLCMD=${SQLCMD:-/opt/mssql-tools18/bin/sqlcmd}
DB_HOST=${DB_HOST:-localhost}
DB_NAME=${DB_NAME:-NorthstarGoods}
APP_LOGIN=${APP_LOGIN:-northstar_app}
SEED_DEMO_DATA=${SEED_DEMO_DATA:-false}
: "${SQLCMDPASSWORD:?SQLCMDPASSWORD (the sa password) is required}"
: "${APP_LOGIN_PASSWORD:?APP_LOGIN_PASSWORD is required}"
export SQLCMDPASSWORD
# sqlcmd resolves $(DatabaseName) etc. in the scripts from environment variables, so no secret is on the command line.
export DatabaseName=$DB_NAME AppLoginName=$APP_LOGIN AppLoginPassword=$APP_LOGIN_PASSWORD

# -b: stop on error  -I: QUOTED_IDENTIFIER ON (required for filtered indexes)  -C: trust the dev server certificate
sql() {
  "$SQLCMD" -S "$DB_HOST" -U sa -C -b -I "$@"
}

echo "Waiting for SQL Server at $DB_HOST..."
for _ in $(seq 1 60); do
  if sql -d master -Q "SET NOCOUNT ON; SELECT 1" >/dev/null 2>&1; then break; fi
  sleep 2
done
sql -d master -Q "SET NOCOUNT ON; SELECT 1" >/dev/null

echo "[always] 000_create_database.sql"
sql -d master -i 000_create_database.sql

apply_once() {
  local file name applied
  for file in "$@"; do
    [ -f "$file" ] || continue
    name="${file#./}"
    applied=$(sql -d "$DB_NAME" -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.SchemaVersions WHERE ScriptName = N'$name'" | tr -d '[:space:]')
    if [ "$applied" = "0" ]; then
      echo "[apply]  $name"
      sql -d "$DB_NAME" -i "$file"
      sql -d "$DB_NAME" -Q "SET NOCOUNT ON; INSERT dbo.SchemaVersions (ScriptName) VALUES (N'$name')"
    else
      echo "[skip]   $name (already applied)"
    fi
  done
}

apply_once migrations/*.sql

for file in programmability/views.sql programmability/procedures.sql; do
  echo "[always] $file"
  sql -d "$DB_NAME" -i "$file"
done

echo "[always] security/app_login.sql"
sql -d master -i security/app_login.sql

if [ "$SEED_DEMO_DATA" = "true" ]; then
  apply_once seed/*.sql
fi

sql -d "$DB_NAME" -h -1 -W -Q "SET NOCOUNT ON;
SELECT CONCAT('Database ready: ', (SELECT COUNT(*) FROM auth.Users), ' users, ', (SELECT COUNT(*) FROM sales.Orders), ' orders, ',
              (SELECT COUNT(*) FROM payment.PaymentAttempts), ' payment attempts.');"
