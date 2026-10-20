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
TrustedProxies__0 etc. (exact IPs); arbitrary forwarded headers are not trusted.
All apps sharing the key ring are one trust boundary. Restrict key-directory access,
encrypt its storage in production, and back up keys securely. The sample local
volume does not implement encryption at rest. Do not enable Development publicly.

## Ownership and legacy records
Ticket/Passenger OwnerUserId is assigned from the authenticated Identity subject.
Only the owner or Admin can read/update tickets; only the owner or Admin can cancel.
Foreign/nonexistent tickets return 404. Legacy nullable owners remain NULL: no
matching by email/name and no automatic claiming. Admin can review legacy records.
Passenger responses do not contain ticket collections. Public flights contain no
ticket/crew collections. Passenger deletion with bookings is refused; flight deletion
with bookings must also be refused. Existing routes stay the same; unauthorized calls
are intentionally no longer compatible with the old anonymous CRUD behavior.

## Schema changes
Back up existing databases. Stop old application versions before migrating. Only API
owns migrations; Web must start after API migration. Do not downgrade an Identity
database casually: a down migration removes accounts and ownership metadata.
SeedDemoData is opt-in, false by default, and never seeds users.
