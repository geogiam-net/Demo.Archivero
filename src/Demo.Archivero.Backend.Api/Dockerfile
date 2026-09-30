FROM mcr.microsoft.com/dotnet/sdk:10.0 AS publish
WORKDIR /source
COPY src/ ./src/
RUN dotnet publish src/Demo.Archivero.Backend.Api/Demo.Archivero.Backend.Api.csproj -c Debug -r linux-x64 --self-contained false -p:UseAppHost=false -o /publish
FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /app
COPY --from=publish /publish/ ./
ENV DOTNET_ENVIRONMENT=Development ASPNETCORE_ENVIRONMENT=Development
EXPOSE 50950
ENTRYPOINT ["dotnet", "Demo.Archivero.Backend.Api.dll"]
