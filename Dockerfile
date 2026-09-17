# Multi-stage build for Bank API
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files first for optimal layer caching
COPY src/Bank.Api/Bank.Api.csproj Bank.Api/
COPY src/Bank.Application/Bank.Application.csproj Bank.Application/
COPY src/Bank.Domain/Bank.Domain.csproj Bank.Domain/
COPY src/Bank.Infrastructure/Bank.Infrastructure.csproj Bank.Infrastructure/
COPY NuGet.Config .

# Restore dependencies
RUN dotnet restore Bank.Api/Bank.Api.csproj

# Copy all source code
COPY src/ .

# Stage 2: Publish
WORKDIR /src/Bank.Api
RUN dotnet publish Bank.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime (minimal image)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install curl for health checks only
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

# Copy published application
COPY --from=build /app/publish .

# Expose API port
EXPOSE 5000

# Environment
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5000

# Health check
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1

# Run application
ENTRYPOINT ["dotnet", "Bank.Api.dll"]
