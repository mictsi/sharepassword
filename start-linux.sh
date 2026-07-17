#!/usr/bin/env bash
set -euo pipefail

PROJECT_PATH="${1:-./sekura/sekura.csproj}"
URLS="${2:-}"
CONFIGURATION="${3:-Debug}"
ENVIRONMENT="${4:-Development}"

echo "Restoring dependencies..."
dotnet restore

echo "Building project (${CONFIGURATION})..."
dotnet build "$PROJECT_PATH" -c "$CONFIGURATION"

export ASPNETCORE_ENVIRONMENT="$ENVIRONMENT"
echo "ASPNETCORE_ENVIRONMENT=$ENVIRONMENT"

if [[ -z "$URLS" ]]; then
	echo "Starting sekura using URL/port from appsettings"
	echo "Pass arg2 to override URL (example: ./start-linux.sh ./sekura/sekura.csproj https://localhost:7099)"
	echo "Press Ctrl+C to stop."
	dotnet run --project "$PROJECT_PATH" -c "$CONFIGURATION" --no-launch-profile
	exit $?
fi

echo "Starting sekura on $URLS"
echo "Press Ctrl+C to stop."

dotnet run --project "$PROJECT_PATH" -c "$CONFIGURATION" --no-launch-profile --urls "$URLS"
