# FlightManagement security model

Web and API share ASP.NET Core Identity tables, Identity.Application authentication,
a host-only HttpOnly SameSite=Strict session cookie and the same DataProtection key
ring/application discriminator. No external identity provider or email service is
contacted. Public registration creates ordinary users only; there are no demo
accounts or seeded passwords. The API validates the security stamp on every request.

## Browser and server boundaries
The supported browser entry point is one origin (local Compose: http://localhost:8080).
Nginx routes /api/ to API and / to Web. Do not expose Web on another public port.
No CORS credentials policy is enabled. Mutations with a different Origin are rejected;
a missing Origin does NOT exempt the request from antiforgery validation.
Razor validates all POST forms; the API validates all unsafe controller methods.
Web forwards only this request's session/CSRF cookie and form token to a fixed internal
API URL. HttpClient has no CookieContainer and follows no redirects. No user identity
header or client-supplied OwnerUserId is trusted.

Production requires HTTPS and PUBLIC_ORIGIN=https://your-host. Secure cookies are
mandatory outside Development. TLS proxies must be explicitly configured with
TrustedProxies__0 etc. (exact IPs) for HTTPS termination; arbitrary forwarded
headers are not trusted. Web also requires an explicit HTTPS
API_BASE_URL with a trusted certificate; secure antiforgery must not be disabled
to accommodate an HTTP internal hop. The local HTTP Compose topology is Development only.
All apps sharing the key ring are one trust boundary. Restrict key-directory access,
encrypt its storage in production, and back up keys securely. The sample local
volume does not implement encryption at rest. Do not enable Development publicly.
Keep the key ring outside source control. .gitignore and .dockerignore exclude
common DataProtection key files/local secret files; they do not protect material
that was already tracked, force-added or named differently.

## Ownership and legacy records
Ticket/Passenger OwnerUserId is assigned from the authenticated Identity subject.
Only the owner or Admin can read/update tickets; only the owner or Admin can cancel.
Foreign/nonexistent tickets return 404. Legacy nullable owners remain NULL: no
matching by email/name and no automatic claiming. Admin can review legacy records.
Passenger responses do not contain ticket collections. Public flights contain no
ticket/crew collections. Passenger deletion with bookings is refused; flight deletion
with bookings is also refused. Existing routes stay the same; unauthorized calls
are intentionally no longer compatible with the old anonymous CRUD behavior.

## Schema changes
Back up existing databases. Stop old application versions before migrating. Only API
owns migrations; Web must start after API migration. Do not downgrade an Identity
database casually: a down migration removes accounts and ownership metadata.
SeedDemoData is opt-in, false by default, and never seeds users.
Flight labels are passed through HTML-encoded data attributes, never dynamically
interpolated into executable onclick handlers. Booking labels use textContent,
not innerHTML. This is a targeted fix, not a complete browser/XSS certification.

## PostgreSQL concurrency and cache
Ticket creation, move, cancellation and flight capacity edits explicitly use READ
COMMITTED transactions and lock flight rows FOR UPDATE in ascending ID order.
Mutating an existing ticket also locks/rechecks the ticket; a concurrent move to a
different source returns 409 instead of proceeding under the wrong flight lock.
Cancellation deletes the reservation. Every stored ticket, including a legacy
status, conservatively consumes capacity until explicitly reviewed/cancelled.
Seat labels are trimmed/uppercased, 1-8 ASCII alphanumerics. A generated normalized
column and a unique index enforce per-flight seat uniqueness even outside the API.
Positive price/capacity constraints are in PostgreSQL. Passenger/flight deletion
cannot cascade around ticket handling. Aggregate capacity is enforced by the API
transaction protocol; direct database writers must use the same protocol.

Redis caches only public, navigation-free schedules, not tickets/passengers or
authentication/availability decisions. CacheRevisions.Version advances in the same
transaction as every ticket/flight mutation. All reads obtain the version from
PostgreSQL before using Redis. Old-key writes cannot overwrite a newer revision;
orphaned old keys expire after 5 minutes. Redis failures/corrupt JSON fall back to
PostgreSQL, including initial connection failure. Database failure is NOT hidden.
This adds a DB version read and serializes short revision increments; it is not a
high-load architecture. Direct SQL maintenance must also advance the version.

## Explicit administration and remaining scope
--create-admin in Web is an explicit local command, never a startup seed. It creates
a NEW account with Identity password validation and refuses to promote existing
accounts. Password input is hidden; no password argument, source-code constant or
automatic account creation is used. Logout revokes all sessions via security stamp.
New Unix key-ring directories are 0700; existing unsafe permissions cause startup
failure without an automatic chmod. Shared keys still need encryption/backup and
restricted service-user access in production.

This is not a certified/production-ready reservation system. Prices remain a
client-supplied educational field (validated positive, 2 decimal places), not an
authoritative fare or payment. There is no aircraft seat map, full flight lifecycle,
MFA, email confirmation/recovery or production registration abuse protection.
Use a separate least-privileged runtime database role and migration process in a
real deployment. Sample Compose is loopback-only development; TLS, live Redis and
the complete container/browser deployment need deployment-specific verification.
Do not expose Development, database admin credentials, metrics or Grafana publicly.

References: [Microsoft shared-cookie guidance](https://learn.microsoft.com/en-us/aspnet/core/security/cookie-sharing?view=aspnetcore-8.0),
[Microsoft antiforgery guidance](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-8.0).
