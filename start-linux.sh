#!/usr/bin/env bash
set -euo pipefail

# Run Sekura locally with the .NET SDK, configured from a .env file.
#
# Usage:
#   ./start-linux.sh [env-file] [urls] [configuration] [project-path]
#
#   env-file       .env file to load (default: .env.dev; copy from .env.template)
#   urls           override listen URLs (default: from env file / Kestrel)
#   configuration  Debug or Release (default: Debug)
#   project-path   project to run (default: ./sekura/sekura.csproj)
#
# Env file lines are KEY=VALUE and read literally (no shell expansion), so
# '$' in password hashes needs no quoting or escaping. Keys with dots
# (e.g. Logging__LogLevel__Microsoft.AspNetCore) cannot be exported by bash
# and are skipped; app defaults apply for those.

ENV_FILE="${1:-.env.dev}"
URLS="${2:-}"
CONFIGURATION="${3:-Debug}"
PROJECT_PATH="${4:-./sekura/sekura.csproj}"

if [[ ! -f "$ENV_FILE" ]]; then
	echo "ERROR: env file '$ENV_FILE' not found." >&2
	echo "Create it from the template first: cp .env.template $ENV_FILE" >&2
	exit 1
fi

echo "Loading environment from $ENV_FILE"
while IFS= read -r line || [[ -n "$line" ]]; do
	[[ "$line" =~ ^[[:space:]]*(#|$) ]] && continue
	if [[ "$line" != *"="* ]]; then
		echo "WARNING: ignoring malformed line in $ENV_FILE (expected KEY=VALUE)" >&2
		continue
	fi
	key="${line%%=*}"
	value="${line#*=}"
	key="${key#"${key%%[![:space:]]*}"}"
	key="${key%"${key##*[![:space:]]}"}"
	[[ "$key" =~ ^[A-Za-z_][A-Za-z0-9_]*$ ]] || continue
	if [[ ( "$value" == \"*\" && "$value" == *\" ) || ( "$value" == \'*\' && "$value" == *\' ) ]]; then
		value="${value:1:-1}"
	fi
	export "$key=$value"
done < "$ENV_FILE"

export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
echo "ASPNETCORE_ENVIRONMENT=$ASPNETCORE_ENVIRONMENT"

echo "Restoring dependencies..."
dotnet restore

echo "Building project (${CONFIGURATION})..."
dotnet build "$PROJECT_PATH" -c "$CONFIGURATION"

if [[ -z "$URLS" ]]; then
	echo "Starting sekura using URLs from $ENV_FILE (ASPNETCORE_URLS=${ASPNETCORE_URLS:-<kestrel default>})"
	echo "Pass arg2 to override (example: ./start-linux.sh .env.dev https://localhost:7099)"
	echo "Press Ctrl+C to stop."
	dotnet run --project "$PROJECT_PATH" -c "$CONFIGURATION" --no-launch-profile
	exit $?
fi

echo "Starting sekura on $URLS"
echo "Press Ctrl+C to stop."

dotnet run --project "$PROJECT_PATH" -c "$CONFIGURATION" --no-launch-profile --urls "$URLS"
