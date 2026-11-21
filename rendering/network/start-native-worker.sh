#!/bin/sh
# Run on a GPU server with a working native Blender/driver context and private shared assets.
set -eu
cd "$(dirname "$0")"
: "${MESH_TOKEN:?Set the private lab token without committing it}"
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://127.0.0.1:18911}"
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
# For private multi-host use, the operator sets ASPNETCORE_URLS to a reachable private interface.
# No display/driver setup, firewall change, Docker socket or public exposure is automated.
exec ./app/DigitalHeat.WorkloadRun render-node "${1:?Provide the edited GPU server config path}"
