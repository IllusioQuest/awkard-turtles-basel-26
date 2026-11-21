# T-61: Complete the shared network job path

- **Tier:** 2 Pitch
- **Owner:** open (coordinate Jona and Nikita)
- **Status:** open
- **Depends on:** T-60, T-54, T-43

## Goal

Use one `INetworkScheduler` decision path for the scored finite/mixed reference-house run and at least one concrete real batch job, with the same job arrivals, deadlines, work sizes and heat inputs. Preserve the safe selected-site admission boundary from T-60.

## Required mapping

- Map each finite job ID and its measured server-hours/deadline to a Network work cohort; map each B2 allocation back to real job IDs without pretending an indivisible task can split.
- Feed B2 site/load decisions into `WorkloadCoordinator.TryStartAtSite` and `SchedulerController.Apply`; record achieved load and terminal progress for each job. Reject stale or ineligible plans and replan.
- Cross-check the fixed reference-house hourly heat, dumped heat, utilisation, missed deadlines, income and break-even figures against the existing run. Explain any intentional change before replacing a scored figure.
- Rehearse a real batch pause/resume and a two-device handoff. Fractional targets need measured achieved-load feedback; zero/full pause behavior alone cannot prove fractional control.

## Boundary

Interactive persistent gaming workloads currently have a different ownership and service model. Decide their Network representation separately before claiming that *every* runtime uses B2. Do not claim T-61 complete merely from the unlimited one-house adapter or a synthetic integration test.
