# Security policy

## Supported scope

This repository is a portfolio demonstration. Only the latest `main` revision is maintained. It is not an enterprise records-management product and must not be used for confidential or regulated records.

## Reporting a vulnerability

Do not open a public issue containing exploit details, credentials, private records, or host information. Contact the repository owner privately through the contact method on the hosting profile and include:

- the affected revision and route or component;
- clear reproduction steps using synthetic data;
- the observed and expected behavior;
- the impact and any suggested mitigation.

Do not test against a public instance without permission, access another browser's workspace, or retain downloaded demo artifacts. The owner will acknowledge a report, assess it against supported dependencies, and publish a focused fix when appropriate.

## Operational expectations

- Keep the .NET 8 servicing runtime and locked dependencies current while this branch is supported.
- Do not operate an internet-facing .NET 8 instance after 10 November 2026; migrate to .NET 10 first.
- Store connection strings outside Git, persist and protect Data Protection keys, use HTTPS, and keep uploads outside the public web root.
- Run the documented NuGet and npm audits before publishing a revision.
