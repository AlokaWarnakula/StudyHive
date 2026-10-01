# StudyHive API — multi-stage .NET 8 build. Build context: api/
#   docker build -t studyhive-api api
# Secrets (ConnectionStrings__Default, Jwt__SigningKey, Agent__InternalApiKey, Brevo__*) are
# runtime environment variables only; nothing secret is copied into the image.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/StudyHive.Api/StudyHive.Api.csproj src/StudyHive.Api/
RUN dotnet restore src/StudyHive.Api/StudyHive.Api.csproj
COPY src/ src/
RUN dotnet publish src/StudyHive.Api/StudyHive.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
USER $APP_UID
# Railway (and similar hosts) inject PORT at run time; default 8080 locally.
ENTRYPOINT ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-8080} exec dotnet StudyHive.Api.dll"]
