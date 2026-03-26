# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY WarpBusiness.slnx .
COPY WarpBusiness.CustomerPortal/WarpBusiness.CustomerPortal.csproj   WarpBusiness.CustomerPortal/
COPY WarpBusiness.Shared/WarpBusiness.Shared.csproj                   WarpBusiness.Shared/
COPY WarpBusiness.ServiceDefaults/WarpBusiness.ServiceDefaults.csproj WarpBusiness.ServiceDefaults/

RUN dotnet restore WarpBusiness.CustomerPortal/WarpBusiness.CustomerPortal.csproj

COPY WarpBusiness.CustomerPortal/  WarpBusiness.CustomerPortal/
COPY WarpBusiness.Shared/          WarpBusiness.Shared/
COPY WarpBusiness.ServiceDefaults/ WarpBusiness.ServiceDefaults/

RUN dotnet publish WarpBusiness.CustomerPortal/WarpBusiness.CustomerPortal.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "WarpBusiness.CustomerPortal.dll"]
