#!/bin/sh
set -eu
dockerd-entrypoint.sh dockerd --host=unix:///var/run/docker.sock --data-root=/var/lib/mesh/docker-overlay --storage-driver=overlay2 &
daemon=$!
trap 'kill "$daemon" 2>/dev/null || true' EXIT
until docker info >/dev/null 2>&1; do kill -0 "$daemon"; sleep 1; done
until docker image inspect "$MESH_JOB_IMAGE" >/dev/null 2>&1; do sleep 1; done
/app/DigitalHeat.WorkloadRun render-node /etc/mesh/node.json

