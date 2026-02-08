# Developer Setup Guide

## Prerequisites
- .NET 8.0 SDK
- Visual Studio 2022 (with WPF workload)
- Wix Toolset v4 (for installer builds)

## Local Development
1. Clone the repository.
2. Restore NuGet packages: `dotnet restore`
3. Build the solution: `dotnet build`
4. Run the application: `dotnet run --project WhisperKey.csproj`

## Running Tests
- All Unit Tests: `dotnet test Tests/WhisperKey.Tests.csproj`
- Specific Category: `dotnet test --filter TestCategory=Security`

## Architecture Overview
The application is structured into the following layers:
- **src/Models**: Domain entities and DTOs.
- **src/Services**: Business logic and service implementations.
- **src/Bootstrap**: Application startup and DI configuration.
- **src/UI**: WPF Windows and Controls.
- **src/Infrastructure**: Low-level system integrations (Audio, Smoke Testing).

## Key Components
- **WhisperService**: Handles audio transcription via OpenAI or Local inference.
- **AudioCaptureService**: Manages system audio recording.
- **TextInjectionService**: Injects transcribed text into active windows.
- **AuditLoggingService**: Maintains compliant audit trails.
