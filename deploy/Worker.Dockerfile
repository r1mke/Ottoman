FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Ottoman.slnx ./
COPY src/ src/
RUN dotnet publish src/Ottoman.Worker/Ottoman.Worker.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Ottoman.Worker.dll"]
