#!/bin/sh
set -eu
# Explicit dockerd command avoids the base entrypoint's automatic Docker TCP listeners.
dockerd-entrypoint.sh dockerd --host=unix:///var/run/docker.sock --data-root=/var/lib/mesh/docker --storage-driver=vfs &
daemon=$!
trap 'kill "$daemon" 2>/dev/null || true' EXIT
until docker info >/dev/null 2>&1; do kill -0 "$daemon"; sleep 1; done
# Operator loads the pinned finite workload locally before the actor may reconcile.
until docker image inspect "$MESH_JOB_IMAGE" >/dev/null 2>&1; do sleep 1; done
/app/DigitalHeat.WorkloadRun mesh-node /etc/mesh/node.json
