# Public workload and price evidence for live jobs

User-supplied sources reviewed 2026-11-21, alongside T-48 deadlines and T-49/T-50 performance work.
No large dataset was downloaded by T-48/T-50 and no external result was silently substituted for a local
measurement or the existing scored parameters. Source fields distinguish measurement, trace,
market snapshot, derived estimate and operator policy. Proposed deadlines are policies, not trace facts.

## MLPerf Inference power

[MLCommons datacenter results](https://mlcommons.org/benchmarks/inference-datacenter/) report
whole-system wall-AC power for the accompanying benchmark. A reusable profile must identify the
result/release, hardware, model, precision, benchmark scenario, performance, power and access date.
Only paired results from that run may be combined for energy/work. Do not transfer a GPU result to
an arbitrary CPU/model or treat device TDP as measured consumption. Electrical energy is not
recovered water heat; any recovery model/evidence stays separate. No such result currently validates
Minecraft/laptop heat, so its failed-measurement admission gate remains unchanged.

## Alibaba GPU trace 2020

The [publisher schema](https://github.com/alibaba/clusterdata/tree/master/cluster-trace-gpu-v2020)
provides job submission/completion and task launch/completion timestamps plus requested resources.
Use successful `Terminated` records when estimating completed-work distributions; keep failures and
right-censored jobs separate. `job.start_time` means submission, while `task.start_time` means task
launch: job end minus submission includes queueing and is not pure compute duration. Multi-task/
multi-instance jobs must be joined by documented IDs rather than blindly summed. Timestamps are
desensitized and are not real calendar dates. Task `plan_cpu`/`plan_gpu` use percentage units;
`plan_mem` is labelled GB. The listed tables provide no explicit customer-deadline column.
Thus deadlines must remain named assumptions/policies, not inferred as actual customer SLAs.
Trace resource fractions also do not authorize fractional-GPU execution in our whole-GPU allocator.

A future importer should pin the publisher revision/archive hash, preserve the raw files, record
units, filtering/joining and deterministic sampling, and report sample size and censoring. It must
not claim Swiss production demand, local runtime or measured power from this trace alone.

## Provider price evidence

Official references: [AWS on-demand](https://aws.amazon.com/ec2/pricing/on-demand/),
[AWS Spot](https://aws.amazon.com/ec2/spot/pricing/),
[Google Compute pricing](https://cloud.google.com/products/compute/pricing),
[Azure Linux VMs](https://azure.microsoft.com/en-us/pricing/details/virtual-machines/linux/).
Record provider, exact instance/GPU count, region, OS, on-demand/spot model, unit, currency,
retrieval date/time and a retained quote/source. Keep storage, networking, tax, reservation and
interruption assumptions explicit. Spot is not equivalent to a noninterruptible on-demand offer.
Normalize only like hardware/service bundles; any FX conversion needs its own source/date.

These references are an acquisition plan, not numerical price observations: no new CHF/hour rate
was invented from a generic pricing page. Existing T-14 economics and pinned baseline rates stay
with their owners. A concrete region/instance quote should be shared there before changing results.

## Current implementation use

The approved live targets use our measured small-job runtimes as context and the human's explicit
300/600-second policy choice. The configuration names those sources and bounds inputs. Public
trace/MLPerf/provider data may produce separately labelled workload/hardware/price variants once
concrete compatible records are selected; they do not retroactively prove feasibility of the targets.

## Parallel data handoff

T-51 / PR #39 was merged while this work was being verified. Reuse its retained source records,
derived profiles and [compute-demand notes](compute-demand-traces.md), rather than creating another
trace importer. That task downloaded large traces separately and committed only documented
aggregates/source records where appropriate. Its task-start-based Alibaba series is an execution
activity proxy, not observed job-submission arrivals or customer deadlines. Keep that distinction
when using it for future live workload profiles. This handoff does not validate or alter another
agent's price assumptions, and does not change the 300/600-second operator policies.
