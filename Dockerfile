# The store (Loja.Web) as one container image.
# Build: docker build -t loja .      Run locally with Postgres and Redis: docker compose up --build

# ---------- Build: the SDK compiles and publishes the web host ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Settings shared by every project (target framework, analyzers, package versions).
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/ src/

RUN dotnet publish src/Loja.Web/Loja.Web.csproj --configuration Release --output /app --nologo

# ---------- Runtime: only the ASP.NET Core runtime, no SDK ----------
# The Debian-based image ships ICU, so prices and dates format as pt-BR (R$ 1.234,56).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Npgsql (the Postgres driver) looks for the Kerberos/GSSAPI library when it negotiates the
# connection; the slim ASP.NET image does not include it and it logs an error on every start.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

# /data keeps what must survive a redeploy: uploaded product images and the Data Protection keys
# (sign-in cookies, antiforgery tokens). Mount a volume there.
RUN mkdir -p /data/uploads /data/keys && chown -R "$APP_UID" /data

ENV ASPNETCORE_HTTP_PORTS=8080 \
    # Hosting platforms end HTTPS at their proxy and forward plain HTTP with X-Forwarded-Proto:
    # this makes the app see the original scheme (redirects, Secure cookies).
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

COPY --from=build /app .

# The non-root user the .NET images provide.
USER $APP_UID
EXPOSE 8080
VOLUME /data

ENTRYPOINT ["dotnet", "Loja.Web.dll"]
