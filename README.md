# UserService

User profile and onboarding service for the DatingApp platform.

## What It Does

UserService owns user-facing account and profile data, including:
- Profile creation and updates
- Onboarding wizard state
- Preferences and account settings
- Verification and safety-related profile metadata

## Why It Is Interesting

This repo demonstrates:
- ASP.NET Core API design in a microservices environment
- EF Core schema evolution with migrations
- Clear separation between controllers, handlers, DTOs, and data models
- Integration patterns for identity-backed user domains

## Stack

- .NET 8
- ASP.NET Core Web API
- EF Core 8 + MySQL
- Keycloak OIDC integration patterns

## Project Layout

```text
UserService/
  Controllers/      # API endpoints
  Commands/         # Command handlers / write flows
  Data/             # DbContext and persistence concerns
  Models/           # Domain entities
  DTOs/             # API contracts
  Migrations/       # EF Core migrations
  Program.cs        # Startup and DI
```

## Build and Test

```bash
dotnet restore UserService.csproj
dotnet build UserService.csproj
dotnet test UserService.Tests/UserService.Tests.csproj
```

## Run Locally

```bash
dotnet run --project UserService.csproj
```

Service is typically run with the platform scripts from the platform root.

## Typical API Areas

- Profile CRUD
- Preferences
- Onboarding/wizard progress
- Verification-related profile endpoints

## Related Repositories

- `best-koder-org/mobile_dejtingapp`
- `best-koder-org/MatchmakingService`
- `best-koder-org/dejting-yarp`

## Status

Active development repository used in the current platform.
