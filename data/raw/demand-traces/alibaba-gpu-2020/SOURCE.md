# Alibaba PAI GPU cluster trace 2020 (task table only)

- **What:** ML training and inference jobs on a production cluster with > 6,500 GPUs (~1,800
  machines), July-August 2020. Only `pai_task_table` (34 MB tar.gz, 1.26 M tasks) is used;
  columns (no header in file): `job_name, task_name, inst_num, status, start_time, end_time,
  plan_cpu, plan_mem, plan_gpu, gpu_type`. `plan_gpu` is percent of one GPU per instance.
- **URL:** https://github.com/alibaba/clusterdata/tree/master/cluster-trace-gpu-v2020; file
  https://aliopentrace.oss-cn-beijing.aliyuncs.com/v2020GPUTraces/pai_task_table.tar.gz, SHA-256
  `cd1d6dc3...c40e5` checked against the README. Retrieved 2026-11-21.
- **Licence:** none stated (no LICENSE file; README asks for citation), so we commit only our
  aggregates and cite the paper. TODO(licence) before any redistribution. Cite: Weng et al., "MLaaS in the Wild",
  NSDI 2022.
- **Not committed.** `python3 -I scripts/fetch_demand_traces.py --only alibaba` writes
  `research-alibaba2020-hour-of-week.csv` and `job-durations.csv`.
- **Filter:** status `Terminated` (successful), `plan_gpu > 0`, `end > start`: 732,700 tasks over
  9.9 weeks. GPUs per task = `inst_num x plan_gpu / 100`; GPU-hours = GPUs x duration.
- **Time zone:** per the README, seconds read as Unix time in UTC+8 give the original local clock
  time and weekday (dates are fake). That local clock is transferred to Zurich's local clock
  without shift (assumption, as for BurstGPT).
- **Caveats:** a hyperscaler's internal ML platform, not academic research compute; task launch
  time is used as arrival (job submission time is in the 60 MB job table, not fetched). Larger
  tables (instance, sensor, machine metrics; ~1.3 GB) were skipped. Microsoft Philly
  (https://github.com/msr-fiddle/philly-traces, 2017, 0.98 GB compressed) was skipped as a
  second training trace for time reasons.
