# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["src/RAG.API/RAG.API.csproj", "src/RAG.API/"]
COPY ["src/RAG.Application/RAG.Application.csproj", "src/RAG.Application/"]
COPY ["src/RAG.Core/RAG.Core.csproj", "src/RAG.Core/"]
COPY ["src/RAG.Infrastructure/RAG.Infrastructure.csproj", "src/RAG.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "src/RAG.API/RAG.API.csproj"

# Copy all source code
COPY . .

# Build the application
WORKDIR "/src/src/RAG.API"
RUN dotnet build "RAG.API.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "RAG.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

# Create non-root user
RUN groupadd -r raguser && useradd -r -g raguser raguser

WORKDIR /app

# Copy published files
COPY --from=publish /app/publish .

# Set ownership
RUN chown -R raguser:raguser /app

# Switch to non-root user
USER raguser

# Expose port (non-privileged)
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:8080/healthz || exit 1

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Start the application
ENTRYPOINT ["dotnet", "RAG.API.dll"]
