# Stage 1: Build and create EF Bundle
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Install EF Core tools
RUN dotnet tool install --global dotnet-ef
ENV PATH="${PATH}:/root/.dotnet/tools"

# Copy solution and project files
COPY ["src/RAG.API/RAG.API.csproj", "src/RAG.API/"]
COPY ["src/RAG.Application/RAG.Application.csproj", "src/RAG.Application/"]
COPY ["src/RAG.Core/RAG.Core.csproj", "src/RAG.Core/"]
COPY ["src/RAG.Infrastructure/RAG.Infrastructure.csproj", "src/RAG.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "src/RAG.Infrastructure/RAG.Infrastructure.csproj"

# Copy all source code
COPY . .

# Build the infrastructure project
WORKDIR "/src/src/RAG.Infrastructure"
RUN dotnet build "RAG.Infrastructure.csproj" -c Release

# Create EF migrations bundle
# Note: This creates a self-contained executable that applies migrations
RUN dotnet ef migrations bundle \
    --project "RAG.Infrastructure.csproj" \
    --startup-project "../RAG.API/RAG.API.csproj" \
    --configuration Release \
    --output /app/efbundle \
    --self-contained \
    --runtime linux-x64 \
    --force

# Stage 2: Runtime (minimal image)
FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine AS final

# Create non-root user
RUN addgroup -S migrationuser && adduser -S migrationuser -G migrationuser

WORKDIR /app

# Copy the migrations bundle
COPY --from=build /app/efbundle .

# Set ownership
RUN chown -R migrationuser:migrationuser /app && \
    chmod +x efbundle

# Switch to non-root user
USER migrationuser

# The bundle requires connection string via environment variable
ENV ASPNETCORE_ENVIRONMENT=Production

# Run migrations
# Connection string should be provided via env var: ConnectionStrings__DefaultConnection
ENTRYPOINT ["./efbundle"]
