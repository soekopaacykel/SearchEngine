FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app
COPY . .
ENTRYPOINT ["dotnet", "DataMigration.dll"]