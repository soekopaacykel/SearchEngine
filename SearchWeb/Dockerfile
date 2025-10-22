# SearchWeb Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY SearchEngine.sln ./
COPY Core/Core.csproj Core/
COPY SearchWeb/SearchWeb.csproj SearchWeb/
RUN dotnet restore SearchWeb/SearchWeb.csproj
COPY . .
RUN dotnet publish SearchWeb/SearchWeb.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
# Optionally allow overriding the API base URL for the web via env
ENV SearchApi__BaseUrl=http://loadbalancer:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SearchWeb.dll"]
