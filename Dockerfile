# Stage 1: Build OpenMU from source
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build-openmu
WORKDIR /src
COPY OpenMU/src/Directory.Packages.props .
COPY OpenMU/src/Directory.Build.props .
COPY OpenMU/src/Startup/MUnique.OpenMU.Startup.csproj Startup/
RUN dotnet restore "Startup/MUnique.OpenMU.Startup.csproj"
COPY OpenMU/src/ .
WORKDIR /src/Startup
RUN dotnet publish "MUnique.OpenMU.Startup.csproj" -c Release -o /app/publish -p:ci=true

# Stage 2: Build RegistrationApi
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build-regapi
WORKDIR /src
COPY registration-api/RegistrationApi.csproj .
RUN dotnet restore
COPY registration-api/ .
RUN dotnet publish -c Release -o /app/publish

# Stage 3: Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final

# Install Caddy, Kerberos libs (needed by Npgsql), and netcat for port checks
RUN apk add --no-cache caddy krb5-libs netcat-openbsd

WORKDIR /app
COPY --from=build-openmu /app/publish ./openmu/
COPY --from=build-regapi /app/publish ./regapi/
COPY entrypoint.sh /app/entrypoint.sh
COPY Caddyfile /app/Caddyfile
RUN chmod +x /app/entrypoint.sh && \
    mkdir -p /app/openmu/logs && \
    chmod 777 /app/openmu/logs && \
    chmod 777 /app/openmu/ConnectionSettings.xml

EXPOSE 8080 44405 55901 55902 55980

USER $APP_UID
ENTRYPOINT ["/app/entrypoint.sh"]
CMD ["-autostart", "-resolveIP:168.220.89.83"]
