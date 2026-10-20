# syntax=docker/dockerfile:1
# Single image: the API serves the REST endpoints, the SignalR hub and the built SPA (ADR-0013).

# ---- Frontend ----
FROM node:24-bookworm-slim AS web
WORKDIR /src/web
COPY web/package.json web/package-lock.json ./
RUN --mount=type=cache,target=/root/.npm npm ci
COPY web/ ./
RUN npm run build

# ---- Backend ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/ src/
# The OpenAPI document is already committed under web/openapi; skip regenerating it in the image build.
# The NuGet cache survives between builds, so native packages (OpenCV, Skia) download only once.
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish src/Salient.Api/Salient.Api.csproj -c Release -o /app -p:OpenApiGenerateDocuments=false
COPY --from=web /src/web/dist /app/wwwroot
COPY samples/ /app/samples/

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=api /app ./
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Salient__StorageRoot=/data \
    Salient__SamplesPath=/app/samples
VOLUME /data
EXPOSE 8080
ENTRYPOINT ["dotnet", "Salient.Api.dll"]
