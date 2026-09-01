# Optional MySQL smoke test

The normal automated suite is credential-free and uses SQLite in memory. Run this manual smoke test only when a local MySQL 8.0.46 test credential is available. Never commit or paste the credential into an issue, log, screenshot, or tracked settings file.

## 1. Create an isolated test database

Run as a local MySQL administrator and replace the placeholder locally:

```sql
CREATE DATABASE portfolio_document_lifecycle_test
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_0900_ai_ci;

CREATE USER 'document_lifecycle_test'@'localhost'
  IDENTIFIED BY '<replace-with-a-local-test-password>';

GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, DROP, INDEX, REFERENCES
  ON portfolio_document_lifecycle_test.*
  TO 'document_lifecycle_test'@'localhost';

FLUSH PRIVILEGES;
```

## 2. Set process-only configuration

Use a fresh PowerShell window so the connection string is not written to the repository:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Database__Provider = "MySql"
$env:DemoMode__Enabled = "true"
$env:ConnectionStrings__DefaultConnection = "Server=127.0.0.1;Port=3306;Database=portfolio_document_lifecycle_test;User=document_lifecycle_test;Password=<replace-locally>;CharSet=utf8mb4"
$env:FileStorage__RootPath = (Join-Path $env:TEMP "document-lifecycle-mysql-smoke-uploads")
```

## 3. Apply migrations and run

```powershell
dotnet tool restore
dotnet restore --locked-mode
dotnet ef database update `
  --project src/DocumentLifecycle.Infrastructure `
  --startup-project src/DocumentLifecycle.Web `
  --context ApplicationDbContext

dotnet run --project src/DocumentLifecycle.Web --launch-profile http --no-restore
```

Confirm `http://localhost:5142/health` returns `Healthy` without database details.

## 4. Exercise one complete workflow

- Sign in as `manager@documents.demo` with `PortfolioDemo123!`.
- Confirm the seeded dashboard and 12 fictional documents appear.
- Create a uniquely titled draft and upload a small synthetic PDF revision.
- Activate it, upload revision 2 with a change note, archive it with a reason, and restore it.
- Confirm the document timeline contains each action and the protected download succeeds.
- Export a filtered XLSX register and the PDF metadata/history summary.
- Sign in from another private browser profile and confirm the new record is not visible.
- Sign in as Viewer and confirm a draft cannot be viewed or changed.

Stop the application and verify no credential or generated upload was added to Git:

```powershell
git status --short
```

Database/user removal is intentionally left as an explicit administrator decision. If cleanup is wanted, back up anything needed and drop only `portfolio_document_lifecycle_test` and `document_lifecycle_test@localhost` after confirming their exact names.
