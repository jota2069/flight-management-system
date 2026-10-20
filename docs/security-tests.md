# Проверки безопасности и бизнес-логики

## Результат локальной проверки
Этапы: 18/18 (Identity/access/CSRF), 35/35 (PostgreSQL booking/migrations),
44/44 (cache), итог 62/62, 0 failures, 0 errors, 0 skipped. Из них 3 — исходные
fixture-тесты, 59 — новые проверки. Дополнительная узкая проверка рендеринга: 1/1.
Release-сборка всего решения: 0 ошибок, 0 предупреждений.
Повторный dotnet restore с NuGetAudit=true завершился без предупреждений.
Явный dotnet nuget verify --all с DOTNET_NUGET_SIGNATURE_VERIFICATION=true
успешно проверил пакеты Identity EF 8.0.14, MVC Testing 8.0.14, Moq 4.20.72
и dotnet-ef 8.0.14. Это не заявляется как аудит всех транзитивных пакетов.
Compose проверен через config --quiet; полный контейнерный запуск не выполнялся.

Итоговый тестовый запуск (FLIGHT_TEST_ADMIN_CONNECTION уже задан на тестовый кластер):
```bash
dotnet test tests/FlightManagement.Tests --configuration Release --no-restore -m:1
```
Проверка проводилась на PostgreSQL 16 через приватный Unix-сокет, .NET 8.
CI-конфигурация добавлена, но не запускалась в GitHub, поскольку push не выполнялся.

Все интеграционные сценарии используют настоящие Identity, cookie/antiforgery,
Razor/MVC pipeline и PostgreSQL. Аутентификация в тестах не подменяется тестовой схемой.
Аккаунты создаются через /Register и входят через /Login; роли для тестов задаются
серверным UserManager. В CI нужен отдельный PostgreSQL с CREATEDB. Его trust-режим
допустим только для одноразового тестового окружения без пользовательских данных.

| Требование | Конкретные тесты |
| --- | --- |
| Постоянные аккаунты, хеширование, Web/API | IdentityRoundTrip_HashesPassword_AndCookieWorksInBothHosts |
| Нет demo-входа, нет публичного повышения прав | IdentityRoundTrip_HashesPassword_AndCookieWorksInBothHosts; AdminProvisioning_DoesNotPromoteExistingAccount |
| Блокировка подбора и отзыв | Login_LocksOutAfterFiveFailedAttempts; SecurityStamp_RevokedCookieIsRejectedByBothHosts |
| Роли/анонимные запросы | AnonymousAndRoleGates_PrivateApiDeniesAnonymous; AnonymousAndRoleGates_UserCannotMutateFlightsOrReadCrew |
| Чужие билеты и server-side owner | TicketOwnership_ServerAssignsOwner_AndDeniesForeignReadUpdateDelete |
| Пассажиры и косвенный доступ | PassengerOwnership_DeniesForeignAccessAndBooking |
| Старые записи / нет fallback | LegacyTickets_AreNotClaimedByMatchingNameOrEmail_AndWebHasNoFallback |
| Admin видит legacy, но не присваивает owner | Admin_CanReviewLegacyTicket_ButCannotAssignOwnerThroughUpdate |
| Согласованность сессий/нет утечки HttpClient | WebForwarding_BookingAndCancelUseCurrentUsersCookieAndFormCsrf; BrowserSessions_NeverLeakBetweenPooledHttpClients_AndCannotCancelForeignTicket |
| CSRF login/register/API POST/PUT/DELETE/Web POST | CsrfRequired_AccountFormsRejectMissingToken; CsrfRequired_ApiRejectsMissingInvalidAndOtherUsersToken; CsrfRequired_TicketMutationsRejectMissingTokenAndPreserveRecord; CsrfRequired_WebMutationRejectsMissingFormToken |
| Origin/CORS | OriginGuard_RejectsCrossOriginEvenWithValidCsrf |
| HTTPS/flags/приватный key ring/logout | Production_RequiresHttpsOrigin_AndSecureHostOnlyCookies; SessionCookies_HaveSecurityFlags_AndLogoutRevokesApiCookie |
| Production Web/API без снятия Secure/CSRF | ProductionCookieAndCsrf_WorkAcrossWebAndApi_WithoutDisablingSecurePolicies |
| Данные рейса не становятся JS/HTML-кодом | Razor_FlightDataIsEncodedData_NotExecutableHandlersOrInnerHtml |
| Новый администратор только явно | AdminProvisioning_CreatesHashedAccount_AndRollsBackWeakPassword |
| Последнее место, реальные PG locks | LastSeatConcurrent_PostgresLockAllowsExactlyOneBooking |
| Одно место в конкуренции | SameSeatConcurrent_CaseAndWhitespaceCannotBypassUniqueness |
| Вместимость / конкурентное уменьшение | CapacityUpdate_RejectsOverbookedReduction_AndDeletionWithTickets; CapacityReductionConcurrentWithBooking_NeverExceedsFinalCapacity; FlightCreation_RejectsNonPositiveCapacity |
| Нормализация и DB constraints | DatabaseConstraints_RejectDirectDuplicateNormalizedSeatAndInvalidPrice; InvalidBooking_RejectsSeatOrPriceWithoutWriting |
| Обновление/перенос/отмена | OwnerUpdate_PreservesIdentityAndPurchaseDate_RejectsFullDestination; BookingTransfer_ReservesDestinationAndReleasesSourceCapacity; Cancellation_ReleasesCapacityAndSeat |
| Миграции старых данных | MigrationPreservesLegacy_WithoutAssigningOwnersOrCreatingDemoUsers; MigrationRejectsInvalidLegacy_WithoutDeletingOrReassigningData |
| Cache hit/miss, приватность | CacheHitMiss_UsesCachedScheduleAndNeverContainsPrivateCollections |
| Сбой Redis / JSON | CacheCorruptionAndOutage_ReadsAuthoritativeDatabase; CacheInitialConnectionFailure_DoesNotPreventDatabaseRead; RealRedisConnectionRefused_PublicReadStillSucceeds |
| Версия только после успешных изменений | BookingUpdateAndCancellation_AdvanceRevision_OnlyOnCommittedChanges |
| Позднее заполнение / CRUD рейсов | LateCacheFill_CannotResurrectScheduleAfterCommittedUpdate; FlightCreateAndDelete_InvalidateListAndDetailByDatabaseRevision |

Тесты конкуренции держат внешний PostgreSQL row lock, запускают два HTTP-запроса,
через pg_stat_activity убеждаются, что оба реально ждут блокировки, затем отпускают её.
Это не имитация транзакций в EF InMemory и не последовательные запросы.

## Статический анализ пробелов
Разобраны наблюдаемые мутации: удаление owner-фильтра нарушает deny/empty-list тесты;
доверие клиентскому owner/date/status — проверки сохранённых полей; удаление CSRF —
отказы 400; удаление row lock/проверки вместимости — конкуренцию и итоговый count;
удаление unique index — прямую вставку дубля; удаление смены cache revision — точные
значения версии/late-fill. Это сопоставление кода и утверждений, не запуск mutation-инструмента.

## Границы
Основная матрица Redis использует управляемый дублёр IDatabase. Проверка отказа соединения
использует настоящий StackExchange.Redis, но успешный обмен с живым Redis отдельно
не проверен. Тестовые HTTP-хосты не заменяют проверку TLS/Nginx/контейнеров браузером.
Исходные три теста fixture-значений сохранены; новые тесты проверяют поведение.
