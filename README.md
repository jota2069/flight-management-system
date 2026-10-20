# FlightManagement

Учебное приложение на C#/.NET 8: ASP.NET Core Web API, Razor Pages, EF Core,
PostgreSQL и необязательный Redis-кеш расписания. Один бизнес-API с инфраструктурой,
не микросервисная система и не готовая платёжная/авиационная платформа.

- [Модель безопасности и ограничения](SECURITY.md)
- [Проверки и соответствие требованиям](docs/security-tests.md)
- [Запуск и ручная проверка](docs/manual-verification.md)

## Быстрый локальный запуск

Нужны Docker с Compose. Установка системных пакетов автоматически не выполняется.
Сначала прочитайте раздел про существующую БД в инструкции ручной проверки.

Для НОВОЙ локальной БД задайте POSTGRES_PASSWORD и GRAFANA_ADMIN_PASSWORD в окружении.
Не используйте значения из старых демонстрационных конфигураций. Не сохраняйте
секреты в Git. Compose требует значения явно и не выводит их при обычном запуске.
Для POSTGRES_PASSWORD используйте случайные 64 hex-символа: он подставляется в
строку Npgsql. Пароль Identity задаётся отдельно и имеет собственные требования.

```bash
docker compose up --build -d db cache api
# Дождитесь успешного завершения миграций и запуска API (docker compose logs api).
docker compose up --build -d web proxy
```

Входная точка: http://localhost:8080. Web не публикуется на отдельном порту.
HTTP разрешён только для локального Development; порты привязаны к loopback.
Для production нужны TLS, доверенные прокси, приватный/защищённый key ring и отдельная
конфигурация; пример Compose не является production-рецептом.

Аккаунтов по умолчанию нет. /Register создаёт обычный аккаунт без роли Admin.
Создание НОВОГО администратора после миграций:

```bash
docker compose exec web dotnet FlightManagement.Web.dll --create-admin YOUR_NEW_ADMIN_LOGIN
```

Пароль вводится интерактивно без отображения. Нельзя повысить права уже существующего
пользователя этой командой. Пароль: 12–128 символов, верхний/нижний регистр, цифра и
спецсимвол. Почта, паспорт и контакты для аккаунта Identity не требуются и не отправляются.

## Сборка и проверки

```bash
DOTNET_NUGET_SIGNATURE_VERIFICATION=true dotnet restore
dotnet build FlightManagement.sln --configuration Release --no-restore
# Только отдельный PostgreSQL для тестов с правом CREATEDB:
# FLIGHT_TEST_ADMIN_CONNECTION должен указывать на него, не на рабочую БД.
dotnet test FlightManagement.sln --configuration Release --no-build
```

Тесты создают случайно названные БД flight_test_*, применяют настоящие миграции
и удаляют только эти БД. Если FLIGHT_TEST_ADMIN_CONNECTION отсутствует, интеграционные
тесты завершаются ошибкой, а не пропускаются. CI поднимает отдельный PostgreSQL 16.
Сервер Redis для основной тестовой матрицы не нужен: отказ контролируется тестовым
дублёром; отдельная проверка использует настоящий клиент и закрытый loopback-порт.

Для изменения схемы есть локальный manifest dotnet-ef 8.0.14: dotnet tool restore.
Приложение использует пакеты EF Core одной версии 8.0.14, провайдер Npgsql 8.0.11.
