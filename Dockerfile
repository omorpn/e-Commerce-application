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

# Everything that must survive restarts lives under /data: mount a persistent disk there.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__Default="Data Source=/data/ecommerce.db" \
    Storage__Root=/data/storage \
    DataProtection__KeysPath=/data/keys
RUN mkdir -p /data
VOLUME /data
EXPOSE 8080

ENTRYPOINT ["dotnet", "e-Commerce application.dll"]
