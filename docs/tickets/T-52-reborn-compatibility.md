# T-52: Assess ReBorn game replacement and multiplayer compatibility

Owner: Nikita / Codex. Status: assessment complete; implementation not started.
Documentation branch: `docs/T-52-reborn-assessment`.

User requested ReBorn 6.2 instead of Xonotic, with multiplayer and Minecraft unchanged.
Archive inspection establishes that it contains mod assets and requires the base CS:GO game/MIGI.
User declined the proposed isolated Linux Steam/CS:GO installation/test and additionally requires
public/community-server compatibility. Do not perform the declined setup.

Evidence and reproduction: `docs/jobs/reborn-compatibility-assessment.md`.
No runtime/code changes. Before implementation can resume, resolve the base game source and
specific community-server compatibility path with the user; approve any new runtime/tool design
under the normal decision/review workflow. Keep Minecraft unchanged and retain the existing
working shooter until a replacement is actually tested.
