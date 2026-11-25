# egzamin-b1: one container with the .NET API and the built front end in wwwroot.
# Build: docker build --build-arg BUILD_VERSION=<git hash> -t egzamin-b1 .

FROM node:22-alpine AS web
ARG BUILD_VERSION=dev
ENV BUILD_VERSION=$BUILD_VERSION
WORKDIR /web
COPY src/Web/package.json src/Web/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY src/Web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY Directory.Build.props EgzaminB1.sln ./
COPY src/Domain/Domain.csproj src/Domain/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY src/Api/Api.csproj src/Api/
RUN dotnet restore src/Api/Api.csproj
COPY src/ src/
RUN dotnet publish src/Api/Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api /app ./
COPY --from=web /web/dist ./wwwroot
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "B1.Api.dll"]
