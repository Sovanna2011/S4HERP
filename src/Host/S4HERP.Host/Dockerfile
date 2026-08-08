# ---- ui ----------------------------------------------------------------
# OpenUI5 is vendored through UI5 Tooling rather than loaded from the SAP CDN,
# so the running image needs no outbound network access at all (ADR-15).
#
# Two ways in, selected by UI_SOURCE:
#   build     (default) — UI5 Tooling fetches the framework and builds the bundle
#   prebuilt            — copy a bundle produced elsewhere, for a builder with no
#                         npm access: an air-gapped network, or one whose proxy
#                         intercepts TLS. Produce it with:
#                           npm --prefix ui5 install && npm --prefix ui5 run build
#
#   docker build --build-arg UI_SOURCE=prebuilt ...
ARG UI_SOURCE=build

# Node from MCR rather than Docker Hub: the .NET and SQL Server images already
# come from MCR, so the build needs one registry rather than two and is not
# subject to Docker Hub's anonymous pull limits.
FROM mcr.microsoft.com/devcontainers/javascript-node:22 AS ui-build
WORKDIR /ui
COPY ui5/package.json ui5/package-lock.json* ./
# No `2>/dev/null` on a fallback: swallowing the first command's output hides
# why it failed, and "npm ci or else npm install" then reports nothing useful.
#
# The CA mount is what makes this work behind a TLS-inspecting proxy — see the
# note above the build stage. Absent, the secret mounts as an empty file and the
# guard skips it, so the ordinary case costs nothing.
RUN --mount=type=secret,id=extra_ca,target=/tmp/extra-ca.crt \
    if [ -s /tmp/extra-ca.crt ]; then \
      cp /tmp/extra-ca.crt /usr/local/share/ca-certificates/extra-ca.crt && \
      update-ca-certificates && \
      npm config set cafile /etc/ssl/certs/ca-certificates.crt; \
    fi; \
    if [ -f package-lock.json ]; then npm ci --no-audit --no-fund; \
    else npm install --no-audit --no-fund; fi
COPY ui5/ ./
RUN npx ui5 build --all --clean-dest --dest /ui/dist

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS ui-prebuilt
COPY src/Host/S4HERP.Host/wwwroot /ui/dist

FROM ui-${UI_SOURCE} AS ui

# ---- build -------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH
WORKDIR /src

# Restore first so the layer is cached until the project file changes.
COPY ["Directory.Build.props", "Directory.Packages.props", "./"]
COPY ["src/", "src/"]

# Corporate networks routinely terminate and re-sign TLS, and every package
# restore then fails with "the remote certificate is invalid ... UntrustedRoot"
# (NuGet reports it as NU1301, which reads like the feed is down). Pass the
# proxy's root certificate and the build trusts it:
#
#   docker build --secret id=extra_ca,src=/path/to/corporate-root.crt ...
#   EXTRA_CA_BUNDLE=/path/to/corporate-root.crt docker compose build
#
# A secret rather than a COPY, so the certificate is never a layer in the image,
# and optional: unset, it mounts as an empty file and the guard skips it.
RUN --mount=type=secret,id=extra_ca,target=/tmp/extra-ca.crt \
    if [ -s /tmp/extra-ca.crt ]; then \
      cp /tmp/extra-ca.crt /usr/local/share/ca-certificates/extra-ca.crt && \
      update-ca-certificates; \
    fi; \
    dotnet restore "src/Host/S4HERP.Host/S4HERP.Host.csproj" -a "${TARGETARCH:-x64}"

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
COPY --from=ui /ui/dist ./wwwroot
# Post-migration SQL the ORM cannot express (RLS, partitioning, grants).
COPY db/scripts ./db/scripts

# Run as the non-root user baked into the .NET base images.
USER $APP_UID
EXPOSE 8080

# The probe asks for readiness, so the start period has to cover the whole of
# first-run setup: migrations, the post-migration scripts and, in Development,
# the sample seed. 20s covered process start and nothing else. Failures inside
# the start period do not mark the container unhealthy, so a generous window
# costs only how long a genuinely broken container takes to be called broken.
HEALTHCHECK --interval=15s --timeout=10s --start-period=180s --retries=5 \
    CMD ["dotnet", "S4HERP.Host.dll", "--healthcheck"]

ENTRYPOINT ["dotnet", "S4HERP.Host.dll"]
