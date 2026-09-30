FROM mcr.microsoft.com/dotnet/sdk:10.0 AS publish
WORKDIR /source
COPY src/ ./src/
RUN dotnet publish src/Demo.Archivero.Converter.Worker/Demo.Archivero.Converter.Worker.csproj -c Debug -r linux-x64 --self-contained false -p:UseAppHost=false -o /publish
FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /app
COPY --from=publish /publish/ ./
ENV DOTNET_ENVIRONMENT=Development ASPNETCORE_ENVIRONMENT=Development
ENTRYPOINT ["dotnet", "Demo.Archivero.Converter.Worker.dll"]
