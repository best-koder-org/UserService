# UserService
.NET 8 user profile management service.
## Build & Test
```bash
dotnet restore UserService.csproj && dotnet build && dotnet test UserService.Tests/UserService.Tests.csproj
```
## Architecture
- Profile CRUD, preferences, onboarding state
- CQRS via MediatR, EF Core 8 with MySQL
- Keycloak OIDC authentication
## Rules
- All new code must have unit tests
