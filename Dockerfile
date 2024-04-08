FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID 
ENV ASPNETCORE_HTTP_PORTS=64212
WORKDIR /app
EXPOSE 64212 

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["API_Gateway.csproj", "."] 
RUN dotnet restore "API_Gateway.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "API_Gateway.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "API_Gateway.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY ["../cert", "cert"]
ENTRYPOINT ["dotnet", "API_Gateway.dll"]
