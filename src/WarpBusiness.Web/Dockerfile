# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY WarpBusiness.slnx .
COPY WarpBusiness.Web/WarpBusiness.Web.csproj                                                     WarpBusiness.Web/
COPY WarpBusiness.Shared/WarpBusiness.Shared.csproj                                               WarpBusiness.Shared/
COPY WarpBusiness.ServiceDefaults/WarpBusiness.ServiceDefaults.csproj                             WarpBusiness.ServiceDefaults/
COPY WarpBusiness.Plugin.Abstractions/WarpBusiness.Plugin.Abstractions.csproj                     WarpBusiness.Plugin.Abstractions/
COPY WarpBusiness.Plugin.Crm/WarpBusiness.Plugin.Crm.csproj                                       WarpBusiness.Plugin.Crm/
COPY WarpBusiness.Plugin.EmployeeManagement/WarpBusiness.Plugin.EmployeeManagement.csproj         WarpBusiness.Plugin.EmployeeManagement/

RUN dotnet restore WarpBusiness.Web/WarpBusiness.Web.csproj

COPY WarpBusiness.Web/                        WarpBusiness.Web/
COPY WarpBusiness.Shared/                     WarpBusiness.Shared/
COPY WarpBusiness.ServiceDefaults/            WarpBusiness.ServiceDefaults/
COPY WarpBusiness.Plugin.Abstractions/        WarpBusiness.Plugin.Abstractions/
COPY WarpBusiness.Plugin.Crm/                 WarpBusiness.Plugin.Crm/
COPY WarpBusiness.Plugin.EmployeeManagement/  WarpBusiness.Plugin.EmployeeManagement/

RUN dotnet publish WarpBusiness.Web/WarpBusiness.Web.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "WarpBusiness.Web.dll"]
