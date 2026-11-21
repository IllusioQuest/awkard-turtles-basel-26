#!/bin/sh
# Run on the Linux host, using its own engine; never inherit a remote Docker context.
set -eu
cd "$(dirname "$0")"
unset DOCKER_CONTEXT DOCKER_TLS_VERIFY DOCKER_CERT_PATH
export DOCKER_HOST=unix:///var/run/docker.sock
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://127.0.0.1:8080}"
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
command -v docker >/dev/null
docker info >/dev/null
if [ "${1:-}" = --probe ]; then
    exec ./app/DigitalHeat.WorkloadRun render-probe "${2:-node.json}"
fi
: "${MESH_TOKEN:?Set the shared private-lab token without committing it}"
exec ./app/DigitalHeat.WorkloadRun render-node "${1:-node.json}"
