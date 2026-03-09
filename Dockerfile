FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/MarcoERP.API/MarcoERP.API.csproj", "src/MarcoERP.API/"]
COPY ["src/MarcoERP.Application/MarcoERP.Application.csproj", "src/MarcoERP.Application/"]
COPY ["src/MarcoERP.Domain/MarcoERP.Domain.csproj", "src/MarcoERP.Domain/"]
COPY ["src/MarcoERP.Persistence/MarcoERP.Persistence.csproj", "src/MarcoERP.Persistence/"]
COPY ["src/MarcoERP.Infrastructure/MarcoERP.Infrastructure.csproj", "src/MarcoERP.Infrastructure/"]

RUN dotnet restore "src/MarcoERP.API/MarcoERP.API.csproj"

COPY ["src/MarcoERP.API/", "src/MarcoERP.API/"]
COPY ["src/MarcoERP.Application/", "src/MarcoERP.Application/"]
COPY ["src/MarcoERP.Domain/", "src/MarcoERP.Domain/"]
COPY ["src/MarcoERP.Persistence/", "src/MarcoERP.Persistence/"]
COPY ["src/MarcoERP.Infrastructure/", "src/MarcoERP.Infrastructure/"]

WORKDIR "/src/src/MarcoERP.API"
RUN dotnet publish "MarcoERP.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_EnableDiagnostics=0

EXPOSE 10000

ENTRYPOINT ["dotnet", "MarcoERP.API.dll"]