FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Paragrafen.Core/Paragrafen.Core.csproj src/Paragrafen.Core/
COPY src/Paragrafen.Data/Paragrafen.Data.csproj src/Paragrafen.Data/
COPY src/Paragrafen.Api/Paragrafen.Api.csproj src/Paragrafen.Api/
RUN dotnet restore src/Paragrafen.Api/Paragrafen.Api.csproj
COPY src/ src/
RUN dotnet publish src/Paragrafen.Api/Paragrafen.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Paragrafen.Api.dll"]
