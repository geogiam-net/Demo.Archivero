# Endpoint integration tests

Run from the repository root:

```powershell
dotnet test src/Demo.Archivero.UnitTests/Demo.Archivero.UnitTests.csproj --filter TestCategory=Integration
```

Each test starts a fresh in-memory ASP.NET Core TestServer and sends HTTP requests
through the real AuthEndpoints/FileEndpoints mappings, JWT authentication registration,
authorization middleware, JSON binding and result serialization. Auth and file services
are local test doubles; the Redis interface uses a strict mock with isolated cache state.
No Docker containers, network services, user secrets or development credentials are needed.

Coverage includes login responses and errors, JWT rejection, current-user claims,
anonymous access, file request binding, authenticated usernames, response contracts,
error mapping, positive/negative caching and invalidation after successful mutations.

These are endpoint integration tests, not full Program.cs startup or infrastructure
tests. They do not run SQL migrations, verify passwords against SQL Server, issue tokens
through the real AuthService, or verify live Redis, Service Bus and blob operations.
