# SearchAPI Dockerfile
# Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY SearchEngine.sln ./
COPY Core/Core.csproj Core/
COPY SearchAPI/SearchAPI.csproj SearchAPI/
RUN dotnet restore SearchAPI/SearchAPI.csproj
COPY . .
RUN dotnet publish SearchAPI/SearchAPI.csproj -c Release -o /app/publish

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SearchAPI.dll"]
