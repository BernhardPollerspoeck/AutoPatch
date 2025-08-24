#!/bin/bash

# Install required tools
dotnet tool install --global coverlet.console
dotnet tool install --global dotnet-reportgenerator-globaltool

# Run tests with coverage for each project
echo "Running Core Tests with coverage..."
coverlet ./test/Autopatch.Core.Tests/bin/Debug/net9.0/Autopatch.Core.Tests.dll \
  --target "dotnet" \
  --targetargs "test ./test/Autopatch.Core.Tests/Autopatch.Core.Tests.csproj --no-build" \
  --format "cobertura" \
  --output "./coverage/Autopatch.Core.Tests.coverage.cobertura.xml"

echo "Running Server Tests with coverage..."
coverlet ./test/Autopatch.Server.Tests/bin/Debug/net9.0/Autopatch.Server.Tests.dll \
  --target "dotnet" \
  --targetargs "test ./test/Autopatch.Server.Tests/Autopatch.Server.Tests.csproj --no-build" \
  --format "cobertura" \
  --output "./coverage/Autopatch.Server.Tests.coverage.cobertura.xml"

echo "Running Client Tests with coverage..."
coverlet ./test/Autopatch.Client.Tests/bin/Debug/net9.0/Autopatch.Client.Tests.dll \
  --target "dotnet" \
  --targetargs "test ./test/Autopatch.Client.Tests/Autopatch.Client.Tests.csproj --no-build" \
  --format "cobertura" \
  --output "./coverage/Autopatch.Client.Tests.coverage.cobertura.xml"

# Generate report
echo "Generating coverage report..."
reportgenerator \
  -reports:"./coverage/*.xml" \
  -targetdir:"./coverage/report" \
  -reporttypes:"Html;Cobertura;Badges"

echo "Coverage report generated at ./coverage/report/index.html"
