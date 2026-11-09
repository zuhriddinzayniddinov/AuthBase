FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/WebApis/AuthApi/AuthApi.csproj", "src/WebApis/AuthApi/"]
COPY ["src/Domain/Entity/Entity.csproj", "src/Domain/Entity/"]
COPY ["src/Infrastructures/DatabaseBroker/DatabaseBroker.csproj", "src/Infrastructures/DatabaseBroker/"]
COPY ["src/Infrastructures/MinIoBroker/MinIoBroker.csproj", "src/Infrastructures/MinIoBroker/"]
COPY ["src/Services/StructureService/RoleService.csproj", "src/Services/StructureService/"]
COPY ["src/Services/AuthService/AuthService.csproj", "src/Services/AuthService/"]
COPY ["Core/src/Core.Domain/Core.Domain.csproj", "Core/src/Core.Domain/"]
COPY ["Core/src/Core.Infrastructure/Core.Infrastructure.csproj", "Core/src/Core.Infrastructure/"]
COPY ["Core/src/Core.Web/Core.Web.csproj", "Core/src/Core.Web/"]
RUN dotnet restore "src/WebApis/AuthApi/AuthApi.csproj"
COPY . .
WORKDIR "/src/src/WebApis/AuthApi"
RUN dotnet build "./AuthApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./AuthApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AuthApi.dll"]
