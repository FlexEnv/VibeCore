FROM node:22-bookworm-slim AS client-build
WORKDIR /src/VibeCoreWeb/ClientApp

COPY VibeCoreWeb/ClientApp/package.json VibeCoreWeb/ClientApp/package-lock.json ./
RUN npm ci

COPY VibeCoreWeb/ClientApp/ ./
RUN npm run generate-client
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS server-build
WORKDIR /src

COPY VibeCoreWeb/VibeCoreWeb.csproj VibeCoreWeb/
RUN dotnet restore VibeCoreWeb/VibeCoreWeb.csproj

COPY . .
COPY --from=client-build /src/VibeCoreWeb/wwwroot/app/ VibeCoreWeb/wwwroot/app/
RUN dotnet publish VibeCoreWeb/VibeCoreWeb.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false \
    /p:BuildClientApp=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=server-build /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "VibeCoreWeb.dll"]
