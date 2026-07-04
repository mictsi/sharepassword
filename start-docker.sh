#!/usr/bin/env bash
set -euo pipefail

# Build and run SharePassword in Docker.
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

IMAGE_NAME="sharepassword"
CONTAINER_NAME="sharepassword"
VOLUME_NAME="sharepassword-data"
HOST_PORT="${SHAREPASSWORD_PORT:-8080}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="$SCRIPT_DIR/.env.docker"

usage() {
	sed -n '4,20p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
	exit 1
}

require_docker() {
	if ! command -v docker >/dev/null 2>&1; then
		echo "ERROR: docker is not installed or not on PATH." >&2
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

	echo "Building image $IMAGE_NAME..."
	docker build -t "$IMAGE_NAME" "$SCRIPT_DIR"

	if docker ps -a --format '{{.Names}}' | grep -qx "$CONTAINER_NAME"; then
		echo "Removing existing container $CONTAINER_NAME..."
		docker rm -f "$CONTAINER_NAME" >/dev/null
	fi

	echo "Starting container $CONTAINER_NAME on port $HOST_PORT..."
	docker run -d \
		--name "$CONTAINER_NAME" \
		-p "$HOST_PORT:8080" \
		-v "$VOLUME_NAME:/app/data" \
		-e "AdminAuth__PasswordHash=$AdminAuth__PasswordHash" \
		-e "Encryption__Passphrase=$Encryption__Passphrase" \
		${AdminAuth__Username:+-e "AdminAuth__Username=$AdminAuth__Username"} \
		"$IMAGE_NAME" >/dev/null

	echo "SharePassword is starting: http://localhost:$HOST_PORT"
	echo "Logs:   docker logs -f $CONTAINER_NAME"
	echo "Health: curl http://localhost:$HOST_PORT/health"
}

stop() {
	if docker ps --format '{{.Names}}' | grep -qx "$CONTAINER_NAME"; then
		echo "Stopping container $CONTAINER_NAME..."
		docker stop "$CONTAINER_NAME" >/dev/null
		echo "Stopped."
	else
		echo "Container $CONTAINER_NAME is not running."
	fi
}

clean() {
	local remove_volume="${1:-}"

	if docker ps -a --format '{{.Names}}' | grep -qx "$CONTAINER_NAME"; then
		echo "Removing container $CONTAINER_NAME..."
		docker rm -f "$CONTAINER_NAME" >/dev/null
	fi

	if docker image inspect "$IMAGE_NAME" >/dev/null 2>&1; then
		echo "Removing image $IMAGE_NAME..."
		docker rmi "$IMAGE_NAME" >/dev/null
	fi

	if [[ "$remove_volume" == "--all" ]]; then
		if docker volume inspect "$VOLUME_NAME" >/dev/null 2>&1; then
			echo "Removing data volume $VOLUME_NAME (deletes the SQLite database)..."
			docker volume rm "$VOLUME_NAME" >/dev/null
		fi
	else
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
