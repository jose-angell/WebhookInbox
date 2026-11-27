# SPEC 02 — Dominio de eventos del inbox

> **Estado:** aprobado
> **Depende de:** SPEC 01
> **Fecha:** 2026-11-26
> **Objetivo:** Modelar en `WebhookInbox/Domain/` la entidad `InboxEvent` con su ciclo de vida y la regla de que un mismo `EventId` no se registra ni se procesa con éxito más de una vez, cubierto con tests unitarios.

## Alcance

**Dentro:**

- Carpeta `WebhookInbox/Domain/` organizada por tipo: `Entities/`, `Enums/`, `Exceptions/`, `Repositories/`, `Services/`.
- Entidad `InboxEvent` con fábrica `Receive(...)` y métodos de transición de estado.
- Enum `EventStatus` con `Received`, `Processing`, `Processed`, `Failed`.
- Excepciones de dominio: `DomainException` (base), `InvalidEventTransitionException`, `DuplicateEventException`.
- Interfaz `IInboxEventRepository` (solo el contrato).
- Servicio `InboxEventRegistrar`, que rechaza un `EventId` ya registrado.
- `ProjectReference` de `WebhookInbox.Tests` a `WebhookInbox`, borrado de `UnitTest1.cs` y tests xUnit en `WebhookInbox.Tests/Domain/`.
- Repositorio fake en memoria, solo dentro del proyecto de tests.

**Fuera de alcance (para specs futuras):**

- EF Core, Npgsql, `DbContext`, connection string y migraciones.
- Implementación real de `IInboxEventRepository` e índice único en `EventId`.
- Endpoints HTTP (`POST` de recepción, consultas) y traducción de excepciones a códigos HTTP.
- Registro de servicios en DI dentro de `Program.cs`.
- Worker o procesador en background que consuma eventos.
- Límite máximo de reintentos.
- Validación de firmas o autenticación del emisor.
- Unicidad por `(Source, EventId)`.

## Modelo de datos

```csharp
// WebhookInbox/Domain/Enums/EventStatus.cs
public enum EventStatus { Received, Processing, Processed, Failed }

// WebhookInbox/Domain/Entities/InboxEvent.cs
public sealed class InboxEvent
{
    public Guid Id { get; private set; }               // id interno
    public string EventId { get; private set; }        // id del emisor externo, único global
    public string Source { get; private set; }         // p. ej. "stripe", "github"
    public string EventType { get; private set; }      // p. ej. "invoice.paid"
    public string Payload { get; private set; }        // JSON crudo tal como llegó
    public EventStatus Status { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }

    public static InboxEvent Receive(string eventId, string source, string eventType,
                                     string payload, DateTimeOffset receivedAt);
    public void StartProcessing();
    public void MarkProcessed(DateTimeOffset processedAt);
    public void MarkFailed(string error);
}

// WebhookInbox/Domain/Repositories/IInboxEventRepository.cs
public interface IInboxEventRepository
{
    Task<bool> ExistsAsync(string eventId, CancellationToken ct);
    Task AddAsync(InboxEvent inboxEvent, CancellationToken ct);
}

// WebhookInbox/Domain/Services/InboxEventRegistrar.cs
public sealed class InboxEventRegistrar(IInboxEventRepository repository, TimeProvider timeProvider)
{
    public Task<InboxEvent> RegisterAsync(string eventId, string source, string eventType,
                                          string payload, CancellationToken ct);
}
```

Transiciones válidas:

| Desde | Método | Hacia | Efecto |
| --- | --- | --- | --- |
| `Received` | `StartProcessing()` | `Processing` | `AttemptCount++` |
| `Failed` | `StartProcessing()` | `Processing` | `AttemptCount++` (reintento) |
| `Processing` | `MarkProcessed(at)` | `Processed` | `ProcessedAt = at`, `LastError = null` |
| `Processing` | `MarkFailed(error)` | `Failed` | `LastError = error` |

Convenciones:

- Cualquier transición que no esté en la tabla lanza `InvalidEventTransitionException` con el estado actual y la operación intentada.
- `Processed` es terminal: un evento procesado con éxito nunca vuelve a `Processing`.
- `Receive` deja `Status = Received`, `AttemptCount = 0` y `Id = Guid.NewGuid()`.
- `Receive` lanza `ArgumentException` si `eventId`, `source` o `eventType` son null o vacíos, o si `payload` es null.
- `MarkFailed` lanza `ArgumentException` si `error` es null o vacío.
- `EventId` se compara de forma exacta (ordinal, sin normalizar mayúsculas ni espacios).
- Namespaces: `WebhookInbox.Domain.Entities`, `WebhookInbox.Domain.Enums`, `WebhookInbox.Domain.Exceptions`, `WebhookInbox.Domain.Repositories`, `WebhookInbox.Domain.Services`.
- El dominio no referencia ASP.NET Core ni EF Core. La hora llega por parámetro o por `TimeProvider`.

## Plan de implementación

1. Añadir a `WebhookInbox.Tests.csproj` el `ProjectReference` a `../WebhookInbox/WebhookInbox.csproj` y borrar `UnitTest1.cs`. Verificar: `dotnet build WebhookInbox.slnx` compila y `dotnet test` corre sin tests.
2. Crear `Domain/Enums/EventStatus.cs` y `Domain/Exceptions/` con `DomainException`, `InvalidEventTransitionException` y `DuplicateEventException`. Verificar: compila.
3. Crear `Domain/Entities/InboxEvent.cs` con las propiedades y la fábrica `Receive` con sus validaciones. Tests en `WebhookInbox.Tests/Domain/InboxEventTests.cs` para el estado inicial y los argumentos inválidos.
4. Implementar `StartProcessing`, `MarkProcessed` y `MarkFailed` según la tabla de transiciones. Añadir tests para cada transición válida y para las inválidas (sobre todo `Processed → StartProcessing`).
5. Crear `Domain/Repositories/IInboxEventRepository.cs` y `Domain/Services/InboxEventRegistrar.cs`. Crear `WebhookInbox.Tests/Domain/InMemoryInboxEventRepository.cs` y `InboxEventRegistrarTests.cs`, que cubren el registro nuevo, el duplicado y el uso de `TimeProvider` para `ReceivedAt`.
6. Ejecutar `dotnet format WebhookInbox.slnx`. Actualizar la sección "Project state" y la de layout de `CLAUDE.md` para mencionar `Domain/` y el proyecto de tests.

## Criterios de aceptación

- [ ] `dotnet build WebhookInbox.slnx` compila sin errores ni warnings nuevos.
- [ ] `dotnet test` pasa todos los tests y hay al menos un test por cada fila de la tabla de transiciones.
- [ ] Existen las carpetas `WebhookInbox/Domain/Entities`, `Enums`, `Exceptions`, `Repositories` y `Services` con los archivos descritos.
- [ ] `InboxEvent.Receive(...)` deja `Status == Received`, `AttemptCount == 0`, `ProcessedAt == null` y `LastError == null`.
- [ ] `Receive` con `eventId` vacío lanza `ArgumentException`.
- [ ] Llamar a `StartProcessing()` sobre un evento `Processed` lanza `InvalidEventTransitionException`.
- [ ] `MarkProcessed` sobre un evento `Received` (sin pasar por `Processing`) lanza `InvalidEventTransitionException`.
- [ ] Un evento `Failed` puede volver a `Processing`, y `AttemptCount` pasa de 1 a 2.
- [ ] `InboxEventRegistrar.RegisterAsync` con un `EventId` ya existente lanza `DuplicateEventException` y no llama a `AddAsync`.
- [ ] `RegisterAsync` con un `EventId` nuevo llama a `AddAsync` una vez y devuelve un evento `Received` con `ReceivedAt` igual a la hora del `TimeProvider`.
- [ ] Ningún archivo en `WebhookInbox/Domain/` tiene `using` de `Microsoft.AspNetCore` ni de `Microsoft.EntityFrameworkCore`.
- [ ] `UnitTest1.cs` ya no existe.
- [ ] `Program.cs` no tiene cambios.

## Decisiones

- **Sí:** solo dominio en esta spec. Persistencia y endpoints van en specs propias para no mezclar tres áreas.
- **No:** EF Core en esta spec. Requiere decisiones propias (mapeo, migraciones, connection string).
- **Sí:** carpetas por tipo (`Entities/`, `Enums/`, `Exceptions/`, `Repositories/`, `Services/`). Elegido por el usuario.
- **No:** `Domain/` plano ni carpetas por agregado.
- **Sí:** 4 estados con reintento desde `Failed`. `Processed` es terminal, y eso impide procesar dos veces con éxito.
- **No:** límite de intentos. Se puede añadir sobre `AttemptCount` en otra spec.
- **Sí:** `EventId` como `string` único global. Los emisores externos no siempre mandan GUIDs.
- **No:** unicidad por `(Source, EventId)` ni `Guid`. Lo primero queda para el futuro si hace falta y lo segundo rechazaría ids válidos.
- **Sí:** `Id` interno `Guid` separado de `EventId`. Desacopla la clave primaria del dato externo.
- **Sí:** `Payload` como `string` con el JSON crudo. Guarda exactamente lo recibido.
- **No:** `JsonDocument`. Es `IDisposable` y complica la entidad.
- **Sí:** excepciones de dominio propias. Son explícitas y la API las traducirá a HTTP más adelante.
- **No:** patrón Result ni idempotencia silenciosa. El primero añade más código y la segunda oculta errores de flujo.
- **Sí:** el contrato `IInboxEventRepository` y `InboxEventRegistrar` viven en el dominio y el índice único irá en la BD. El dominio expresa la regla y la BD la garantiza ante concurrencia.
- **Sí:** `TimeProvider` en el registrar. Hace que los tests de fechas sean deterministas sin paquetes extra.
- **Sí:** tests xUnit en el `WebhookInbox.Tests` existente con un fake en memoria. No se añaden librerías de mocking.

## Riesgos

| Riesgo | Mitigación |
| --- | --- |
| Dos peticiones simultáneas con el mismo `EventId` pasan ambas `ExistsAsync` (carrera check-then-act) | La spec de persistencia debe añadir un índice único en `EventId` y traducir la violación a `DuplicateEventException`. |
| Dos workers ejecutan `StartProcessing` sobre el mismo evento a la vez | Fuera de este dominio en memoria. La spec de persistencia/procesamiento debe usar concurrencia optimista o bloqueo de fila. |
| El emisor reenvía el mismo evento con distinta capitalización de `EventId` | Comparación ordinal exacta y documentada. Si aparece el caso, se decide en otra spec. |

## Lo que **no** está en esta spec

- EF Core, Npgsql, `DbContext`, migraciones e índice único.
- Implementación real del repositorio.
- Endpoints HTTP y mapeo de excepciones a códigos de estado.
- Registro en DI en `Program.cs`.
- Worker de procesamiento en background.
- Límite de reintentos.
- Validación de firmas del emisor.

Cada una de esas, si llega, va en su propia spec.
