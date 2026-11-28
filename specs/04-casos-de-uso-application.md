# SPEC 04 — Casos de uso del inbox en la capa Application

> **Estado:** implementado
> **Depende de:** SPEC 02, SPEC 03
> **Fecha:** 2026-11-27
> **Objetivo:** Crear en `WebhookInbox/Application/` un servicio `InboxEventService` con los casos de uso registrar, procesar, consultar por `EventId` y listar eventos, respaldado por un `EfInboxEventRepository` registrado en DI.

## Alcance

**Dentro:**

- Ampliar el contrato `IInboxEventRepository` con `GetByEventIdAsync`, `ListAsync` y `UpdateAsync`.
- Nuevas excepciones de dominio `EventNotFoundException` y `ConcurrencyConflictException`.
- Carpeta `WebhookInbox/Application/` organizada por tipo: `Services/`, `Dtos/`, `Abstractions/`.
- `InboxEventService` con cuatro casos de uso: `RegisterAsync`, `ProcessAsync`, `GetByEventIdAsync`, `ListAsync`.
- DTOs `InboxEventDto` y `PagedResult<T>`.
- Interfaz `IInboxEventProcessor` y su implementación `NoOpInboxEventProcessor` en `Infrastructure/`.
- `EfInboxEventRepository` en `Infrastructure/`, con `SaveChangesAsync` dentro del propio repositorio.
- Traducción en el repositorio: violación `23505` → `DuplicateEventException` y `DbUpdateConcurrencyException` → `ConcurrencyConflictException`.
- Registro en DI en `Program.cs` de `TimeProvider`, repositorio, registrar, procesador y servicio.
- Tests unitarios de `InboxEventService` con el fake en memoria, que se amplía con los nuevos métodos.
- Actualización de `CLAUDE.md`.

**Fuera de alcance (para specs futuras):**

- Endpoints HTTP y traducción de excepciones a códigos HTTP.
- Tests de integración del repositorio EF (Testcontainers o Postgres de compose).
- Lógica real de procesamiento por tipo de evento (implementaciones reales de `IInboxEventProcessor`).
- Worker o procesador en background.
- Límite de reintentos.
- Paginación por cursor.
- Consulta por `Id` interno.
- MediatR o patrón Result.
- `IUnitOfWork`.

## Modelo de datos

La entidad `InboxEvent` y la tabla `inbox_events` no cambian. No hay migración nueva.

```csharp
// WebhookInbox/Domain/Repositories/IInboxEventRepository.cs (ampliado)
public interface IInboxEventRepository
{
    Task<bool> ExistsAsync(string eventId, CancellationToken ct);
    Task AddAsync(InboxEvent inboxEvent, CancellationToken ct);
    Task<InboxEvent?> GetByEventIdAsync(string eventId, CancellationToken ct);
    Task<(IReadOnlyList<InboxEvent> Items, int Total)> ListAsync(EventStatus? status, int skip, int take, CancellationToken ct);
    Task UpdateAsync(InboxEvent inboxEvent, CancellationToken ct);
}

// WebhookInbox/Domain/Exceptions/
public sealed class EventNotFoundException(string eventId) : DomainException(...) { public string EventId { get; } }
public sealed class ConcurrencyConflictException(string eventId) : DomainException(...) { public string EventId { get; } }

// WebhookInbox/Application/Dtos/InboxEventDto.cs
public sealed record InboxEventDto(Guid Id, string EventId, string Source, string EventType, string Payload,
    EventStatus Status, DateTimeOffset ReceivedAt, DateTimeOffset? ProcessedAt, int AttemptCount, string? LastError);

// WebhookInbox/Application/Dtos/PagedResult.cs
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

// WebhookInbox/Application/Abstractions/IInboxEventProcessor.cs
public interface IInboxEventProcessor
{
    Task ProcessAsync(InboxEventDto inboxEvent, CancellationToken ct);
}

// WebhookInbox/Application/Services/InboxEventService.cs
public sealed class InboxEventService(InboxEventRegistrar registrar, IInboxEventRepository repository,
                                      IInboxEventProcessor processor, TimeProvider timeProvider)
{
    public Task<InboxEventDto> RegisterAsync(string eventId, string source, string eventType, string payload, CancellationToken ct);
    public Task<InboxEventDto> ProcessAsync(string eventId, CancellationToken ct);
    public Task<InboxEventDto> GetByEventIdAsync(string eventId, CancellationToken ct);
    public Task<PagedResult<InboxEventDto>> ListAsync(EventStatus? status, int page, int pageSize, CancellationToken ct);
}

// WebhookInbox/Infrastructure/EfInboxEventRepository.cs
public sealed class EfInboxEventRepository(AppDbContext db) : IInboxEventRepository { ... }

// WebhookInbox/Infrastructure/NoOpInboxEventProcessor.cs
public sealed class NoOpInboxEventProcessor : IInboxEventProcessor { ... } // devuelve Task.CompletedTask
```

Flujo de `ProcessAsync(eventId)`:

1. `GetByEventIdAsync`. Si es `null` lanza `EventNotFoundException`.
2. `StartProcessing()` y `UpdateAsync` (el `xmin` impide que dos llamadas simultáneas arranquen el mismo evento).
3. Llama a `processor.ProcessAsync(dto, ct)`.
4. Si termina bien: `MarkProcessed(timeProvider.GetUtcNow())` y `UpdateAsync`.
5. Si lanza: `MarkFailed(ex.Message)` y `UpdateAsync`. Devuelve el DTO en estado `Failed` sin relanzar.
6. Devuelve el DTO del estado final.

Convenciones:

- Namespaces: `WebhookInbox.Application.Services`, `WebhookInbox.Application.Dtos`, `WebhookInbox.Application.Abstractions`. El repositorio y el procesador no-op usan `WebhookInbox.Infrastructure`.
- `Application/` referencia `Domain/` y nunca `Infrastructure/`, ASP.NET Core ni EF Core.
- `Domain/` no referencia `Application/`.
- `RegisterAsync` delega en `InboxEventRegistrar` existente y no duplica su lógica.
- `GetByEventIdAsync` del servicio lanza `EventNotFoundException` si no existe. El repositorio devuelve `null`.
- `ListAsync`: `page` empieza en 1. `pageSize` entre 1 y 100. Fuera de rango lanza `ArgumentOutOfRangeException`. Orden `ReceivedAt` descendente. `status` `null` = sin filtro.
- El servicio traduce `page` y `pageSize` a `skip = (page - 1) * pageSize` y `take = pageSize` antes de llamar al repositorio.
- `ProcessAsync` sobre un evento `Processed` o `Processing` deja propagar `InvalidEventTransitionException` sin llamar al procesador.
- `OperationCanceledException` del procesador no marca el evento como `Failed`: se relanza.
- `AddAsync` y `UpdateAsync` llaman a `SaveChangesAsync`. `GetByEventIdAsync` usa consulta con tracking. `ListAsync` usa `AsNoTracking`.
- `AddAsync` captura `DbUpdateException` con `PostgresException { SqlState: "23505" }` y lanza `DuplicateEventException`.
- `UpdateAsync` captura `DbUpdateConcurrencyException` y lanza `ConcurrencyConflictException`.
- La conversión entidad → DTO es un método estático `InboxEventDto.From(InboxEvent)`.
- DI en `Program.cs`: `TimeProvider.System` como singleton. Repositorio, `InboxEventRegistrar`, `IInboxEventProcessor` e `InboxEventService` como scoped.

## Plan de implementación

1. Crear `EventNotFoundException` y `ConcurrencyConflictException` en `Domain/Exceptions/`. Ampliar `IInboxEventRepository` con los tres métodos nuevos y actualizar `WebhookInbox.Tests/Domain/InMemoryInboxEventRepository.cs` para implementarlos. Verificar: compila y `dotnet test` pasa.
2. Crear `Application/Dtos/InboxEventDto.cs` (con `From`) y `Application/Dtos/PagedResult.cs`. Crear `Application/Abstractions/IInboxEventProcessor.cs`. Verificar: compila.
3. Crear `Application/Services/InboxEventService.cs` con `RegisterAsync`, `GetByEventIdAsync` y `ListAsync`. Tests en `WebhookInbox.Tests/Application/InboxEventServiceTests.cs`: registro nuevo, duplicado, consulta existente, consulta inexistente, listado con filtro, paginación y rango inválido.
4. Añadir `ProcessAsync` al servicio. Crear un procesador fake en `WebhookInbox.Tests/Application/` que pueda terminar bien o lanzar. Tests: éxito → `Processed`, fallo → `Failed` con `LastError`, reintento desde `Failed`, evento `Processed` lanza sin llamar al procesador, inexistente lanza `EventNotFoundException`.
5. Crear `Infrastructure/EfInboxEventRepository.cs` y `Infrastructure/NoOpInboxEventProcessor.cs` con la traducción de errores descrita. Verificar: compila.
6. Registrar en `Program.cs` `TimeProvider.System`, `EfInboxEventRepository`, `InboxEventRegistrar`, `NoOpInboxEventProcessor` e `InboxEventService`. Verificar: `dotnet run --project WebhookInbox --launch-profile http` arranca sin errores de validación de DI y `GET /weatherforecast` responde.
7. Ejecutar `dotnet format WebhookInbox.slnx`. Actualizar `CLAUDE.md`: project state, `Application/` en layout y mención de los tests en `WebhookInbox.Tests/Application/`.

## Criterios de aceptación

- [x] `dotnet build WebhookInbox.slnx` compila sin errores ni warnings nuevos.
- [x] `dotnet test` pasa todos los tests, incluidos los de SPEC 02.
- [x] Existen `WebhookInbox/Application/Services/InboxEventService.cs`, `Application/Dtos/InboxEventDto.cs`, `Application/Dtos/PagedResult.cs` y `Application/Abstractions/IInboxEventProcessor.cs`.
- [x] Existen `Infrastructure/EfInboxEventRepository.cs` e `Infrastructure/NoOpInboxEventProcessor.cs`.
- [x] `IInboxEventRepository` declara `GetByEventIdAsync`, `ListAsync` y `UpdateAsync`.
- [x] `RegisterAsync` con un `EventId` nuevo devuelve un `InboxEventDto` con `Status == Received`.
- [x] `RegisterAsync` con un `EventId` existente lanza `DuplicateEventException`.
- [x] `GetByEventIdAsync` con un `EventId` inexistente lanza `EventNotFoundException`.
- [x] `ListAsync(EventStatus.Failed, 1, 20)` devuelve solo eventos `Failed`, ordenados por `ReceivedAt` descendente, con `Total` correcto.
- [x] `ListAsync` con `pageSize` 0 o 101 lanza `ArgumentOutOfRangeException`.
- [x] `ProcessAsync` con procesador que termina bien devuelve `Status == Processed` y `ProcessedAt` igual a la hora del `TimeProvider`.
- [x] `ProcessAsync` con procesador que lanza devuelve `Status == Failed`, `LastError` igual al mensaje y no relanza.
- [x] `ProcessAsync` sobre un evento `Failed` lo procesa de nuevo y `AttemptCount` pasa a 2.
- [x] `ProcessAsync` sobre un evento `Processed` lanza `InvalidEventTransitionException` y no llama al procesador.
- [x] `EfInboxEventRepository.AddAsync` traduce `SqlState 23505` a `DuplicateEventException` (revisión de código).
- [x] `EfInboxEventRepository.UpdateAsync` traduce `DbUpdateConcurrencyException` a `ConcurrencyConflictException` (revisión de código).
- [x] Ningún archivo en `WebhookInbox/Application/` tiene `using` de `Microsoft.AspNetCore`, `Microsoft.EntityFrameworkCore` ni `WebhookInbox.Infrastructure`.
- [x] Ningún archivo en `WebhookInbox/Domain/` tiene `using` de `WebhookInbox.Application` ni de `Microsoft.EntityFrameworkCore`.
- [x] `dotnet run --project WebhookInbox --launch-profile http` arranca en Development sin errores de validación de DI.
- [x] No hay migración nueva en `Infrastructure/Migrations/`.
- [x] `CLAUDE.md` documenta la carpeta `Application/`.

## Decisiones

- **Sí:** los cuatro casos de uso (registrar, procesar, consultar, listar) en una sola spec. Elegido por el usuario.
- **No:** dividir en una spec de repositorio y otra de Application. Se propuso por tocar varias áreas; el usuario prefirió un solo ciclo.
- **Sí:** un servicio de aplicación `InboxEventService` con un método por caso de uso. Elegido por el usuario.
- **No:** un handler por caso de uso ni MediatR. MediatR añade un paquete con licencia comercial e indirección.
- **Sí:** DTOs y excepciones de dominio propagadas. Coherente con SPEC 02 y desacopla la futura API de la entidad.
- **No:** devolver la entidad ni patrón Result. SPEC 02 ya descartó Result.
- **Sí:** `EfInboxEventRepository` en esta spec. Elegido por el usuario para que los casos de uso tengan persistencia real.
- **Sí:** `SaveChangesAsync` dentro del repositorio. Simple. `ProcessAsync` guarda tras `StartProcessing` y tras `Mark*`.
- **No:** `IUnitOfWork`. Más piezas sin necesidad actual.
- **Sí:** `ProcessAsync` con `IInboxEventProcessor`. El servicio controla el ciclo de vida y la lógica por tipo de evento se enchufa después.
- **No:** exponer `StartProcessing`, `MarkProcessed` y `MarkFailed` por separado. Deja el ciclo en manos de un actor externo.
- **Sí:** `NoOpInboxEventProcessor` en `Infrastructure/`. Permite resolver el grafo de DI sin lógica real.
- **Sí:** fallo del procesador → `MarkFailed` y devolver el DTO sin relanzar. Un fallo es un resultado esperado del ciclo.
- **No:** relanzar tras `MarkFailed`.
- **Sí:** consultar y procesar por `EventId` del emisor. Elegido por el usuario: es el id que conoce el emisor.
- **No:** consulta por `Id` interno.
- **Sí:** listado con filtro opcional por estado y paginación offset (`page`, `pageSize` 20 por defecto, máximo 100), orden `ReceivedAt` desc, con `Total`.
- **No:** paginación por cursor ni listado sin paginar.
- **Sí:** nuevas `EventNotFoundException` y `ConcurrencyConflictException` en `Domain/Exceptions/`. Coherente con `DuplicateEventException`.
- **Sí:** carpetas por tipo en `Application/` (`Services/`, `Dtos/`, `Abstractions/`). Coherente con `Domain/`.
- **No:** `Application/` plana.
- **Sí:** tests solo con el fake en memoria. Elegido por el usuario.
- **No:** tests de integración del repositorio EF (Testcontainers o Postgres de compose). Van en otra spec.
- **Sí:** verificación del repositorio EF por compilación, revisión de código y arranque con validación de DI. El uso real contra Postgres se prueba con la spec de endpoints.
- **No:** endpoints mínimos para probar a mano. Ampliaría el alcance a la capa HTTP.

## Riesgos

| Riesgo | Mitigación |
| --- | --- |
| `EfInboxEventRepository` no se ejecuta contra Postgres en esta spec y puede tener errores de consulta o traducción | Revisión de código en los criterios. La spec de endpoints o de tests de integración lo ejercita de verdad. |
| El fake en memoria no reproduce el índice único ni `xmin` | Los tests cubren la lógica del servicio, no la concurrencia. La traducción de errores vive solo en el repositorio EF. |
| Un fallo al guardar `MarkFailed` deja el evento en `Processing` para siempre | Se acepta en esta spec. Un worker o una spec de recuperación de eventos atascados lo resolverá. |
| `ex.Message` del procesador puede filtrar detalles internos en `LastError` | Aceptado: `LastError` es dato interno del inbox. La API decidirá qué exponer. |
| Cancelar la petición en medio de `ProcessAsync` deja el evento en `Processing` | `OperationCanceledException` se relanza sin marcar `Failed`. Recuperación de atascados en otra spec. |

## Lo que **no** está en esta spec

- Endpoints HTTP y mapeo de excepciones a códigos de estado.
- Tests de integración del repositorio EF.
- Procesadores reales por tipo de evento.
- Worker en background y recuperación de eventos atascados en `Processing`.
- Límite de reintentos.
- Paginación por cursor y consulta por `Id` interno.

Cada una de esas, si llega, va en su propia spec.
