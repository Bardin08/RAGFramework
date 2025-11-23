# Stage 1: Build and create EF Bundle
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /build

# Install EF Core tools with retry logic for transient failures
RUN dotnet nuget list source || true && \
    dotnet tool install --global dotnet-ef --version 8.0.0 || \
    (echo "Retrying dotnet-ef installation..." && sleep 5 && dotnet tool install --global dotnet-ef --version 8.0.0)
ENV PATH="${PATH}:/root/.dotnet/tools"

# Copy solution and project files
COPY ["src/RAG.API/RAG.API.csproj", "src/RAG.API/"]
COPY ["src/RAG.Application/RAG.Application.csproj", "src/RAG.Application/"]
COPY ["src/RAG.Core/RAG.Core.csproj", "src/RAG.Core/"]
COPY ["src/RAG.Infrastructure/RAG.Infrastructure.csproj", "src/RAG.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "src/RAG.Infrastructure/RAG.Infrastructure.csproj"

# Copy all source code
COPY src/ src/

# Create EF migrations bundle
# Set the working directory back to Infrastructure for the bundle creation
WORKDIR "/build/src/RAG.Infrastructure"
RUN dotnet ef migrations bundle \
    --project "RAG.Infrastructure.csproj" \
    --startup-project "../RAG.API/RAG.API.csproj" \
    --configuration Release \
    --output /app/efbundle \
    --force

# Stage 2: Runtime (minimal image)
# Using ASP.NET Core runtime because the EF bundle requires it
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

# Create non-root user with UID 1000 to match Kubernetes securityContext
RUN groupadd -g 1000 migrationuser && useradd -u 1000 -g migrationuser -m migrationuser

WORKDIR /app

# Copy the migrations bundle
COPY --from=build /app/efbundle .

# Create a writable directory for bundle extraction in the user's home
RUN mkdir -p /home/migrationuser/.dotnet/bundle && \
    chown -R migrationuser:migrationuser /app /home/migrationuser && \
    chmod +x efbundle

# Switch to non-root user
USER migrationuser

# The bundle requires connection string via environment variable
ENV ASPNETCORE_ENVIRONMENT=Production

# Set the bundle extraction directory to user's home directory
ENV DOTNET_BUNDLE_EXTRACT_BASE_DIR=/home/migrationuser/.dotnet/bundle

# Run migrations
# Connection string should be provided via env var: ConnectionStrings__DefaultConnection
ENTRYPOINT ["./efbundle"]
