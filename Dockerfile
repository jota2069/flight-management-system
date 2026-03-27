FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Копируем решение и проекты для кэширования слоев восстановления
COPY FlightManagement.sln .
COPY src/FlightManagement.Api/FlightManagement.Api.csproj src/FlightManagement.Api/
COPY src/FlightManagement.Client/FlightManagement.Client.csproj src/FlightManagement.Client/
COPY tests/FlightManagement.Tests/FlightManagement.Tests.csproj tests/FlightManagement.Tests/

RUN dotnet restore

# Копируем весь исходный код и выполняем сборку (publish)
COPY . .
WORKDIR /app/src/FlightManagement.Api
RUN dotnet publish -c Release -o /out

# Создаем финальный минимальный образ на базе ASP.NET Core Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /out .

# Порт, который будет слушать приложение внутри контейнера
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

ENTRYPOINT ["dotnet", "FlightManagement.Api.dll"]
