FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY EpiRead/EpiRead.csproj EpiRead/
RUN dotnet restore EpiRead/EpiRead.csproj

COPY . .
RUN dotnet publish EpiRead/EpiRead.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://0.0.0.0:10000 \
    DOTNET_EnableDiagnostics=0

COPY --from=build /app/publish .

# The ASP.NET process runs as the non-root .NET container user. EpiRead writes
# application logs beneath its content root, so grant that user ownership.
RUN mkdir -p /app/Logs && chown -R $APP_UID:$APP_UID /app

EXPOSE 10000
USER $APP_UID
ENTRYPOINT ["dotnet", "EpiRead.dll"]
