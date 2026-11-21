# T-39: Professional pitch video

- **Tier:** 2 Pitch  ·  **Area:** Presentation / video
- **Depends on:** T-21 storyboard, T-25 house animation, T-02 real-job recording
- **Suggested owner:** Team lead / Claude with the team  ·  **Owner:**  ·  **Status:** proposed (format agreed)

## Goal

Produce a clean, professional, offline-playable short clip within the live pitch. The user chose
the clip plus a real live demo on 2026-11-21; T-25's browser file is a tryable prototype and
visual/data source, not the final medium. The live job pause/resume remains part of the demo.

## Tasks

- [x] Agree the format: a short clip within the live pitch plus a real live demo.
- [ ] Agree the clip's exact placement, duration, aspect ratio and visual style with the T-21 owner.
- [ ] Use the T-21 storyboard and source-backed figures. Show simulated house heat and
      real job control as distinct layers; do not animate claims unsupported by the C# export.
- [ ] Label a manual "tank full" override if it triggers the live pause. The selected January and
      July year-export days do not naturally reach a zero server target; the clip must not imply
      that the exported heat trace caused a real job to stop on its own.
- [ ] Incorporate a real T-02 pause/resume recording (PR #15 has January and July LiveRun logs)
      and, if useful, footage from Nikita's hosting work without duplicating their implementation.
- [ ] Export an offline local video plus a fallback recording for the live demo; document source
      assets, commands and any audio/image rights. Use ElevenLabs only if the team agrees.
- [ ] Rehearse with the full pitch so the timing and message work without narration from an agent.

## Done when

The team lead approves the final cut, it plays offline on the presentation laptop, and every
number and real-process claim can be traced to the recorded run or repository source.

## Coordination

The T-25 owner verified that its importer reads both real T-02 transcription LiveRun logs in
PR #15: 24 hours each, forced pause at 12:00 and resumption at 13:00. Coordination questions
are on PRs #15 and #17. The user selected a short clip plus live demo; T-21's owner still needs
to fit it into the five-minute run of show.
