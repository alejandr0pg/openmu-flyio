#!/bin/sh
set -e

DB_HOST="${DB_HOST:-localhost}"
DB_PORT="${DB_PORT:-5432}"
DB_NAME="${DB_NAME:-openmu}"
DB_ADMIN_USER="${DB_ADMIN_USER:-postgres}"
DB_ADMIN_PW="${DB_ADMIN_PW:-admin}"
DB_SSL="${DB_SSL:-Prefer}"
DB_TIMEOUT="${DB_TIMEOUT:-120}"

CONN_BASE="Server=${DB_HOST};Port=${DB_PORT};Database=${DB_NAME};Command Timeout=${DB_TIMEOUT};SSL Mode=${DB_SSL};Trust Server Certificate=true"
ADMIN_CONN="${CONN_BASE};User Id=${DB_ADMIN_USER};Password=${DB_ADMIN_PW}"

cat > /app/openmu/ConnectionSettings.xml <<XMLEOF
<?xml version="1.0" encoding="utf-8" ?>
<ConnectionSettings xmlns="http://www.munique.net/ConnectionSettings">
  <Connections>
    <Connection>
      <ContextTypeName>MUnique.OpenMU.Persistence.EntityFramework.EntityDataContext</ContextTypeName>
      <ConnectionString>${ADMIN_CONN}</ConnectionString>
      <DatabaseEngine>Npgsql</DatabaseEngine>
    </Connection>
    <Connection>
      <ContextTypeName>MUnique.OpenMU.Persistence.EntityFramework.TypedContext</ContextTypeName>
      <ConnectionString>${ADMIN_CONN}</ConnectionString>
      <DatabaseEngine>Npgsql</DatabaseEngine>
    </Connection>
    <Connection>
      <ContextTypeName>MUnique.OpenMU.Persistence.EntityFramework.ConfigurationContext</ContextTypeName>
      <ConnectionString>${ADMIN_CONN}</ConnectionString>
      <DatabaseEngine>Npgsql</DatabaseEngine>
    </Connection>
    <Connection>
      <ContextTypeName>MUnique.OpenMU.Persistence.EntityFramework.AccountContext</ContextTypeName>
      <ConnectionString>${ADMIN_CONN}</ConnectionString>
      <DatabaseEngine>Npgsql</DatabaseEngine>
    </Connection>
    <Connection>
      <ContextTypeName>MUnique.OpenMU.Persistence.EntityFramework.TradeContext</ContextTypeName>
      <ConnectionString>${ADMIN_CONN}</ConnectionString>
      <DatabaseEngine>Npgsql</DatabaseEngine>
    </Connection>
    <Connection>
      <ContextTypeName>MUnique.OpenMU.Persistence.EntityFramework.FriendContext</ContextTypeName>
      <ConnectionString>${ADMIN_CONN}</ConnectionString>
      <DatabaseEngine>Npgsql</DatabaseEngine>
    </Connection>
    <Connection>
      <ContextTypeName>MUnique.OpenMU.Persistence.EntityFramework.GuildContext</ContextTypeName>
      <ConnectionString>${ADMIN_CONN}</ConnectionString>
      <DatabaseEngine>Npgsql</DatabaseEngine>
    </Connection>
  </Connections>
</ConnectionSettings>
XMLEOF

echo "ConnectionSettings.xml generated: Host=${DB_HOST} Port=${DB_PORT} SSL=${DB_SSL}"

# Build DB connection string for RegistrationApi
export DB_CONNECTION="Host=${DB_HOST};Port=${DB_PORT};Username=${DB_ADMIN_USER};Password=${DB_ADMIN_PW};Database=${DB_NAME};SSL Mode=${DB_SSL};Timeout=30;Trust Server Certificate=true"

# Start RegistrationApi on port 8081 (background)
echo "Starting RegistrationApi on port 8081..."
cd /app/regapi
ASPNETCORE_URLS=http://+:8081 dotnet RegistrationApi.dll &
REGAPI_PID=$!
cd /app

# Start OpenMU on port 8082 (background)
echo "Starting OpenMU on port 8082..."
cd /app/openmu
ASPNETCORE_URLS=http://+:8082 dotnet MUnique.OpenMU.Startup.dll "$@" &
OPENMU_PID=$!
cd /app

# Start Caddy reverse proxy on port 8080 (foreground-ish)
echo "Starting Caddy reverse proxy on port 8080..."
caddy run --config /app/Caddyfile --adapter caddyfile &
CADDY_PID=$!

# Wait for any process to exit, then stop all
wait_and_cleanup() {
    echo "Shutting down all services..."
    kill $CADDY_PID $REGAPI_PID $OPENMU_PID 2>/dev/null || true
    wait
}

trap wait_and_cleanup SIGTERM SIGINT

# Wait for OpenMU (main process)
wait $OPENMU_PID
EXIT_CODE=$?
wait_and_cleanup
exit $EXIT_CODE
