#!/bin/sh
set -eu
# Reproducible bounded lab fixture, not a claimed service runtime or measured heat profile.
export WORK=/work WORKERS=1 PASSES=2 LATTICE_SIZE=16 SWEEPS=100000 THERMALISATION=2000
export N_TEMPERATURES=1 N_SEEDS=1 T_MIN=2.269 T_MAX=2.269 BASE_SEED=20251115
python /opt/mesh/ising.py prepare
exec python /opt/mesh/ising.py run
