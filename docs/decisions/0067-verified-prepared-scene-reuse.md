# 0067 — Verified prepared-scene reuse

Status: proposed. T-77. User explicitly approved CPU1 verifying and reusing an
unchanged landscape rather than rebuilding it on every run. Default native
adapter remains opt-out; presentation profile enables reuse_prepared_scene.

CPU preparation remains a real heat-admitted owned job. On start the native
adapter may select a registered scene only if recipe (includes trusted scripts,
settings and expected version), actual Blender build/version and bounded scene
SHA-256 match. Python independently rehashes and copies the same bytes into the
new attempt, publishing prepared_scene_reused. No match means fresh trusted
procedural build. Corrupt or changed output is never accepted as successful.
Existing content-addressed assets are verified, not replaced while GPU workers
read them. Frame jobs retain hash and recipe checks. Exact same scene identity
lets existing warm GPU processes keep their loaded scene across render runs.
No arbitrary uploaded scenes, relaxed isolation or skipped CPU ownership.
Warm idle timeout remains120s; cache hits are recorded rather than guaranteed.

## Review
Go with changes. Applied read-once/hash/write of the exact selected scene bytes to close the copy race, and ignored malformed optional scene registration records with diagnostics while ownership journals remain fail-closed. Recipe/build/hash validation remains mandatory.
