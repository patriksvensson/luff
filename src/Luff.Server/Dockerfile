# syntax=docker/dockerfile:1

# Build context is the repo root (see compose.yaml) so the shared props + NuGet config are available.
# Pin the build stage to amd64: grpc.tools' linux_arm64 protoc segfaults (exit 139). A framework-dependent
# publish is portable IL + all-RID native assets, so the output still runs on the native final image below.
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG VERSION=0.0.0-dev
WORKDIR /src
COPY global.json NuGet.Config ./
COPY src/ ./src/
# UseBuildTimeTools=false skips MinVer/SourceLink/Roslynator (no .git in the context); the OpenAPI doc is
# committed, so don't re-emit it here.
RUN dotnet publish src/Luff.Server/Luff.Server.csproj \
      -c Release -o /app/publish \
      -p:UseBuildTimeTools=false \
      -p:OpenApiGenerateDocumentsOnBuild=false \
      -p:Version=$VERSION

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app

# luff.db + keys/ (Data-Protection ring + jwt.key) live under /data. Mount a volume there to persist state.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__Luff="Data Source=/data/luff.db"
COPY --from=build /app/publish ./
EXPOSE 8080 8081
HEALTHCHECK --interval=15s --timeout=3s --start-period=20s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "Luff.Server.dll"]
