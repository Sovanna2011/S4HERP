# ---- build -------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH
WORKDIR /src

# Restore first so the layer is cached until the project file changes.
COPY ["Directory.Build.props", "Directory.Packages.props", "./"]
COPY ["src/", "src/"]
RUN dotnet restore "src/Host/S4HERP.Host/S4HERP.Host.csproj" -a "${TARGETARCH:-x64}"

COPY . .
RUN dotnet publish "src/Host/S4HERP.Host/S4HERP.Host.csproj" \
    -c "$BUILD_CONFIGURATION" \
    -a "${TARGETARCH:-x64}" \
    --no-restore \
    -o /app/publish

# ---- runtime -----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=1

COPY --from=build /app/publish .
# Post-migration SQL the ORM cannot express (RLS, partitioning, grants).
COPY db/scripts ./db/scripts

# Run as the non-root user baked into the .NET base images.
USER $APP_UID
EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=10s --start-period=20s --retries=5 \
    CMD ["dotnet", "S4HERP.Host.dll", "--healthcheck"]

ENTRYPOINT ["dotnet", "S4HERP.Host.dll"]
