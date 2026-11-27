# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project state

WebhookInbox is an ASP.NET Core minimal API project on .NET 10 (`net10.0`, nullable and implicit usings on). Right now it is still the unchanged `webapi` template: `WebhookInbox/Program.cs` sets up the whole app, and the only endpoint is the sample `GET /weatherforecast`. There are no test projects, controllers, DI registrations or persistence yet. The webhook-inbox features still need to be built.

## Layout

- `WebhookInbox.slnx`: solution file in the new XML `.slnx` format (needs .NET 9.0.200+ SDK / VS 2022 17.13+).
- `WebhookInbox/`: the single web project. Endpoints are mapped inline in `Program.cs` with `app.MapGet/MapPost`.
- `WebhookInbox/WebhookInbox.http`: request samples for the VS/Rider HTTP client (`@WebhookInbox_HostAddress = http://localhost:5288`). Update it when you add endpoints.

## Commands

Run these from the repo root:

- Build: `dotnet build WebhookInbox.slnx`
- Run (HTTP, port 5288): `dotnet run --project WebhookInbox --launch-profile http`
- Run (HTTPS 7283 + HTTP 5288): `dotnet run --project WebhookInbox --launch-profile https`
- Format/lint: `dotnet format WebhookInbox.slnx`

When a test project is added, run it with `dotnet test`. Run a single test with `dotnet test --filter "FullyQualifiedName~<Name>"`.

## Notes

- `app.UseHttpsRedirection()` is on. When you call the app over HTTP with the `http` profile, a redirect warning can show up in the logs. This is expected.
- Both launch profiles set `ASPNETCORE_ENVIRONMENT=Development`, so `appsettings.Development.json` applies when running locally.
