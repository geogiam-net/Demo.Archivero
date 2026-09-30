# Local Docker development stack

Start with Linux/WSL x86-64, Docker Engine and the Docker Compose plugin installed.
No .NET SDK or existing service containers are required on the host.

```bash
cd docker
cp .env.example .env
# Fill in the blank passwords and keys in .env.
bash run-archivero.sh
```

Use `sudo bash run-archivero.sh` if Docker requires sudo. The script contains one
command: `docker compose -f docker-compose.yml up --build`. Run it from `docker/`.
Compose builds the three applications and creates all five infrastructure containers:

- Backend API: http://localhost:50950/swagger
- Frontend SPA: http://localhost:50952
- Converter worker: no HTTP port
- SQL Server 2025: localhost:1435
- Service Bus emulator: localhost:5672; health endpoint localhost:5300/health
- SQL Edge: private SQL dependency for Service Bus
- Azurite blob emulator: localhost:10000
- Redis: localhost:6379

Each executable project has its own `Dockerfile`. Both build and final images use
`mcr.microsoft.com/dotnet/sdk:10.0`; the images contain Debug linux-x64 binaries and
PDBs in `/app`. Only the published frontend app.js switches the API URL to HTTP.
There are no certificates or application source changes required.

The apps use Linux host networking so their localhost blob URLs also work in the
browser. Infrastructure uses Compose's default network and publishes the ports
above. Stop other containers/processes using those ports before launching this stack.
This setup is for local Linux/WSL development, not remote hosting.

All settings are in `.env`, with documented placeholders in `.env.example`.
`SQL_ADMIN_USER` must be `sa` for a freshly created SQL Server image; the image
cannot rename its built-in administrator. `SQL_ADMIN_PASSWORD` and
`MSSQL_SA_PASSWORD` configure separate application and Service Bus SQL instances.
Use strong SQL passwords and an application password of at least 12 characters.
Generate `BLOB_ACCOUNT_KEY` with `openssl rand -base64 32` and `JWT_SIGNING_KEY`
with `openssl rand -base64 48`. Keep credentials in the ignored `.env` only.

The former Service Bus `.env` and Compose file are merged into these root Docker
files. `CONFIG_PATH` points to `AzureServiceBusEmulator/Config.json`, which provisions
the createwordfilequeue and deletewordfilequeue queues. Both applications use those
same names. There is no second Compose command or manual queue setup.

Compose waits for SQL, Redis and Azurite readiness. The backend applies its existing
migrations and seeds the configured administrator. Its health check also waits for
the Service Bus health endpoint before Compose starts the frontend and worker.

SQL, SQL Edge, blob and Redis data use named volumes. `docker compose down` preserves
them; changing initial SQL passwords in `.env` does not reset existing database
credentials. The Service Bus emulator is a development tool; do not rely on it to
retain queued work across restarts.

```bash
docker compose logs -f backend worker
docker compose down
```

To copy Debug binaries to the host when needed (not required to run the stack):

```bash
mkdir -p bin/backend bin/frontend bin/worker
docker compose cp backend:/app/. bin/backend/
docker compose cp frontend:/app/. bin/frontend/
docker compose cp worker:/app/. bin/worker/
```
