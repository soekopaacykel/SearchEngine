# ConsoleSearch Dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY SearchEngine.sln ./
COPY Core/Core.csproj Core/
COPY ConsoleSearch/ConsoleSearch.csproj ConsoleSearch/
COPY indexer/indexer.csproj indexer/
COPY SearchAPI/SearchAPI.csproj SearchAPI/
COPY SearchWeb/SearchWeb.csproj SearchWeb/
RUN dotnet restore ConsoleSearch/ConsoleSearch.csproj
COPY . .
RUN dotnet publish ConsoleSearch/ConsoleSearch.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ConsoleSearch.dll"]
