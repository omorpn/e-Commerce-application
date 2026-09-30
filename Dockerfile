# Build and run ShopNest in a container (used by Render, Railway, Fly.io, Azure, etc.)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["e-Commerce application.csproj", "./"]
RUN dotnet restore "e-Commerce application.csproj"
COPY . .
RUN dotnet publish "e-Commerce application.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Without a database URL the app uses SQLite under /data (mount a persistent disk there).
# Set ConnectionStrings__Default to a PostgreSQL URL (e.g. a free Neon database) to keep
# all data, uploads included, in PostgreSQL instead.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__Default="Data Source=/data/ecommerce.db" \
    Storage__Root=/data/storage
RUN mkdir -p /data
VOLUME /data
EXPOSE 8080

ENTRYPOINT ["dotnet", "e-Commerce application.dll"]
