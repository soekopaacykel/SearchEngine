# Loadbalencer Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY SearchEngine.sln ./
COPY Core/Core.csproj Core/
COPY Loadbalencer/Loadbalencer.csproj Loadbalencer/
RUN dotnet restore Loadbalencer/Loadbalencer.csproj
COPY . .
RUN dotnet publish Loadbalencer/Loadbalencer.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Loadbalencer.dll"]
