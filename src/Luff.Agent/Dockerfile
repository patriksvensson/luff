# syntax=docker/dockerfile:1

# Pin the build stage to amd64: grpc.tools' linux_arm64 protoc segfaults (exit 139). The framework-dependent
# publish output is portable and runs on the native final image below.
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json NuGet.Config ./
COPY src/ ./src/
RUN dotnet publish src/Luff.Agent/Luff.Agent.csproj \
      -c Release -o /app/publish \
      -p:UseBuildTimeTools=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
# The agent shells out to `docker` / `docker compose` (against the mounted host socket). The docker CLI and the
# compose v2 plugin are static Go binaries, so they run on the Debian-based .NET runtime image.
COPY --from=docker:cli /usr/local/bin/docker /usr/local/bin/docker
COPY --from=docker:cli /usr/local/libexec/docker/cli-plugins/docker-compose /usr/local/libexec/docker/cli-plugins/docker-compose
WORKDIR /app
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "Luff.Agent.dll"]
