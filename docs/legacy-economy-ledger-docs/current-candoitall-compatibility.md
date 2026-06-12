# Current CanDoItAll compatibility notes

This implementation was revised after inspecting the attached `CanDoItAll-agents-integration` source tree.

## Observed conventions

- SDK is pinned through `global.json` to `10.0.200` with `latestPatch` roll-forward.
- Projects target `net10.0`.
- Nullable reference types and implicit usings are enabled through `Directory.Build.props`.
- Package versions are declared directly in project files, not in central package management.
- EF Core is the primary persistence stack.
- PostgreSQL uses `Npgsql.EntityFrameworkCore.PostgreSQL`.
- BaseLib components are the UI building blocks.
- Codex bundles use normalized requirements, architecture, phase plan, subbundles, shared prompts, reviews, and traceability.

## Package alignment

| Package | Version |
|---|---:|
| Microsoft.EntityFrameworkCore | 10.0.4 |
| Microsoft.EntityFrameworkCore.Relational | 10.0.4 |
| Microsoft.EntityFrameworkCore.Design | 10.0.4 |
| Microsoft.EntityFrameworkCore.InMemory | 10.0.4 |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.0 |
| Microsoft.Extensions.Hosting | 10.0.0 |
| Microsoft.Extensions.Configuration.Json | 10.0.0 |
| Microsoft.Extensions.Configuration.EnvironmentVariables | 10.0.0 |
| Microsoft.Extensions.Logging.Console | 10.0.0 |
| Microsoft.Extensions.Options.ConfigurationExtensions | 10.0.0 |
| Microsoft.Extensions.Options.DataAnnotations | 10.0.0 |
| Microsoft.AspNetCore.Components.Web | 10.0.4 |
| Microsoft.NET.Test.Sdk | 17.14.1 |
| xunit | 2.9.3 |
| xunit.runner.visualstudio | 3.1.4 |
| coverlet.collector | 6.0.4 |

## Integration rule

The standalone economy repository must not depend on the full CanDoItAll web app. Add an optional UI module only after the headless node and CLI are stable.
