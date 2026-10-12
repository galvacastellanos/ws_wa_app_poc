# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["ws_wa_app_poc.csproj", "./"]
RUN dotnet restore "ws_wa_app_poc.csproj"

COPY . .
RUN dotnet publish "ws_wa_app_poc.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ws_wa_app_poc.dll"]
