# AutoPatch Framework - GitHub Copilot Instructions

Always reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.

## Project Overview

AutoPatch is a .NET 9.0 real-time synchronization framework for automatic object synchronization between server and client using SignalR and JsonPatch. The project consists of three core libraries (Core, Server, Client), demo applications, and comprehensive test suites.

## Bootstrap and Build Requirements

### .NET 9.0 SDK Installation (REQUIRED)
- **CRITICAL**: This project requires .NET 9.0 SDK. The system may have .NET 8.0 by default.
- Install .NET 9.0 using the official installer script:
  ```bash
  curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 9.0 --install-dir ~/.dotnet
  export PATH="$HOME/.dotnet:$PATH"
  dotnet --version  # Should show 9.0.x
  ```
- Always verify .NET 9.0 is available before building: `dotnet --version`

### Build Process
- Build all main projects in dependency order:
  ```bash
  export PATH="$HOME/.dotnet:$PATH"
  dotnet build src/Autopatch.Core/Autopatch.Core.csproj          # ~4 seconds
  dotnet build src/Autopatch.Server/Autopatch.Server.csproj      # ~2 seconds  
  dotnet build src/Autopatch.Client/Autopatch.Client.csproj      # ~6 seconds
  ```
- Build all test projects:
  ```bash
  dotnet build test/Autopatch.Core.Tests/Autopatch.Core.Tests.csproj      # ~6 seconds
  dotnet build test/Autopatch.Server.Tests/Autopatch.Server.Tests.csproj  # ~3 seconds
  dotnet build test/Autopatch.Client.Tests/Autopatch.Client.Tests.csproj  # ~2 seconds
  ```
- Build demo applications:
  ```bash
  dotnet build demo/Autopatch.Demo.Shared/Autopatch.Demo.Shared.csproj    # ~2 seconds
  dotnet build demo/Autopatch.Demo.Server/Autopatch.Demo.Server.csproj    # ~3 seconds
  dotnet build demo/Autopatch.Demo.Console/Autopatch.Demo.Console.csproj  # ~2 seconds
  ```
- Build WPF demo (requires EnableWindowsTargeting flag on Linux):
  ```bash
  dotnet build demo/Autopatch.Demo.WPF/Autopatch.Demo.WPF.csproj -p:EnableWindowsTargeting=true  # ~4 seconds
  ```

### Testing
- Run individual test projects (NEVER CANCEL - tests complete in seconds):
  ```bash
  dotnet test test/Autopatch.Core.Tests/Autopatch.Core.Tests.csproj --no-build      # ~1 second
  dotnet test test/Autopatch.Server.Tests/Autopatch.Server.Tests.csproj --no-build  # ~1 second
  dotnet test test/Autopatch.Client.Tests/Autopatch.Client.Tests.csproj --no-build  # ~1 second
  ```
- All tests should pass with summary: "total: 2, failed: 0, succeeded: 2, skipped: 0"

### Test Coverage
- Build test projects first, then run coverage script:
  ```bash
  # Build test projects first
  dotnet build test/Autopatch.Core.Tests/Autopatch.Core.Tests.csproj
  dotnet build test/Autopatch.Server.Tests/Autopatch.Server.Tests.csproj
  dotnet build test/Autopatch.Client.Tests/Autopatch.Client.Tests.csproj
  
  # Run coverage - NEVER CANCEL: Takes ~10 seconds. Set timeout to 30+ seconds:
  chmod +x test-coverage.sh && ./test-coverage.sh  # ~10 seconds
  ```
- Coverage report generated at `./coverage/report/index.html`
- Note: reportgenerator may have .NET 9 compatibility issues, but core coverage collection works

## Demo Applications and Validation

### Running the Demo Server
- Start the pizza delivery demo server:
  ```bash
  dotnet run --project demo/Autopatch.Demo.Server/Autopatch.Demo.Server.csproj --no-build
  ```
- Server listens on `http://localhost:5249`
- Shows live pizza orders and delivery drivers being generated
- Press Ctrl+C to stop

### Running the Demo Console Client
- Start the console client (requires server to be running):
  ```bash
  dotnet run --project demo/Autopatch.Demo.Console/Autopatch.Demo.Console.csproj --no-build
  ```
- Demonstrates authentication scenarios with different auth tokens
- Tests subscription validation (admin, store users, invalid users)
- Shows real-time collection synchronization
- Press any key to exit

### Manual Validation Scenarios
Always run through this complete validation scenario after making changes:

1. **Start Demo Server**: Launch the demo server and verify it shows:
   - "🍕 Poller's Pizza Palace Demo Server starting..."
   - Driver registration messages
   - Order generation and kitchen processing
   - "Now listening on: http://localhost:5249"

2. **Test Console Client**: Run console client and verify:
   - Successful connection to AutoPatch server
   - Admin accessing store1: ✅ Success
   - Store1 user accessing store1: ✅ Success  
   - Store1 user accessing store2: ❌ Rejected
   - Invalid user accessing store1: ❌ Rejected
   - No auth string provided: ❌ Rejected
   - DeliveryDriver subscription: ✅ Success

3. **Stop both applications cleanly**

## Code Quality and CI Integration

### Formatting and Linting
- Run code formatting on individual projects (solution-level formatting has issues with .slnx files):
  ```bash
  dotnet format src/Autopatch.Core/Autopatch.Core.csproj
  dotnet format src/Autopatch.Server/Autopatch.Server.csproj  
  dotnet format src/Autopatch.Client/Autopatch.Client.csproj
  # Format test and demo projects as needed
  ```
- Always run formatting before committing changes
- The project has comprehensive .editorconfig with C# style rules
- Use `--verify-no-changes` flag to check formatting without modifying files

### CI Pipeline Validation
- The project uses GitHub Actions (.github/workflows/nuget.yml)
- CI builds with .NET 9.0 and runs all tests with coverage
- To ensure CI compatibility, always run:
  ```bash
  # Format individual projects
  dotnet format src/Autopatch.Core/Autopatch.Core.csproj
  dotnet format src/Autopatch.Server/Autopatch.Server.csproj
  dotnet format src/Autopatch.Client/Autopatch.Client.csproj
  # Build and test
  dotnet build -c Debug  # CI uses Debug for coverage
  dotnet test --no-build
  ```

## Project Structure and Navigation

### Core Libraries
- `src/Autopatch.Core/` - Shared models and core functionality
- `src/Autopatch.Server/` - Server-side SignalR hub and services  
- `src/Autopatch.Client/` - Client-side connection and collection management

### Demo Applications
- `demo/Autopatch.Demo.Shared/` - Shared models for demos (PizzaOrder, DeliveryDriver)
- `demo/Autopatch.Demo.Server/` - ASP.NET Core server with live data simulation
- `demo/Autopatch.Demo.Console/` - Console client demonstrating authentication
- `demo/Autopatch.Demo.WPF/` - WPF client (builds on Linux with EnableWindowsTargeting=true)

### Test Projects
- `test/Autopatch.Core.Tests/` - Unit tests for core functionality
- `test/Autopatch.Server.Tests/` - Server-side tests
- `test/Autopatch.Client.Tests/` - Client-side tests

### Key Configuration Files
- `Directory.Build.props` - Common build properties and package versions
- `AutoPatch.slnx` - Solution file (requires .NET 9.0 SDK)
- `.editorconfig` - Code style configuration
- `test-coverage.sh` - Coverage testing script

## Common Development Tasks

### Adding New Features
1. Build and test existing code to establish baseline
2. Make changes to appropriate library (Core/Server/Client)
3. Update corresponding test project
4. Run tests to verify functionality
5. Test with demo applications for integration validation
6. Run `dotnet format` before committing

### Debugging Issues
1. Use demo server/client to reproduce issues
2. Check server logs for SignalR connection issues
3. Verify authentication scenarios with console client
4. Use `dotnet build` verbose output for build issues

### Performance Testing
- Demo server generates continuous updates for performance testing
- Monitor BulkFlushQueue messages in server logs
- Client connection and subscription validation built into console demo

## Important Notes

- **NEVER CANCEL** long-running operations - builds complete in seconds, tests in ~10 seconds
- Always use `export PATH="$HOME/.dotnet:$PATH"` to ensure .NET 9.0 is used
- WPF demo requires `-p:EnableWindowsTargeting=true` on non-Windows systems
- Demo server must be running before starting demo clients
- All timeouts should be 30+ seconds to account for .NET 9.0 SDK download/setup time
- The project builds successfully and all tests pass - if they don't, check .NET SDK version first