# Stage 1: Build the React PWA frontend
FROM node:20-alpine AS frontend-build
WORKDIR /frontend

COPY src/BaggageDelivery.PaxPortal/package*.json ./
RUN npm ci --silent

# Build-arg baked into the JS bundle. For single-container, use a relative path.
ARG VITE_API_URL=/api/v1
ENV VITE_API_URL=$VITE_API_URL

COPY src/BaggageDelivery.PaxPortal/ ./
RUN npm run build

# Stage 2: Build the .NET API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /App

COPY *.slnx Directory.Build.props Directory.Packages.props NuGet.Config global.json ./
COPY src/BaggageDelivery.Core/*.csproj ./src/BaggageDelivery.Core/
COPY src/BaggageDelivery.Api/*.csproj ./src/BaggageDelivery.Api/

RUN dotnet restore src/BaggageDelivery.Api/BaggageDelivery.Api.csproj

COPY src/BaggageDelivery.Core/ ./src/BaggageDelivery.Core/
COPY src/BaggageDelivery.Api/ ./src/BaggageDelivery.Api/

RUN dotnet build src/BaggageDelivery.Api/BaggageDelivery.Api.csproj -c Release --no-restore
RUN dotnet publish src/BaggageDelivery.Api/BaggageDelivery.Api.csproj -c Release --no-build -o /publish

COPY --from=frontend-build /frontend/dist /publish/wwwroot

# Stage 3: Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build-env --chown=app:app /publish .

USER app

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD wget --spider -q http://localhost:8080/healthz || exit 1
ENTRYPOINT ["dotnet", "BaggageDelivery.Api.dll"]
