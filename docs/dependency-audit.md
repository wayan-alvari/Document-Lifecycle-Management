# Dependency audit

Audit date: 1 September 2026

The repository uses exact central NuGet versions, committed NuGet lock files, and an exact npm lock file. Run the following checks from the repository root:

```powershell
.\.dotnet\dotnet.exe list DocumentLifecycle.sln package --vulnerable --include-transitive
.\.dotnet\dotnet.exe list DocumentLifecycle.sln package --deprecated
npm audit --omit=dev
```

## Assessment

- The npm production audit reports zero known vulnerabilities.
- The application runtime projects report no known vulnerable or deprecated NuGet packages.
- The first NuGet audit found vulnerable `System.Net.Http` 4.3.0 and `System.Text.RegularExpressions` 4.3.0 packages only in the legacy `NETStandard.Library` graph beneath xUnit 2.5.3. Updating to the final xUnit v2 release removed that legacy compatibility graph. The post-remediation audit reports no known vulnerabilities in any project.
- The test framework was updated within xUnit v2 to 2.9.3 and its Visual Studio runner to 2.8.2. NuGet marks the xUnit v2 metapackage as deprecated because feature development moved to v3. It is test-only and not present in published runtime output. A major v3 migration is intentionally tracked for a later test-infrastructure update rather than mixed into the .NET 8 application hardening milestone.

Reassess this record whenever a direct dependency changes, before publishing, and at least monthly while the portfolio is maintained.
