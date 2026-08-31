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
