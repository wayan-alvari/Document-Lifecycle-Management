# Third-Party Notices

Document Lifecycle Management is original software distributed under the MIT
License. The following public dependencies are approved by the project
specification. Versions are exact and correspond to the committed .NET SDK
declaration, central NuGet file, NuGet lockfiles, .NET tool manifest, and npm
lockfile.

| Component | Version | License | Project | Purpose |
|---|---:|---|---|---|
| .NET SDK | 8.0.424 | MIT | <https://github.com/dotnet/sdk> | Build toolchain |
| .NET / ASP.NET Core shared framework | 8.0.30 | MIT | <https://github.com/dotnet/aspnetcore> | Web runtime and Identity |
| Microsoft.EntityFrameworkCore; Microsoft.EntityFrameworkCore.Design | 8.0.30 | MIT | <https://github.com/dotnet/efcore> | Object-relational mapping, migrations, and design-time tooling |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 8.0.30 | MIT | <https://github.com/dotnet/aspnetcore> | Demo account and role persistence |
| Microsoft.AspNetCore.Mvc.Testing | 8.0.30 | MIT | <https://github.com/dotnet/aspnetcore> | In-process integration testing |
| Microsoft.EntityFrameworkCore.Sqlite | 8.0.30 | MIT | <https://github.com/dotnet/efcore> | Credential-free relational automated tests |
| Pomelo.EntityFrameworkCore.MySql | 8.0.3 | MIT | <https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql> | MySQL EF Core provider |
| admin-lte | 4.1.0 | MIT | <https://github.com/ColorlibHQ/AdminLTE> | Responsive administration interface components |
| bootstrap | 5.3.8 | MIT | <https://github.com/twbs/bootstrap> | Responsive layout and accessible interface components |
| @popperjs/core | 2.11.8 | MIT | <https://github.com/floating-ui/floating-ui> | Bootstrap positioning support |
| ClosedXML | 0.105.1 | MIT | <https://github.com/ClosedXML/ClosedXML> | XLSX report generation |
| PDFsharp-MigraDoc | 6.2.4 | MIT | <https://github.com/empira/PDFsharp> | PDF summary generation |
| dotnet-ef | 8.0.30 | MIT | <https://github.com/dotnet/efcore> | Repository-local migration tool |
| Microsoft.NET.Test.Sdk | 17.8.0 | MIT | <https://github.com/microsoft/vstest> | .NET test discovery and execution |
| xUnit.net | 2.9.3 | Apache-2.0 | <https://github.com/xunit/xunit> | Unit and integration test framework |
| xunit.runner.visualstudio | 2.8.2 | Apache-2.0 | <https://github.com/xunit/visualstudio.xunit> | Test runner adapter |
| coverlet.collector | 6.0.0 | MIT | <https://github.com/coverlet-coverage/coverlet> | Cross-platform code coverage collection |

## Resolved transitive components

The following table completes the resolved NuGet runtime, build, and test graph.
Names grouped in one row have the same pinned version, license, upstream, and
purpose. Direct components already named above can also appear in a lockfile but
are not repeated here.

| Component | Version | License | Project | Purpose |
|---|---:|---|---|---|
| ClosedXML.Parser | 2.0.0 | MIT | <https://github.com/ClosedXML/ClosedXML.Parser> | Spreadsheet formula parsing |
| DocumentFormat.OpenXml; DocumentFormat.OpenXml.Framework | 3.1.1 | MIT | <https://github.com/dotnet/Open-XML-SDK> | Open XML spreadsheet packaging |
| ExcelNumberFormat | 1.1.0 | MIT | <https://github.com/andersnm/ExcelNumberFormat> | Excel number-format rendering |
| Humanizer.Core | 2.14.1 | MIT | <https://github.com/Humanizr/Humanizer> | EF design-time text helpers |
| Microsoft.AspNetCore.Cryptography.Internal; Microsoft.AspNetCore.Cryptography.KeyDerivation | 8.0.30 | MIT | <https://github.com/dotnet/aspnetcore> | Identity password cryptography |
| Microsoft.AspNetCore.TestHost | 8.0.30 | MIT | <https://github.com/dotnet/aspnetcore> | In-process ASP.NET Core host |
| Microsoft.Bcl.AsyncInterfaces | 6.0.0 | MIT | <https://github.com/dotnet/runtime> | Async interface compatibility |
| Microsoft.CodeAnalysis.Analyzers | 3.3.3 | MIT | <https://github.com/dotnet/roslyn-analyzers> | Roslyn analyzer rules |
| Microsoft.CodeAnalysis.Common; Microsoft.CodeAnalysis.CSharp; Microsoft.CodeAnalysis.CSharp.Workspaces; Microsoft.CodeAnalysis.Workspaces.Common | 4.5.0 | MIT | <https://github.com/dotnet/roslyn> | EF design-time C# code generation |
| Microsoft.CodeCoverage | 17.8.0 | MIT | <https://github.com/microsoft/vstest> | Test coverage instrumentation |
| Microsoft.Data.Sqlite.Core | 8.0.30 | MIT | <https://github.com/dotnet/efcore> | SQLite ADO.NET provider |
| Microsoft.EntityFrameworkCore.Abstractions; Microsoft.EntityFrameworkCore.Analyzers; Microsoft.EntityFrameworkCore.Relational; Microsoft.EntityFrameworkCore.Sqlite.Core | 8.0.30 | MIT | <https://github.com/dotnet/efcore> | EF Core runtime, analyzers, relational, and SQLite internals |
| Microsoft.Extensions.Caching.Abstractions | 8.0.0 | MIT | <https://github.com/dotnet/runtime> | Cache contracts |
| Microsoft.Extensions.Caching.Memory | 8.0.1 | MIT | <https://github.com/dotnet/runtime> | In-memory caching |
| Microsoft.Extensions.Configuration; Microsoft.Extensions.Configuration.Abstractions; Microsoft.Extensions.Configuration.CommandLine; Microsoft.Extensions.Configuration.EnvironmentVariables | 8.0.0 | MIT | <https://github.com/dotnet/runtime> | Configuration providers and contracts |
| Microsoft.Extensions.Configuration.Binder | 8.0.2 | MIT | <https://github.com/dotnet/runtime> | Configuration object binding |
| Microsoft.Extensions.Configuration.FileExtensions; Microsoft.Extensions.Configuration.Json; Microsoft.Extensions.Configuration.UserSecrets | 8.0.1 | MIT | <https://github.com/dotnet/runtime> | File, JSON, and user-secret configuration |
| Microsoft.Extensions.DependencyInjection | 8.0.1 | MIT | <https://github.com/dotnet/runtime> | Dependency injection runtime |
| Microsoft.Extensions.DependencyInjection.Abstractions | 8.0.2 | MIT | <https://github.com/dotnet/runtime> | Dependency injection contracts |
| Microsoft.Extensions.DependencyModel | 8.0.2 | MIT | <https://github.com/dotnet/runtime> | Dependency metadata inspection |
| Microsoft.Extensions.Diagnostics; Microsoft.Extensions.Diagnostics.Abstractions | 8.0.1 | MIT | <https://github.com/dotnet/runtime> | Diagnostics services and contracts |
| Microsoft.Extensions.FileProviders.Abstractions; Microsoft.Extensions.FileProviders.Physical; Microsoft.Extensions.FileSystemGlobbing | 8.0.0 | MIT | <https://github.com/dotnet/runtime> | Hosting file access and globbing |
| Microsoft.Extensions.Hosting; Microsoft.Extensions.Hosting.Abstractions | 8.0.1 | MIT | <https://github.com/dotnet/runtime> | Generic host services |
| Microsoft.Extensions.Identity.Core; Microsoft.Extensions.Identity.Stores | 8.0.30 | MIT | <https://github.com/dotnet/aspnetcore> | Identity managers and persistence contracts |
| Microsoft.Extensions.Logging | 8.0.1 | MIT | <https://github.com/dotnet/runtime> | Logging runtime |
| Microsoft.Extensions.Logging.Abstractions | 8.0.3 | MIT | <https://github.com/dotnet/runtime> | Logging contracts |
| Microsoft.Extensions.Logging.Configuration; Microsoft.Extensions.Logging.Console; Microsoft.Extensions.Logging.Debug; Microsoft.Extensions.Logging.EventLog; Microsoft.Extensions.Logging.EventSource | 8.0.1 | MIT | <https://github.com/dotnet/runtime> | Logging configuration and providers |
| Microsoft.Extensions.Options | 8.0.2 | MIT | <https://github.com/dotnet/runtime> | Typed options runtime |
| Microsoft.Extensions.Options.ConfigurationExtensions; Microsoft.Extensions.Primitives | 8.0.0 | MIT | <https://github.com/dotnet/runtime> | Options binding and framework primitives |
| Microsoft.TestPlatform.ObjectModel; Microsoft.TestPlatform.TestHost | 17.8.0 | MIT | <https://github.com/microsoft/vstest> | Test protocol and execution host |
| Mono.TextTemplating | 2.2.1 | MIT | <https://github.com/mono/t4> | EF design-time templates |
| MySqlConnector | 2.3.5 | MIT | <https://github.com/mysql-net/MySqlConnector> | MySQL network protocol provider |
| Newtonsoft.Json | 13.0.1 | MIT | <https://github.com/JamesNK/Newtonsoft.Json> | Test platform JSON serialization |
| NuGet.Frameworks | 6.5.0 | Apache-2.0 | <https://github.com/NuGet/NuGet.Client> | Test platform target-framework parsing |
| RBush.Signed | 4.0.0 | MIT | <https://github.com/viceroypenguin/RBush> | PDF layout spatial index |
| SixLabors.Fonts | 1.0.0 | Apache-2.0 | <https://github.com/SixLabors/Fonts> | PDF font metrics |
| SQLitePCLRaw.bundle_e_sqlite3; SQLitePCLRaw.core; SQLitePCLRaw.lib.e_sqlite3; SQLitePCLRaw.provider.e_sqlite3 | 2.1.12 | Apache-2.0 | <https://github.com/ericsink/SQLitePCL.raw> | Native SQLite bundle and bindings |
| System.CodeDom | 4.4.0 | MIT | <https://github.com/dotnet/runtime> | EF design-time code model |
| System.Collections.Immutable; System.Runtime.CompilerServices.Unsafe; System.Text.Encoding.CodePages; System.Threading.Channels | 6.0.0 | MIT | <https://github.com/dotnet/runtime> | Compiler and design-time runtime support |
| System.Composition; System.Composition.AttributedModel; System.Composition.Convention; System.Composition.Hosting; System.Composition.Runtime; System.Composition.TypedParts | 6.0.0 | MIT | <https://github.com/dotnet/runtime> | Roslyn composition services |
| System.Diagnostics.EventLog; System.IO.Packaging; System.Security.Cryptography.Pkcs | 8.0.1 | MIT | <https://github.com/dotnet/runtime> | Event logging, Open XML packaging, and certificate support |
| System.IO.Pipelines | 6.0.3 | MIT | <https://github.com/dotnet/runtime> | MySQL buffered I/O |
| System.IO.Pipelines | 8.0.0 | MIT | <https://github.com/dotnet/runtime> | Test-host buffered I/O |
| System.Memory | 4.5.3 | MIT | <https://github.com/dotnet/runtime> | Memory APIs for Open XML |
| System.Reflection.Metadata | 1.6.0 | MIT | <https://github.com/dotnet/runtime> | Test platform assembly metadata |
| System.Reflection.Metadata | 6.0.1 | MIT | <https://github.com/dotnet/runtime> | Roslyn assembly metadata |
| xunit.abstractions | 2.0.3 | Apache-2.0 | <https://github.com/xunit/xunit> | xUnit extensibility contracts |
| xunit.analyzers | 1.18.0 | Apache-2.0 | <https://github.com/xunit/xunit.analyzers> | xUnit analyzer rules |
| xunit.assert; xunit.core; xunit.extensibility.core; xunit.extensibility.execution | 2.9.3 | Apache-2.0 | <https://github.com/xunit/xunit> | xUnit assertions and execution engine |

The MIT License in `LICENSE` covers only original repository source. Each
third-party component retains its own copyright and license. The committed
lockfiles are authoritative for package content hashes and dependency paths.
