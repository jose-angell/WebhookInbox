# SPEC 01 — Docker Compose con PostgreSQL 16

> **Estado:** aprobado
> **Depende de:** —
> **Fecha:** 2026-11-26
> **Objetivo:** Levantar un PostgreSQL 16 local con docker-compose cuya base de datos, usuario, contraseña y puerto se leen de un archivo `.env`.

## Alcance

**Dentro:**

- `docker-compose.yml` en la raíz del repo con un único servicio `postgres` (imagen `postgres:16-alpine`).
- `POSTGRES_DB`, `POSTGRES_USER` y `POSTGRES_PASSWORD` se inyectan al contenedor desde `.env`.
- `POSTGRES_PORT` del `.env` define el puerto del host, mapeado al `5432` del contenedor.
- Volumen nombrado `pgdata` montado en `/var/lib/postgresql/data` para persistir datos.
- `.env.example` versionado con valores de ejemplo.
- `.env` añadido a `.gitignore`.
- Sección de comandos de Docker en `CLAUDE.md`.

**Fuera de alcance (para specs futuras):**

- Dockerfile y servicio contenerizado para la API `WebhookInbox`.
- Conexión de la API a PostgreSQL (Npgsql, EF Core, ConnectionString en `appsettings*.json`, migraciones).
- Healthcheck del contenedor.
- Herramientas de administración (pgAdmin, Adminer).
- Scripts de inicialización (`/docker-entrypoint-initdb.d`).

## Modelo de datos

Esta feature no introduce estructuras de datos de la aplicación. Solo introduce variables de entorno:

```dotenv
# .env.example
POSTGRES_DB=webhookinbox
POSTGRES_USER=webhookinbox
POSTGRES_PASSWORD=change_me
POSTGRES_PORT=5432
```

Convenciones:

- Los nombres `POSTGRES_DB`, `POSTGRES_USER` y `POSTGRES_PASSWORD` coinciden con las variables oficiales de la imagen `postgres`.
- `POSTGRES_PORT` solo se usa en `ports` del compose (`"${POSTGRES_PORT}:5432"`). El contenedor siempre escucha en `5432`.
- Docker Compose lee `.env` de la raíz automáticamente; no se usa `env_file` explícito.

## Plan de implementación

1. Añadir `.env` a `.gitignore`. Crear `.env.example` en la raíz con las cuatro variables. Verificar: `git status` no muestra `.env` tras copiarlo.
2. Crear `docker-compose.yml` en la raíz con el servicio `postgres` (`postgres:16-alpine`), `environment` con las tres variables `POSTGRES_*`, `ports: "${POSTGRES_PORT}:5432"` y volumen nombrado `pgdata`. Verificar: `docker compose config` resuelve los valores del `.env` sin advertencias.
3. Actualizar `CLAUDE.md` con los comandos: `cp .env.example .env`, `docker compose up -d`, `docker compose down`, `docker compose down -v` (borra datos).

## Criterios de aceptación

- [x] Existe `docker-compose.yml` en la raíz con un único servicio `postgres` y imagen `postgres:16-alpine`.
- [x] `docker-compose.yml` no contiene ningún valor literal de base de datos, usuario, contraseña ni puerto del host.
- [x] `.env.example` existe y contiene exactamente `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` y `POSTGRES_PORT`.
- [x] `.env` está en `.gitignore` y `git status` no lo lista después de crearlo.
- [x] Con `.env` copiado de `.env.example`, `docker compose up -d` arranca el contenedor sin errores.
- [x] `docker compose exec postgres psql -U webhookinbox -d webhookinbox -c "select version();"` devuelve una versión `PostgreSQL 16.x`.
- [x] Cambiar `POSTGRES_PORT=5433` en `.env` y recrear el contenedor expone PostgreSQL en el puerto `5433` del host (`docker compose ps` lo muestra).
- [x] Una tabla creada antes de `docker compose down` sigue existiendo tras `docker compose up -d`.
- [x] `docker compose down -v` elimina el volumen `pgdata`.
- [x] `CLAUDE.md` documenta los comandos de Docker.

## Decisiones

- **Sí:** solo el servicio PostgreSQL. La API sigue corriendo con `dotnet run`; contenerizarla va en otra spec.
- **No:** conectar la API a la BD en esta spec. La integración Npgsql/EF Core merece su propia spec.
- **Sí:** `.env` ignorado + `.env.example` versionado. Evita subir credenciales al repo.
- **No:** versionar `.env`. Expone la contraseña.
- **Sí:** nombres oficiales `POSTGRES_*`. Se pasan directo a la imagen sin renombrar.
- **No:** nombres propios (`DB_NAME`, etc.). Añaden una capa de mapeo sin beneficio.
- **Sí:** `.env` solo controla el puerto del host (`${POSTGRES_PORT}:5432`). El puerto interno fijo simplifica la config.
- **No:** configurar `PGPORT` dentro del contenedor. Más config, poco beneficio.
- **Sí:** `postgres:16-alpine`. Imagen ligera, suficiente para desarrollo.
- **No:** `postgres:16` (Debian). Más pesada sin necesidad actual.
- **Sí:** volumen nombrado `pgdata`. Los datos sobreviven a `docker compose down`.
- **No:** healthcheck. Descartado por el usuario; se puede añadir cuando la API dependa del contenedor.
- **Sí:** nombre `docker-compose.yml` en la raíz, junto a `WebhookInbox.slnx`.
- **No:** `compose.yaml`. Se prefirió el nombre clásico.

## Riesgos

| Riesgo | Mitigación |
| --- | --- |
| Falta `.env` y Compose arranca con variables vacías | `.env.example` documentado y paso `cp .env.example .env` en `CLAUDE.md`. |
| El puerto `5432` del host ya está ocupado por otro PostgreSQL | Cambiar `POSTGRES_PORT` en `.env`. |
| Cambiar `POSTGRES_USER`/`POSTGRES_PASSWORD` no afecta a un volumen ya inicializado | Documentar que hay que usar `docker compose down -v` para reinicializar. |

## Lo que **no** está en esta spec

- Dockerfile o servicio de la API.
- Conexión de la API a PostgreSQL (Npgsql, EF Core, migraciones).
- Healthcheck.
- pgAdmin / Adminer.
- Scripts de inicialización.

Cada una de esas, si llega, va en su propia spec.
