#!/usr/bin/env bash
set -euo pipefail

# Build and run SharePassword with Docker Compose (docker-compose.yml).
#
# Usage:
#   ./start-docker.sh start        Build the image and start the container
#   ./start-docker.sh stop         Stop the container
#   ./start-docker.sh clean        Stop and remove container + image (data volume kept)
#   ./start-docker.sh clean --all  Also remove the data volume (deletes the SQLite database)
#
# Required environment for 'start' (can also be placed in .env.docker next to this script):
#   AdminAuth__PasswordHash    Generate with: dotnet run --project ./sharepassword -- hash-admin-password --password '<password>'
#   Encryption__Passphrase     At least 15 characters (32+ recommended)
#
# .env.docker lines are KEY=VALUE and read literally, so '$' in password
# hashes needs no quoting or escaping.
#
# Optional environment:
#   SHAREPASSWORD_PORT         Host port to publish (default 8080)

CONTAINER_NAME="sharepassword"
VOLUME_NAME="sharepassword-data"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="$SCRIPT_DIR/.env.docker"
COMPOSE_FILE="$SCRIPT_DIR/docker-compose.yml"
export SHAREPASSWORD_PORT="${SHAREPASSWORD_PORT:-8080}"

compose() {
	docker compose -f "$COMPOSE_FILE" "$@"
}

usage() {
	sed -n '4,20p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
	exit 1
}

require_docker() {
	if ! command -v docker >/dev/null 2>&1; then
		echo "ERROR: docker is not installed or not on PATH." >&2
		exit 1
	fi

	if ! docker compose version >/dev/null 2>&1; then
		echo "ERROR: the Docker Compose plugin is not available (docker compose)." >&2
		exit 1
	fi
}

# Values are read literally (no shell expansion), because admin password
# hashes contain '$' (e.g. ARGON2ID$v=19$m=...). Lines are KEY=VALUE;
# optional surrounding single or double quotes are stripped.
load_env_file() {
	if [[ ! -f "$ENV_FILE" ]]; then
		return
	fi

	echo "Loading environment from $ENV_FILE"
	local line key value
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
		# Keys with dots (e.g. Logging__LogLevel__Microsoft.AspNetCore) cannot be
		# exported by bash; compose reads them from the env_file directly.
		[[ "$key" =~ ^[A-Za-z_][A-Za-z0-9_]*$ ]] || continue
		if [[ ( "$value" == \"*\" && "$value" == *\" ) || ( "$value" == \'*\' && "$value" == *\' ) ]]; then
			value="${value:1:-1}"
		fi
		export "$key=$value"
	done < "$ENV_FILE"
}

start() {
	load_env_file

	if [[ -z "${AdminAuth__PasswordHash:-}" ]]; then
		echo "ERROR: AdminAuth__PasswordHash is not set." >&2
		echo "Generate one with:" >&2
		echo "  dotnet run --project ./sharepassword -- hash-admin-password --password '<password>'" >&2
		echo "Then export it or add it to $ENV_FILE" >&2
		exit 1
	fi

	if [[ -z "${Encryption__Passphrase:-}" ]]; then
		echo "ERROR: Encryption__Passphrase is not set (minimum 15 characters)." >&2
		echo "Export it or add it to $ENV_FILE" >&2
		exit 1
	fi

	echo "Building and starting via Docker Compose..."
	compose up -d --build

	echo "SharePassword is starting: http://localhost:$SHAREPASSWORD_PORT"
	echo "Logs:   docker compose -f $COMPOSE_FILE logs -f"
	echo "Health: curl http://localhost:$SHAREPASSWORD_PORT/health"
}

stop() {
	if docker ps --format '{{.Names}}' | grep -qx "$CONTAINER_NAME"; then
		echo "Stopping via Docker Compose..."
		compose stop
		echo "Stopped."
	else
		echo "Container $CONTAINER_NAME is not running."
	fi
}

clean() {
	local remove_volume="${1:-}"

	if [[ "$remove_volume" == "--all" ]]; then
		echo "Removing container, image, and data volume (deletes the SQLite database)..."
		compose down --rmi all --volumes
	else
		echo "Removing container and image (data volume kept)..."
		compose down --rmi all
		echo "Data volume $VOLUME_NAME kept. Use './start-docker.sh clean --all' to remove it."
	fi

	echo "Clean complete."
}

require_docker

case "${1:-}" in
	start) start ;;
	stop) stop ;;
	clean) clean "${2:-}" ;;
	*) usage ;;
esac
