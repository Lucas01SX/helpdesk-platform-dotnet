FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Helpdesk.API/Helpdesk.API.csproj", "src/Helpdesk.API/"]
COPY ["src/Helpdesk.Shared/Helpdesk.Shared.csproj", "src/Helpdesk.Shared/"]
COPY ["src/Modules/Tickets/Helpdesk.Modules.Tickets.csproj", "src/Modules/Tickets/"]
COPY ["src/Modules/Identity/Helpdesk.Modules.Identity.csproj", "src/Modules/Identity/"]
COPY ["src/Modules/SLA/Helpdesk.Modules.SLA.csproj", "src/Modules/SLA/"]
COPY ["src/Modules/Notifications/Helpdesk.Modules.Notifications.csproj", "src/Modules/Notifications/"]
RUN dotnet restore "src/Helpdesk.API/Helpdesk.API.csproj"
COPY . .
WORKDIR "/src/src/Helpdesk.API"
RUN dotnet build "Helpdesk.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Helpdesk.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Helpdesk.API.dll"]
