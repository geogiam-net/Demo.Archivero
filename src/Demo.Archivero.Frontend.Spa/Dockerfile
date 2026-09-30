FROM mcr.microsoft.com/dotnet/sdk:10.0 AS publish
WORKDIR /source
COPY src/ ./src/
RUN dotnet publish src/Demo.Archivero.Frontend.Spa/Demo.Archivero.Frontend.Spa.csproj -c Debug -r linux-x64 --self-contained false -p:UseAppHost=false -o /publish
RUN sed -i 's|https://localhost:50949|http://localhost:50950|g' /publish/wwwroot/app.js
FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /app
COPY --from=publish /publish/ ./
ENV DOTNET_ENVIRONMENT=Development ASPNETCORE_ENVIRONMENT=Development
EXPOSE 50952
ENTRYPOINT ["dotnet", "Demo.Archivero.Frontend.Spa.dll"]
