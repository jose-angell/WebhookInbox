# SPEC 03 — Infraestructura de persistencia con EF Core

> **Estado:** aprobado
> **Depende de:** SPEC 01, SPEC 02
> **Fecha:** 2026-11-27
> **Objetivo:** Crear en `WebhookInbox/Infrastructure/` el `AppDbContext` de EF Core sobre PostgreSQL con la configuración Fluent de `InboxEvent` y su migración inicial, registrado en DI.

## Alcance

**Dentro:**

- Paquetes NuGet en `WebhookInbox.csproj`: `Npgsql.EntityFrameworkCore.PostgreSQL`, `EFCore.NamingConventions` y `Microsoft.EntityFrameworkCore.Design` (este con `PrivateAssets=all`), todos en la línea 10.x.
- Carpeta plana `WebhookInbox/Infrastructure/` con `AppDbContext.cs`, `Configurations/InboxEventConfiguration.cs` y `Migrations/`.
- Configuración Fluent de `InboxEvent` con `IEntityTypeConfiguration<InboxEvent>`: clave, longitudes, índice único en `event_id` y token de concurrencia `xmin`.
- Nombres snake_case en BD mediante `UseSnakeCaseNamingConvention()`.
- Connection string `ConnectionStrings:WebhookInbox` en `appsettings.Development.json` con los valores de `.env.example`.
- `AddDbContext<AppDbContext>` en `Program.cs`.
- Migración inicial `InitialCreate` aplicada a mano con `dotnet ef database update`.
- Actualización de `CLAUDE.md` (project state, layout y comandos de migraciones).

**Fuera de alcance (para specs futuras):**

- Implementación `EfInboxEventRepository` de `IInboxEventRepository` y su registro en DI.
- Traducción de la violación del índice único (`23505`) a `DuplicateEventException`.
- Manejo de `DbUpdateConcurrencyException` en el flujo de procesamiento.
- Endpoints HTTP.
- Aplicar migraciones automáticamente al arrancar (`Database.Migrate()`).
- Tests de integración (Testcontainers, SQLite o InMemory).
- Connection string de producción, user-secrets o variables de entorno.
- Contenerizar la API.

## Modelo de datos

La entidad no cambia: se reutiliza `InboxEvent` de SPEC 02. Esta spec añade el mapeo a la tabla:

```sql
-- Esquema esperado tras la migración InitialCreate
CREATE TABLE inbox_events (
    id             uuid                     PRIMARY KEY,
    event_id       varchar(200)             NOT NULL,
    source         varchar(100)             NOT NULL,
    event_type     varchar(200)             NOT NULL,
    payload        text                     NOT NULL,
    status         integer                  NOT NULL,
    received_at    timestamp with time zone NOT NULL,
    processed_at   timestamp with time zone NULL,
    attempt_count  integer                  NOT NULL,
    last_error     text                     NULL
    -- xmin: columna de sistema de Postgres, usada como token de concurrencia
);
CREATE UNIQUE INDEX ix_inbox_events_event_id ON inbox_events (event_id);
```

```csharp
// WebhookInbox/Infrastructure/AppDbContext.cs
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<InboxEvent> InboxEvents => Set<InboxEvent>();
    // OnModelCreating: ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly)
}

// WebhookInbox/Infrastructure/Configurations/InboxEventConfiguration.cs
public sealed class InboxEventConfiguration : IEntityTypeConfiguration<InboxEvent> { ... }
```

```json
// WebhookInbox/appsettings.Development.json
"ConnectionStrings": {
  "WebhookInbox": "Host=localhost;Port=5432;Database=webhookinbox;Username=webhookinbox;Password=change_me"
}
```

Convenciones:

- Namespaces: `WebhookInbox.Infrastructure` y `WebhookInbox.Infrastructure.Configurations`. Las migraciones usan `WebhookInbox.Infrastructure.Migrations`.
- Tabla `inbox_events`. Nombres de columnas, PK e índice los genera `EFCore.NamingConventions` en snake_case.
- `Id` es la clave primaria y la genera el dominio (`ValueGeneratedNever()`).
- `Status` se guarda como entero (conversión por defecto del enum). Los valores actuales de `EventStatus` no se reordenan.
- `Payload` y `LastError` son `text` sin límite. `Payload` guarda el JSON crudo tal como llegó.
- `xmin` se mapea como propiedad sombra `uint` con `IsRowVersion()`. La entidad de dominio no gana ninguna propiedad.
- El dominio sigue sin referenciar EF Core. Toda la configuración vive en `Infrastructure/`.
- EF materializa `InboxEvent` con su constructor privado existente. Si EF no puede enlazarlo, se añade un constructor privado sin parámetros en la entidad, sin cambiar su comportamiento público.

## Plan de implementación

1. Añadir los tres paquetes NuGet a `WebhookInbox.csproj`. Verificar: `dotnet build WebhookInbox.slnx` compila.
2. Crear `Infrastructure/AppDbContext.cs` con `DbSet<InboxEvent>` y `ApplyConfigurationsFromAssembly`. Añadir `ConnectionStrings:WebhookInbox` a `appsettings.Development.json`. Registrar en `Program.cs` `AddDbContext<AppDbContext>(o => o.UseNpgsql(...).UseSnakeCaseNamingConvention())`. Verificar: compila y `dotnet run --project WebhookInbox --launch-profile http` arranca.
3. Crear `Infrastructure/Configurations/InboxEventConfiguration.cs` con tabla, clave, longitudes, requeridos, índice único en `EventId` y `xmin`. Verificar: compila.
4. Generar la migración con `dotnet ef migrations add InitialCreate --project WebhookInbox --output-dir Infrastructure/Migrations`. Revisar que el código generado coincide con el esquema de esta spec. Verificar: compila.
5. Con `docker compose up -d`, aplicar `dotnet ef database update --project WebhookInbox`. Verificar con `psql` el esquema de `inbox_events`.
6. Ejecutar `dotnet format WebhookInbox.slnx`. Actualizar `CLAUDE.md`: project state, `Infrastructure/` en layout y comandos `dotnet ef migrations add` / `dotnet ef database update`.

## Criterios de aceptación

- [ ] `dotnet build WebhookInbox.slnx` compila sin errores ni warnings nuevos.
- [ ] `dotnet test` sigue pasando todos los tests de SPEC 02.
- [ ] Existen `WebhookInbox/Infrastructure/AppDbContext.cs`, `Infrastructure/Configurations/InboxEventConfiguration.cs` y la migración `InitialCreate` en `Infrastructure/Migrations/`.
- [ ] `appsettings.Development.json` contiene `ConnectionStrings:WebhookInbox`.
- [ ] `Program.cs` registra `AppDbContext` con `UseNpgsql` y `UseSnakeCaseNamingConvention`.
- [ ] Con Postgres levantado, `dotnet ef database update --project WebhookInbox` termina sin errores.
- [ ] `\d inbox_events` en `psql` muestra las 10 columnas del esquema con los tipos y la nulabilidad indicados.
- [ ] `status` es de tipo `integer`.
- [ ] `payload` es de tipo `text`.
- [ ] Existe un índice único sobre `event_id`.
- [ ] `select * from "__EFMigrationsHistory";` devuelve una fila con `InitialCreate`.
- [ ] `InboxEventConfiguration` marca `xmin` como row version (visible en el model snapshot de la migración).
- [ ] Ningún archivo en `WebhookInbox/Domain/` tiene `using` de `Microsoft.EntityFrameworkCore`.
- [ ] `dotnet run --project WebhookInbox --launch-profile http` arranca y `GET /weatherforecast` sigue respondiendo.
- [ ] `CLAUDE.md` documenta la carpeta `Infrastructure/` y los comandos de migraciones.

## Decisiones

- **Sí:** DbContext, configuración Fluent, migración y registro en DI. Es lo mínimo para que `dotnet ef` use el host real.
- **No:** repositorio EF en esta spec. Va con sus tests de integración y la traducción de errores en otra spec.
- **No:** `IDesignTimeDbContextFactory`. Con `AddDbContext` en `Program.cs` no hace falta.
- **Sí:** carpeta plana `Infrastructure/` (`AppDbContext.cs`, `Configurations/`, `Migrations/`). Elegido por el usuario.
- **No:** subcarpeta `Infrastructure/Persistence/`. Se prefirió menos anidamiento.
- **Sí:** connection string en `appsettings.Development.json` con los valores de `.env.example`. Simple y coherente con SPEC 01. Se puede sobrescribir con `ConnectionStrings__WebhookInbox`.
- **No:** user-secrets ni variable de entorno obligatoria. Más fricción para desarrollo local.
- **Sí:** snake_case con `EFCore.NamingConventions`. Idiomático en Postgres y evita comillas en SQL manual.
- **No:** PascalCase por defecto. Obliga a citar identificadores.
- **Sí:** `Status` como entero. Elegido por el usuario: más compacto.
- **No:** `Status` como string. Más legible, pero descartado.
- **Sí:** `Payload` como `text`. Conserva el cuerpo exacto, como decidió SPEC 02.
- **No:** `jsonb`. Reordena claves y quita espacios, y rompería una futura verificación de firmas.
- **No:** `json`. Rechazaría payloads no-JSON.
- **Sí:** índice único en `event_id`. Garantiza en BD la regla de SPEC 02 ante concurrencia.
- **Sí:** longitudes máximas (`event_id` 200, `source` 100, `event_type` 200). Acotan datos basura del emisor.
- **Sí:** `xmin` como token de concurrencia en propiedad sombra. Mitiga dos workers sobre el mismo evento sin tocar el dominio.
- **Sí:** migraciones aplicadas a mano con `dotnet ef database update`. Explícito y la API no depende de Postgres para arrancar.
- **No:** `Database.Migrate()` al arrancar.
- **Sí:** `dotnet-ef` global (ya instalado, 10.0.6).
- **No:** tests nuevos. La verificación es manual con `psql`; los tests de integración llegan con el repositorio.

## Riesgos

| Riesgo | Mitigación |
| --- | --- |
| EF no puede materializar `InboxEvent` con su constructor privado con parámetros | Añadir un constructor privado sin parámetros en la entidad. No cambia la API pública ni rompe tests. |
| Reordenar `EventStatus` corrompe los datos guardados como entero | Convención documentada: no reordenar. Valores nuevos siempre al final. |
| Postgres no está levantado al ejecutar `dotnet ef database update` | Paso documentado: `docker compose up -d` antes. |
| `POSTGRES_PORT` en `.env` distinto de `5432` | Ajustar el `Port` de la connection string o sobrescribirla con `ConnectionStrings__WebhookInbox`. |
| La contraseña `change_me` queda versionada en `appsettings.Development.json` | Es solo de desarrollo local y ya está en `.env.example`. Producción va en otra spec. |

## Lo que **no** está en esta spec

- Repositorio `EfInboxEventRepository` y su registro en DI.
- Traducción de errores de BD a excepciones de dominio.
- Endpoints HTTP.
- Migraciones automáticas al arrancar.
- Tests de integración.
- Configuración de producción.

Cada una de esas, si llega, va en su propia spec.
