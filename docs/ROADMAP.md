# Roadmap

## .NET 10 upgrade

.NET 8 is a deliberate transitional baseline for this portfolio and reaches
end of support on 10 November 2026. Before any public deployment remains online
after that date, upgrade the SDK, target framework, Microsoft package line,
Pomelo provider, EF tool manifest, migrations, lockfiles, CI/runtime image, and
hosting prerequisites to supported .NET 10 releases.

The upgrade is complete only after the normal SQLite suite, the optional MySQL
smoke test, dependency audits, Release publish, and the full recruiter workflow
all pass on .NET 10. Do not operate an internet-facing .NET 8 deployment beyond
its support date.

## Test infrastructure

Migrate the test projects from the security-maintained xUnit v2 line to xUnit
v3 in a dedicated change. Update the runner and test SDK together, review
parallelization and analyzer behavior, regenerate locks, and require the full
SQLite integration suite to remain green. The current test-only v2 deprecation
is assessed in `docs/dependency-audit.md`.

## Deliberately deferred product scope

- Add configurable object storage and malware scanning before accepting
  untrusted public uploads beyond this portfolio demo.
- Add retention/legal-hold policies, approval workflows, and richer ownership
  only if the product scope expands beyond the current document lifecycle tour.
- Add multi-instance cleanup coordination and shared caching before scaling a
  public demo beyond one application process.
