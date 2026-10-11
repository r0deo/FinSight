# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/FinSight.Domain/FinSight.Domain.csproj src/FinSight.Domain/
COPY src/FinSight.Application/FinSight.Application.csproj src/FinSight.Application/
COPY src/FinSight.Infrastructure/FinSight.Infrastructure.csproj src/FinSight.Infrastructure/
COPY src/FinSight.Api/FinSight.Api.csproj src/FinSight.Api/
RUN dotnet restore src/FinSight.Api/FinSight.Api.csproj
COPY src/ src/
RUN dotnet publish src/FinSight.Api/FinSight.Api.csproj --configuration Release --no-restore --output /publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /publish/ ./
ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=10000
EXPOSE 10000
USER $APP_UID
ENTRYPOINT ["dotnet", "FinSight.Api.dll"]
