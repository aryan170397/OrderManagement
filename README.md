# Order Management API

REST API for an e-commerce order workflow. Customers register and place orders. Administrators manage the catalog, inventory, and order lifecycle. Prices, totals, ownership, and stock changes are calculated on the server.

The solution uses a small layered layout:

- `OrderManagement.Api` handles HTTP, JWT authentication, authorization policies, validation filters, and problem responses.
- `OrderManagement.Application` implements use cases, DTO validation, and business rules.
- `OrderManagement.Domain` holds entities, roles, order transitions, and monetary calculation.
- `OrderManagement.Infrastructure` holds EF Core, MySQL, repositories, password hashing, and token creation.

## Prerequisites

- .NET SDK 10
- MySQL 8.0 or newer (8.4 is the version used while developing this solution). Check constraints require MySQL 8.0.16+.
- `dotnet-ef` 10.0.9, matching the EF Core package version:

```bash
dotnet tool install --global dotnet-ef --version 10.0.9
```

Docker is optional and is only a way to run MySQL. This machine does not need Docker to build the API.

## Installation and setup

```bash
dotnet restore
dotnet build
```

Configure secrets with environment variables, a root `.env` file, or user secrets. ASP.NET Core does not load `.env` by itself. `EnvFileLoader` reads a `.env` file from the working directory before the host is built, and it does not overwrite variables that are already set.

```bash
copy .env.example .env
```

User secrets for the API project:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=ordermanagement;User ID=ordermanagement;Password=CHANGE_ME;SslMode=Preferred;CharSet=utf8mb4" --project src/OrderManagement.Api
dotnet user-secrets set "Jwt:SigningKey" "replace-with-at-least-32-random-characters" --project src/OrderManagement.Api
dotnet user-secrets set "AdminSeed:Email" "admin@example.com" --project src/OrderManagement.Api
dotnet user-secrets set "AdminSeed:Password" "replace-with-a-real-password" --project src/OrderManagement.Api
dotnet user-secrets set "AdminSeed:Name" "Administrator" --project src/OrderManagement.Api
```

The signing key must be at least 32 bytes. The API refuses to start without a connection string and a valid JWT configuration. Do not commit `.env`, user secrets, or real passwords.

For a local MySQL container:

```bash
docker compose up -d
```

Use `SslMode=Disabled` in the connection string when the container does not present a certificate. Example:

```text
Server=localhost;Port=3306;Database=ordermanagement;User ID=ordermanagement;Password=changeme;SslMode=Disabled;CharSet=utf8mb4
```

## Database setup

Migrations are the schema source. The API does not create or update the schema on startup.

```bash
dotnet ef migrations add InitialCreate --project src/OrderManagement.Infrastructure --startup-project src/OrderManagement.Api --output-dir Persistence/Migrations

dotnet ef database update --project src/OrderManagement.Infrastructure --startup-project src/OrderManagement.Api
```

`InitialCreate` is already in the repository. Apply it with `database update` before the first run. If administrator credentials are configured and the tables do not exist yet, startup fails on purpose so the missing migration is visible.

## Running the application

```bash
dotnet run --project src/OrderManagement.Api --launch-profile http
```

The HTTP profile listens on `http://localhost:5080`. The HTTPS profile also listens on `https://localhost:7080`. Deployed environments should terminate TLS. The `Testing` environment used by integration tests does not redirect to HTTPS, so the in-memory test server can call the API over HTTP.

Swagger UI: `http://localhost:5080/swagger`

## Authentication

`POST /api/auth/register` creates an active customer. The request has name, email, and password only. A `role` field in the JSON body is ignored. Every registration is stored as `Customer`.

`POST /api/auth/login` checks the normalized email and the password hash. Unknown users, wrong passwords, and inactive accounts all receive the same `401` response so the API does not reveal which part failed.

Passwords are hashed with ASP.NET Core Identity's `PasswordHasher<T>` (PBKDF2). The database stores the hash. Responses never include it.

JWT access tokens are HMAC-SHA256. Validation checks the issuer, audience, signature, and lifetime. Clock skew is one minute. The default lifetime is 60 minutes (`Jwt:ExpirationMinutes`, allowed range 5 to 1440). Send the token as `Authorization: Bearer {token}`. In Swagger, choose **Authorize** and paste the token without the `Bearer` prefix.

The initial administrator is created only when `AdminSeed:Email` and `AdminSeed:Password` are configured and that email is not already registered. An existing non-admin account is not promoted. The seed does not reset an existing administrator password. There is no production password in source control. Set `AdminSeed:ApplyOnStartup` to `false` to skip seeding.

Roles are `Admin` and `Customer`. Customers can place and read their own orders. Administrators manage products and move orders through confirmation and shipment. Administrator accounts cannot place customer orders.

## API documentation

| Method | Endpoint | Access |
|---|---|---|
| POST | `/api/auth/register` | Anonymous |
| POST | `/api/auth/login` | Anonymous |
| GET | `/api/customers/me` | Authenticated |
| PUT | `/api/customers/me` | Authenticated, name only |
| PATCH | `/api/customers/{id}/activate` | Admin |
| PATCH | `/api/customers/{id}/deactivate` | Admin |
| POST | `/api/products` | Admin |
| GET | `/api/products` | Authenticated |
| GET | `/api/products/{id}` | Authenticated |
| PUT | `/api/products/{id}` | Admin, name and description |
| PATCH | `/api/products/{id}/price` | Admin |
| PATCH | `/api/products/{id}/inventory` | Admin, absolute quantity |
| PATCH | `/api/products/{id}/activate` | Admin |
| PATCH | `/api/products/{id}/deactivate` | Admin |
| POST | `/api/orders` | Customer |
| GET | `/api/orders` | Admin sees all; customer sees own |
| GET | `/api/orders/{id}` | Admin or owner |
| PATCH | `/api/orders/{id}/confirm` | Admin |
| PATCH | `/api/orders/{id}/ship` | Admin |
| PATCH | `/api/orders/{id}/cancel` | Owner or admin, when the transition is allowed |

`PATCH /api/products/{id}/inventory` sets `availableQuantity` to an absolute value. It is not a delta. Price, inventory, and descriptive updates require the `version` returned by the last read. A stale version returns `409`.

Product list query: `page`, `pageSize` (1–100), `search`, `isActive`, `sortBy` (`name`, `price`, `createdAt`), `sortDirection` (`asc` or `desc`).

Order list query: `page`, `pageSize`, `status` (`Pending`, `Confirmed`, `Shipped`, `Cancelled`), `createdFromUtc`, `createdToUtc`. Both date bounds are inclusive. Sort is `createdAt` descending, then `id` descending.

Successful creates return `201` with the resource. Product and order creates include a `Location` header. Updates and reads return `200`. Validation failures return `400` problem details. Missing or invalid tokens return `401`. The wrong role returns `403`. Unknown resources return `404`. Inventory conflicts, duplicate emails, stale versions, and illegal transitions return `409`. Unexpected failures return `500` without a stack trace, SQL text, or exception message. Problem responses include `traceId` and `errorCode`. The response header `X-Correlation-Id` repeats the trace identifier.

## Testing

Unit tests do not need MySQL:

```bash
dotnet test tests/OrderManagement.UnitTests/OrderManagement.UnitTests.csproj
```

Integration tests use a real MySQL server. They create a database named `omtest` plus a random suffix, apply migrations, and drop that database at the end. They do not use the EF Core in-memory provider.

```bash
set ORDERMANAGEMENT_TEST_CONNECTION=Server=127.0.0.1;Port=3306;User ID=root;Password=YOUR_PASSWORD;SslMode=Disabled
dotnet test tests/OrderManagement.IntegrationTests/OrderManagement.IntegrationTests.csproj
```

If the variable is unset, the tests try `Server=127.0.0.1;Port=3306;User ID=root;Password=;SslMode=Disabled`. Connector/NET 9 uses `SslMode=Disabled` rather than `None`. If MySQL is not reachable, the tests are skipped rather than reported as passed.

The integration-test administrator is `admin@test.local` / `AdminPass123`. That password exists only in the test fixture.

## Architecture

Controllers depend on application services. Services depend on repository and token interfaces. Infrastructure implements those interfaces with EF Core and MySQL. Domain types do not reference EF Core or ASP.NET Core.

Reads that do not need identity values use `AsNoTracking`. Order pages load the page of order ids first, then the orders and items in a second query, so pagination is not applied to a joined row set. Product search uses a parameterized `LIKE` with `%` and `_` escaped.

## Technical decisions

**Relationships.** A customer has many orders. An order has many items. A product has many items. Deleting a customer or product that is referenced by an order is restricted. Deleting an order would delete its items; the API does not delete orders. Products are deactivated instead of deleted.

**Money.** Prices, unit prices, line totals, and order totals use `decimal(18,2)`. Line totals are `unitPrice * quantity`. The order total is the sum of line totals. Product price changes do not rewrite existing order items. The product name is also copied onto the order item.

**Status storage.** `OrderStatus` and `UserRole` are stored as their names (`Pending`, `Confirmed`, `Shipped`, `Cancelled`, `Admin`, `Customer`).

**Timestamps.** The application writes UTC clock values. A connection interceptor sets the MySQL session time zone to `+00:00`. API responses mark those values as UTC.

**Email.** Values are trimmed and converted with the invariant lowercase culture before they are stored or looked up. Dots are not removed and international domains are not rewritten.

**Transactions.** Order creation and status changes run in one `ReadCommitted` transaction. Read committed is enough because correctness comes from conditional updates, not from a long consistent snapshot. InnoDB locks the updated row until commit. Products in one order are reserved in ascending id order. Deadlock (`1213`) and lock-wait (`1205`) failures retry the whole transaction up to three times. Business conflicts are not retried. The change tracker is cleared before each attempt.

**Inventory.** Creating a pending order decrements stock. Confirm and ship do not. Cancelling a pending or confirmed order restores the reserved quantity once. The status change and the stock restore commit together. A second cancel finds a terminal status and does not restore again. Shipped orders cannot be cancelled. Reservation uses a parameterized update:

```sql
UPDATE Products
SET AvailableQuantity = AvailableQuantity - @quantity,
    UpdatedAt = @updatedAt,
    Version = Version + 1
WHERE Id = @productId
  AND IsActive = 1
  AND AvailableQuantity >= @quantity;
```

Zero affected rows means the product is missing, inactive, or short of stock. The conditional update is used because tracking an entity and calling `SaveChanges` would let two transactions read the same quantity and both write a decremented value. Administrative price and inventory updates compare `Version`, so a reservation that happened after the admin read the product cannot be overwritten by a stale absolute quantity.

**Order state.**

```text
Pending --> Confirmed --> Shipped
   |            |
   +----> Cancelled <---+
```

`Pending` to `Shipped`, and any move out of `Shipped` or `Cancelled`, is rejected. The transition itself is a conditional update on the current status, so two overlapping requests cannot both win.

**Ownership.** The customer id comes from the validated `nameidentifier` claim. A client-supplied customer id is not accepted. A customer who asks for another customer's order receives `404`.

**Validation and errors.** FluentValidation checks request shape. Services enforce inventory, activation, and lifecycle rules. `IExceptionHandler` writes problem details. Database unique violations become `409`. Logs record operational events and unexpected exceptions with the trace id. They do not record passwords, tokens, signing keys, or connection strings. EF Core sensitive-data logging is off.

## Assumptions

- Inventory on create is a reservation. The assignment allowed the implementation to define that workflow.
- Inventory updates are absolute quantities plus a concurrency version, not deltas.
- `PUT /api/products/{id}` changes name and description only. Price and stock have their own endpoints.
- Customers may update their name only. Email and role stay server-controlled.
- Duplicate product ids in one order are summed before stock is reserved.
- Orders must contain 1 to 100 lines. Each line quantity is 1 to 10,000. Product price is greater than zero, at most two decimal places, and at most `9999999.99`. Stock cannot exceed 1,000,000.
- Inactive customers cannot sign in or place orders. The login error is the same as a bad password.
- A customer who does not own an order gets `404`, including on cancel.
- An administrator account has only the Admin role and cannot place orders.
- Page size defaults to 20 and cannot exceed 100.
- Swagger stays enabled outside Development so the API can be demonstrated. A production deployment can put it behind the gateway.

## Significant packages

| Package | Version | Role |
|---|---|---|
| Microsoft.EntityFrameworkCore | 10.0.9 | ORM, aligned with the MySQL provider |
| Microsoft.EntityFrameworkCore.Design | 10.0.9 | Migrations |
| MySql.EntityFrameworkCore | 10.0.9 | Official MySQL provider for EF Core 10. Pomelo 9.0.0 does not target EF Core 10 |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.12 | JWT bearer authentication |
| System.IdentityModel.Tokens.Jwt | 8.19.2 | Token creation |
| Microsoft.Extensions.Identity.Core | 10.0.12 | Password hashing |
| FluentValidation | 12.2.0 | Request validation |
| Swashbuckle.AspNetCore | 10.3.0 | Swagger / OpenAPI |
| xunit | 2.9.3 | Tests |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.12 | HTTP integration tests |

## Future improvements

These are not implemented:

- Rate limiting and account lockout
- Refresh tokens and token revocation
- Idempotency keys for order creation
- Metrics, distributed tracing, and an outbox for external events
- A background worker for payment or shipping integrations
- Customer email change and password reset
- Automatic password rehash when Identity reports `SuccessRehashNeeded`

## Configuration reference

| Setting | Purpose |
|---|---|
| `ConnectionStrings:DefaultConnection` | MySQL connection string |
| `Jwt:Issuer` | Token issuer |
| `Jwt:Audience` | Token audience |
| `Jwt:SigningKey` | HMAC key, at least 32 bytes |
| `Jwt:ExpirationMinutes` | Access token lifetime |
| `AdminSeed:ApplyOnStartup` | Create the administrator after migrations |
| `AdminSeed:Email` / `Password` / `Name` | Initial administrator |
| `ASPNETCORE_ENVIRONMENT` | `Development`, `Testing`, or `Production` |
