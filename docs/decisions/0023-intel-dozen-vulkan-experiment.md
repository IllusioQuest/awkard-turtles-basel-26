# 0023: Isolated Mesa Dozen Vulkan experiment for Intel WSL

Status: proposed; user approved isolated driver build experiment, 2026-11-21. T-39.
Build only Mesa 26.0.8 Dozen (microsoft-experimental Vulkan) in a multi-stage Docker image derived from the already pinned Selkies desktop. Official Mesa release tarball via HTTPS, exact source SHA256 pinned after download; no claim of separately verified signing key. Install under /opt/digitalheat-dzn and select its ICD only for the opt-in experiment. Existing system OpenGL/VAAPI libraries stay intact. Build flags and dependency manifest recorded; distro apt dependencies may change on a rebuild and are not a byte-reproducibility claim.

Keep existing session-a/b containers and their homes untouched during build and headless probe. A transient labelled probe container gets /dev/dxg and read-only WSL libraries/driver mounts, no Docker socket or privileged mode, no host driver changes, no network during probe. Bound timeout and report exit status so a driver hang is a failed experiment, not success. A host graphics driver is still shared: container isolation cannot eliminate GPU-reset risk; run lightweight enumeration and triangle/cube smoke before any CS2. No game download.

Success gates: actual Intel physical device enumerates (not llvmpipe); requested Vulkan features/API surface reviewed; a nontrivial GPU render smoke succeeds; only then offer opt-in desktop C with separate home/port, never replace user-active A/B. X11 presentation/stream and original CS2 must be independently checked. Device enumeration alone is not CS2 compatibility. If driver cannot expose needed features or render reliably, report and keep working fallback rather than silently installing arbitrary drivers or changing Windows.

Resource limit: inspect free disk before build; parallel compilation limited to two jobs. Do not remove unrelated Docker caches/images. No simulated fleet or GPU-capacity claim. Multi-site placement code remains separate, using existing shared heat helper; this experiment adds a replaceable runtime driver only.

Review: pending fresh limited-context review. Sources: https://archive.mesa3d.org/mesa-26.0.8.tar.xz, Mesa 26.0.8 meson.options and src/microsoft/vulkan/meson.build, https://docs.mesa3d.org/meson.html. Existing instructions docs/gaming-lab.md.
