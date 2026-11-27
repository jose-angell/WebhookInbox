# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project state

WebhookInbox is an ASP.NET Core minimal API project on .NET 10 (`net10.0`, nullable and implicit usings on). `WebhookInbox/Program.cs` is still the unchanged `webapi` template and the only endpoint is the sample `GET /weatherforecast`. The domain model of the inbox exists in `WebhookInbox/Domain/` (`InboxEvent` with its lifecycle `Received → Processing → Processed/Failed`, and `InboxEventRegistrar`, which rejects an already registered `EventId`), covered by xUnit tests in `WebhookInbox.Tests/`. There are no controllers, DI registrations or persistence yet. The real `IInboxEventRepository` implementation (EF Core) is still to be built and will go in an `Infrastructure/` folder.

## Layout

- `WebhookInbox.slnx`: solution file in the new XML `.slnx` format (needs .NET 9.0.200+ SDK / VS 2022 17.13+).
- `WebhookInbox/`: the web project. Endpoints are mapped inline in `Program.cs` with `app.MapGet/MapPost`.
- `WebhookInbox/Domain/`: domain layer, organized by type: `Entities/`, `Enums/`, `Exceptions/`, `Repositories/` (contracts only) and `Services/`. It must not reference ASP.NET Core or EF Core; time comes in as a parameter or through `TimeProvider`.
- `WebhookInbox.Tests/`: xUnit test project referencing `WebhookInbox`. Domain tests are in `Domain/`, with an in-memory repository fake that lives only here.
- `WebhookInbox/WebhookInbox.http`: request samples for the VS/Rider HTTP client (`@WebhookInbox_HostAddress = http://localhost:5288`). Update it when you add endpoints.

## Commands

Run these from the repo root:

- Build: `dotnet build WebhookInbox.slnx`
- Run (HTTP, port 5288): `dotnet run --project WebhookInbox --launch-profile http`
- Run (HTTPS 7283 + HTTP 5288): `dotnet run --project WebhookInbox --launch-profile https`
- Format/lint: `dotnet format WebhookInbox.slnx`

Run the tests with `dotnet test`. Run a single test with `dotnet test --filter "FullyQualifiedName~<Name>"`.

## Docker (PostgreSQL)

`docker-compose.yml` defines a single `postgres` service (`postgres:16-alpine`). Its database, user, password and host port are read from `.env` (copy it from `.env.example`; `.env` is git-ignored). The API is not containerized yet and still runs with `dotnet run`.

Run these from the repo root:

- First-time setup: `cp .env.example .env`
- Start PostgreSQL: `docker compose up -d`
- Stop it (data is kept in the `pgdata` volume): `docker compose down`
- Stop it and delete the data: `docker compose down -v`

Changing `POSTGRES_USER` or `POSTGRES_PASSWORD` has no effect on an already-initialized volume. Run `docker compose down -v` to reinitialize. To use another host port (e.g. if 5432 is taken), change `POSTGRES_PORT` in `.env` and recreate the container.

## Notes

- `app.UseHttpsRedirection()` is on. When you call the app over HTTP with the `http` profile, a redirect warning can show up in the logs. This is expected.
- Both launch profiles set `ASPNETCORE_ENVIRONMENT=Development`, so `appsettings.Development.json` applies when running locally.
