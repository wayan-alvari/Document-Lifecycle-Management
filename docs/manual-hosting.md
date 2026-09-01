# Manual hosting guide

This guide prepares a conventional host without containers. It does not deploy, provision infrastructure, purchase hosting, or modify a remote system.

The public portfolio is a demo system only. Do not upload confidential, personal, regulated, or employer-owned documents.

## 1. Build workstation gate

Use .NET SDK 8.0.424, Node.js 20+, npm 10+, and the committed locks:

```powershell
dotnet tool restore
dotnet restore --locked-mode
npm ci
npm audit --omit=dev
dotnet format --verify-no-changes
dotnet build DocumentLifecycle.sln -c Release --no-restore
dotnet test DocumentLifecycle.sln -c Release --no-build --no-restore
dotnet list DocumentLifecycle.sln package --vulnerable --include-transitive
dotnet list DocumentLifecycle.sln package --deprecated
```

Review the recorded assessment in `docs/dependency-audit.md` before publishing.

## 2. Publish choices

Framework-dependent output uses the host's supported .NET 8 runtime:

```powershell
dotnet publish src/DocumentLifecycle.Web/DocumentLifecycle.Web.csproj `
  -c Release --no-restore `
  -o .publish/framework-dependent `
  /p:UseAppHost=false
```

Run it with:

```text
dotnet DocumentLifecycle.Web.dll
```

Self-contained Linux x64 output bundles the runtime and is larger:

```powershell
dotnet publish src/DocumentLifecycle.Web/DocumentLifecycle.Web.csproj `
  -c Release --no-restore `
  -r linux-x64 --self-contained true `
  -o .publish/linux-x64
```

Use the correct runtime identifier for the selected host. Rebuild and re-audit whenever the target runtime changes. Do not keep a public .NET 8 deployment online after 10 November 2026.

## 3. Host directories and permissions

Keep mutable data outside the versioned/publish directory. A Linux layout can be:

```text
/opt/document-lifecycle/current/       read-only application publish
/var/lib/document-lifecycle/uploads/   private writable revisions
/var/lib/document-lifecycle/keys/      private persistent Data Protection keys
/var/backups/document-lifecycle/       protected backups
```

Create the two writable directories for the dedicated service account, grant no web-server static-file mapping to them, and restrict Data Protection keys and environment files to that account. `FileStorage__RootPath` and `DataProtection__KeyPath` accept absolute paths.

Persist and back up Data Protection keys. Losing them invalidates authentication and workspace cookies; sharing multiple application instances requires the same protected key ring and application name.

## 4. MySQL and migrations

Use MySQL Community Server 8.0.46, `utf8mb4`, and `utf8mb4_0900_ai_ci`. Create a database-specific account using the placeholder SQL in `README.md`; keep its real password in the host secret/environment mechanism with restrictive permissions.

For a reviewed migration command from the published source revision:

```powershell
dotnet ef database update `
  --project src/DocumentLifecycle.Infrastructure `
  --startup-project src/DocumentLifecycle.Web `
  --context ApplicationDbContext `
  --configuration Release
```

Alternatively, build a reviewed migration bundle for the chosen runtime:

```powershell
dotnet ef migrations bundle `
  --project src/DocumentLifecycle.Infrastructure `
  --startup-project src/DocumentLifecycle.Web `
  --context ApplicationDbContext `
  --configuration Release `
  --self-contained -r linux-x64 `
  --output .publish/migrations/linux-x64/efbundle
```

Back up the database before applying a new migration. Run the migration or bundle as an explicit release step; the Production web process does not apply migrations automatically.

## 5. Required runtime configuration

Provide these as protected environment variables or the host's secret store:

```text
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5080
Database__Provider=MySql
ConnectionStrings__DefaultConnection=Server=127.0.0.1;Port=3306;Database=portfolio_document_lifecycle;User=<app-user>;Password=<host-secret>;CharSet=utf8mb4
FileStorage__RootPath=/var/lib/document-lifecycle/uploads
DataProtection__KeyPath=/var/lib/document-lifecycle/keys
DemoMode__Enabled=true
```

`DemoMode__Enabled=true` is appropriate only for the public fictional portfolio. It creates isolated browser workspaces, removes them after six inactive hours, and deletes their private uploads. Use `false` for any non-demo adaptation and supply a separate, reviewed identity/user lifecycle before use.

Production startup never seeds accounts automatically. After reviewing the environment and migration, initialize the public demo identities explicitly once; the operation is idempotent and exits without starting the server:

```text
dotnet DocumentLifecycle.Web.dll --initialize-demo
```

The explicit initializer requires demo mode, applies pending migrations, and seeds only the three published demo identities. Do not run it against a non-demo database.

## 6. HTTPS and reverse proxy

Bind Kestrel to loopback and terminate public HTTPS at a same-host reverse proxy. The application accepts `X-Forwarded-For` and `X-Forwarded-Proto` only from ASP.NET Core's default trusted loopback proxy/network set. A remote proxy requires a reviewed `KnownProxies`/`KnownNetworks` configuration before traffic is accepted.

For a same-host Nginx proxy, include:

```nginx
proxy_set_header Host $host;
proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
proxy_set_header X-Forwarded-Proto $scheme;
proxy_pass http://127.0.0.1:5080;
```

Redirect HTTP to HTTPS at the edge, use a valid certificate, and preserve the forwarded scheme so secure cookies and redirects work correctly. Do not remove the application's CSP, HSTS, antiforgery, rate-limit, or security-header middleware.

## 7. Process supervision and logs

Run under a dedicated unprivileged account and a service manager. A systemd unit can use this shape after paths and the environment-file location are reviewed:

```ini
[Unit]
Description=Document Lifecycle Management portfolio
After=network.target mysql.service

[Service]
Type=notify
User=document-lifecycle
Group=document-lifecycle
WorkingDirectory=/opt/document-lifecycle/current
ExecStart=/usr/bin/dotnet /opt/document-lifecycle/current/DocumentLifecycle.Web.dll
EnvironmentFile=/etc/document-lifecycle/environment
Restart=on-failure
RestartSec=5
NoNewPrivileges=true
PrivateTmp=true

[Install]
WantedBy=multi-user.target
```

Protect the environment file (`0600`), validate the unit, then use the host's normal `daemon-reload`, start/restart, and enable procedures. Structured application logs go to standard output and can be inspected with the service manager, for example:

```text
journalctl -u document-lifecycle --since today
```

Logs intentionally exclude request bodies, passwords, cookies, connection strings, and file contents. Keep access to logs restricted and define retention/rotation at the host.

## 8. Health and release verification

From the host itself:

```text
curl --fail --silent http://127.0.0.1:5080/health
```

The expected body is `Healthy`. This is liveness only; it intentionally exposes no database diagnostics. After proxying, verify HTTPS, security headers, sign-in for all three roles, the full recruiter workflow, private downloads, XLSX/PDF exports, browser isolation, desktop/mobile layout, and a clean browser console.

## 9. Backup and restore

Back up as one release set:

- a transactional MySQL dump (`mysqldump --single-transaction`) with restore tested separately;
- the private upload directory;
- the Data Protection key directory;
- the exact application revision, environment-variable names (not values), and migration version.

Store backups encrypted with restricted access and a tested retention schedule. Coordinate database and upload backups during a quiet/write-paused window so revision metadata and files remain consistent.

## 10. Rollback

Keep the previous publish directory until verification completes. To roll back application code without a schema change, stop traffic, repoint `current` to the prior publish, restart, and rerun health/workflow checks.

Do not blindly run an older application against an incompatible newer schema. If a release migration must be reversed, stop writes and restore the pre-release database, uploads, and key-ring backup as a coordinated set, then start the matching prior publish. Record the incident and preserve logs without secrets.
