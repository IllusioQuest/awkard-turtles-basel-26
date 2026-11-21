#!/usr/bin/env python3
"""Download public compute-demand traces and reduce them to small demand shapes (T-47).

Large raw files go to the git-ignored ``data/external/<source>/`` (one directory per
source); only the small aggregates in ``data/derived/demand/`` are committed.
Sources, licences and caveats: ``data/raw/demand-traces/<source>/SOURCE.md``.
Method and how to read the outputs: ``docs/research/compute-demand-traces.md``.

Usage (Python 3.9+, standard library only)::

    python3 -I scripts/fetch_demand_traces.py                 # download missing files, aggregate all
    python3 -I scripts/fetch_demand_traces.py --no-download   # aggregate what is in data/external/
    python3 -I scripts/fetch_demand_traces.py --only azure2024,alibaba

Plus the GPU rental price history (source ``gpuprices``), aggregated from the committed CC BY 4.0
snapshot ``data/raw/demand-traces/getdeploying-gpu-prices/weekly-2026-11-21.csv``.

Sizes: Azure 2024 ~1.8 GB (2 CSV), BurstGPT ~190 MB (2 CSV), Alibaba task table 34 MB (tar.gz).
Downloaded files are treated as untrusted data: they are only parsed as CSV numbers.

Time handling (all profiles are indexed by Europe/Zurich local clock, Monday 00:00 = hour 0):
* Azure 2024: timestamps are UTC; converted to Europe/Zurich (May 2024 = CEST, UTC+2).
  ``hour_utc`` is kept so the profile can be re-indexed under a different user-timezone guess.
* Alibaba 2020: per the trace README, seconds read as Unix time in UTC+8 give the original
  local clock time and weekday (dates are fake). We transfer that *local clock* to Zurich's
  local clock (assumption: users follow the same local working-day rhythm), no shift.
* BurstGPT: seconds from 0:00 of day 1 "calibrated to the local time zone" (zone not named;
  weekday of day 1 not stated). Hour-of-day is a local-clock transfer as for Alibaba; the
  day-of-week column is ``day_index_mod7`` with unknown weekday offset.
Every ``*_rel`` column is normalised to mean 1.0 over its rows (per series).
"""

from __future__ import annotations

import argparse
import csv
import datetime as dt
import hashlib
import math
import statistics
import tarfile
import urllib.request
from collections import defaultdict
from pathlib import Path
from zoneinfo import ZoneInfo

ROOT = Path(__file__).resolve().parent.parent
EXTERNAL = ROOT / "data" / "external"
OUT = ROOT / "data" / "derived" / "demand"
ZURICH = ZoneInfo("Europe/Zurich")
UTC = dt.timezone.utc
HOURS_PER_WEEK = 168
SECONDS_PER_HOUR = 3600
SECONDS_PER_DAY = 86400
SHANGHAI_UTC_OFFSET_S = 8 * SECONDS_PER_HOUR  # Alibaba README: read times as UTC+8
WEEKDAYS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"]

AZURE_BASE = "https://github.com/Azure/AzurePublicDataset/releases/download/dataset-llm-2024/"
SOURCES = {
    "azure2024": {
        "dir": "azure-llm-2024",
        "files": {
            "AzureLLMInferenceTrace_code_1week.csv": AZURE_BASE + "AzureLLMInferenceTrace_code_1week.csv",
            "AzureLLMInferenceTrace_conv_1week.csv": AZURE_BASE + "AzureLLMInferenceTrace_conv_1week.csv",
        },
    },
    "burstgpt": {
        "dir": "burstgpt",
        "files": {
            f"BurstGPT_without_fails_{i}.csv":
                f"https://github.com/HPMLL/BurstGPT/releases/download/v2.0/BurstGPT_without_fails_{i}.csv"
            for i in (1, 2)
        },
    },
    "gpuprices": {"dir": None, "files": {}},  # committed CC BY 4.0 snapshot, no download
    "alibaba": {
        "dir": "alibaba-gpu-2020",
        "files": {
            "pai_task_table.tar.gz":
                "https://aliopentrace.oss-cn-beijing.aliyuncs.com/v2020GPUTraces/pai_task_table.tar.gz",
        },
        # sha256 published in the trace README (cluster-trace-gpu-v2020/README.md)
        "sha256": {"pai_task_table.tar.gz": "cd1d6dc3215d2a8607ccf6b6dd952b5db776df86926c73259fea7c1499ac40e5"},
    },
}
# Column order of pai_task_table.csv (no header in file), from the trace README.
ALIBABA_TASK_COLUMNS = ["job_name", "task_name", "inst_num", "status", "start_time", "end_time",
                        "plan_cpu", "plan_mem", "plan_gpu", "gpu_type"]
# Duration bins (upper edges in hours) for job-duration distributions.
DURATION_BINS_H = [(1 / 60, "<1 min"), (10 / 60, "1-10 min"), (1, "10-60 min"), (4, "1-4 h"),
                   (12, "4-12 h"), (24, "12-24 h"), (72, "1-3 d"), (math.inf, ">3 d")]
QUANTILES = [0.1, 0.25, 0.5, 0.75, 0.9, 0.95, 0.99]


# ---------------------------------------------------------------- helpers
def download(source: str) -> None:
    spec = SOURCES[source]
    if spec["dir"] is None:
        return
    target_dir = EXTERNAL / spec["dir"]
    target_dir.mkdir(parents=True, exist_ok=True)
    for name, url in spec["files"].items():
        path = target_dir / name
        if path.exists() and path.stat().st_size > 0:
            continue
        print(f"downloading {url}")
        tmp = path.with_suffix(path.suffix + ".part")
        with urllib.request.urlopen(url, timeout=120) as resp, open(tmp, "wb") as fh:
            while chunk := resp.read(1 << 20):
                fh.write(chunk)
        tmp.rename(path)
    for name, expected in spec.get("sha256", {}).items():
        digest = hashlib.sha256((target_dir / name).read_bytes()).hexdigest()
        if digest != expected:
            raise SystemExit(f"checksum mismatch for {name}: {digest}")


def quantile(sorted_values: list[float], q: float) -> float:
    """Nearest-rank quantile of an already sorted list."""
    idx = min(len(sorted_values) - 1, max(0, math.ceil(q * len(sorted_values)) - 1))
    return sorted_values[idx]


def normalise(values: list[float]) -> list[float]:
    """Scale to mean 1.0 (empty or all-zero input returns zeros)."""
    mean = sum(values) / len(values) if values else 0.0
    return [v / mean if mean else 0.0 for v in values]


def peak_trough(values: list[float]) -> float:
    low = min(values)
    return max(values) / low if low > 0 else math.inf


def write_csv(name: str, header: list[str], rows: list[list]) -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    with open(OUT / name, "w", newline="", encoding="utf-8") as fh:
        writer = csv.writer(fh, lineterminator="\n")
        writer.writerow(header)
        for row in rows:
            writer.writerow([f"{v:.4f}" if isinstance(v, float) else v for v in row])
    print(f"wrote {OUT / name} ({len(rows)} rows)")


def fmt(v: float) -> str:
    return f"{v:.4g}"


TOKEN_ROWS: list[list] = []  # rows: source, series, quantile, input_tokens, output_tokens
SUMMARY: list[list] = []  # rows: source, series, metric, value, unit, note


def summary(source: str, series: str, metric: str, value, unit: str, note: str = "") -> None:
    SUMMARY.append([source, series, metric, value if isinstance(value, str) else fmt(value), unit, note])


def duration_rows(source: str, series: str, durations_h: list[float], weights: list[float]) -> list[list]:
    """Histogram rows: share of jobs and share of weight (e.g. GPU-hours) per duration bin."""
    total_n, total_w = len(durations_h), sum(weights)
    counts, wsum = [0] * len(DURATION_BINS_H), [0.0] * len(DURATION_BINS_H)
    for d, w in zip(durations_h, weights):
        for i, (edge, _) in enumerate(DURATION_BINS_H):
            if d < edge:
                counts[i] += 1
                wsum[i] += w
                break
    return [[source, series, label, counts[i], counts[i] / total_n, wsum[i] / total_w if total_w else 0.0]
            for i, (_, label) in enumerate(DURATION_BINS_H)]


# ---------------------------------------------------------------- Azure LLM 2024
def azure2024() -> None:
    d = EXTERNAL / SOURCES["azure2024"]["dir"]
    rows, token_rows = [], []
    for service in ("code", "conv"):
        per_hour = defaultdict(lambda: [0, 0, 0])  # 'YYYY-MM-DD HH' UTC -> req, in, out
        ctx_all, gen_all = [], []
        with open(d / f"AzureLLMInferenceTrace_{service}_1week.csv", newline="") as fh:
            reader = csv.reader(fh)
            next(reader)
            for i, (ts, ctx, gen) in enumerate(reader):
                acc = per_hour[ts[:13]]
                c, g = int(ctx), int(gen)
                acc[0] += 1
                acc[1] += c
                acc[2] += g
                if i % 20 == 0:  # deterministic 5 % sample for token quantiles
                    ctx_all.append(c)
                    gen_all.append(g)
        hours = sorted(per_hour)
        # one row per hour of the trace, re-indexed to Zurich hour-of-week
        series = []
        for key in hours:
            t_utc = dt.datetime.strptime(key, "%Y-%m-%d %H").replace(tzinfo=UTC)
            t_zh = t_utc.astimezone(ZURICH)
            how = t_zh.weekday() * 24 + t_zh.hour
            series.append((how, t_zh, t_utc, *per_hour[key]))
        # keep exactly one value per hour-of-week (trace is ~7 days; drop partial edge hours)
        by_how = {}
        for s in series:
            by_how.setdefault(s[0], s)
        ordered = [by_how[h] for h in sorted(by_how)]
        if len(ordered) != HOURS_PER_WEEK:
            print(f"warning: azure {service} covers {len(ordered)} hours-of-week")
        req = normalise([s[3] for s in ordered])
        tin = normalise([s[4] for s in ordered])
        tout = normalise([s[5] for s in ordered])
        for s, r, a, b in zip(ordered, req, tin, tout):
            rows.append([service, s[0], WEEKDAYS[s[1].weekday()], s[1].hour, s[2].hour,
                         s[1].strftime("%Y-%m-%d %H:00%z"), s[3], s[4], s[5], r, a, b])
        n = sum(s[3] for s in ordered)
        summary("azure2024", service, "requests_in_week", n, "requests")
        summary("azure2024", service, "mean_requests_per_hour", n / len(ordered), "requests/h")
        summary("azure2024", service, "peak_to_trough_requests_hourly", peak_trough(req), "ratio",
                "max/min over 168 Zurich hours")
        summary("azure2024", service, "peak_to_mean_requests_hourly", max(req), "ratio")
        summary("azure2024", service, "peak_to_trough_output_tokens_hourly", peak_trough(tout), "ratio")
        wk = [sum(r for s, r in zip(ordered, req) if s[1].weekday() < 5) / (5 * 24),
              sum(r for s, r in zip(ordered, req) if s[1].weekday() >= 5) / (2 * 24)]
        summary("azure2024", service, "weekend_to_weekday_requests", wk[1] / wk[0], "ratio")
        ctx_all.sort()
        gen_all.sort()
        for q in QUANTILES:
            token_rows.append(["azure2024", service, q, quantile(ctx_all, q), quantile(gen_all, q)])
        token_rows.append(["azure2024", service, "mean", statistics.fmean(ctx_all), statistics.fmean(gen_all)])
    write_csv("llm-azure2024-hour-of-week.csv",
              ["service", "hour_of_week_zurich", "weekday_zurich", "hour_zurich", "hour_utc",
               "timestamp_zurich", "requests", "input_tokens", "output_tokens",
               "requests_rel", "input_tokens_rel", "output_tokens_rel"], rows)
    TOKEN_ROWS.extend(token_rows)


# ---------------------------------------------------------------- BurstGPT
def burstgpt() -> None:
    d = EXTERNAL / SOURCES["burstgpt"]["dir"]
    hod = defaultdict(lambda: [0] * 24)      # series -> requests per local hour
    dow = defaultdict(lambda: [0] * 7)       # series -> requests per day_index mod 7
    daily = defaultdict(lambda: defaultdict(lambda: [0, 0]))  # series -> day -> req, tokens
    per_minute = defaultdict(int)            # all series, minute index -> requests
    tokens = defaultdict(lambda: ([], []))
    n_days = 0
    for i in (1, 2):
        with open(d / f"BurstGPT_without_fails_{i}.csv", newline="") as fh:
            reader = csv.DictReader(fh)
            for n, rec in enumerate(reader):
                t = float(rec["Timestamp"])
                day = int(t // SECONDS_PER_DAY)
                n_days = max(n_days, day + 1)
                series = f'{rec["Model"]} / {rec["Log Type"]}'
                for s in (series, "all"):
                    hod[s][int(t % SECONDS_PER_DAY // SECONDS_PER_HOUR)] += 1
                    dow[s][day % 7] += 1
                    acc = daily[s][day]
                    acc[0] += 1
                    acc[1] += int(rec["Total tokens"])
                per_minute[(day, int(t % SECONDS_PER_DAY // 60))] += 1
                if n % 10 == 0:
                    tokens[series][0].append(int(rec["Request tokens"]))
                    tokens[series][1].append(int(rec["Response tokens"]))
        # File 2 continues file 1's clock (seconds since day 1 of the 121-day trace).
    rows = []
    for s in sorted(hod):
        rel = normalise(hod[s])
        for h in range(24):
            rows.append([s, h, hod[s][h], rel[h]])
        summary("burstgpt", s, "peak_to_trough_requests_hour_of_day", peak_trough(rel), "ratio",
                "24 local-clock hours, averaged over 121 days")
    write_csv("llm-burstgpt-hour-of-day.csv", ["series", "hour_local_source", "requests", "requests_rel"], rows)
    rows = []
    for s in sorted(dow):
        rel = normalise(dow[s])
        for k in range(7):
            rows.append([s, k, dow[s][k], rel[k]])
        summary("burstgpt", s, "max_to_min_day_index_mod7", peak_trough(rel), "ratio",
                "weekday offset unknown")
    write_csv("llm-burstgpt-day-index-mod7.csv", ["series", "day_index_mod7", "requests", "requests_rel"], rows)
    rows = []
    for s in sorted(daily):
        days = sorted(daily[s])
        rel = normalise([daily[s][k][0] for k in days])
        for k, r in zip(days, rel):
            rows.append([s, k, daily[s][k][0], daily[s][k][1], r])
    write_csv("llm-burstgpt-daily.csv", ["series", "day_index", "requests", "total_tokens", "requests_rel"], rows)
    # Burstiness: coefficient of variation of requests per minute (all series), and the
    # ratio of the 99th percentile minute to the mean minute.
    counts = [per_minute.get((day, m), 0) for day in range(n_days) for m in range(1440)]
    mean = statistics.fmean(counts)
    summary("burstgpt", "all", "cv_requests_per_minute", statistics.pstdev(counts) / mean, "ratio",
            "std/mean over all minutes incl. zeros")
    summary("burstgpt", "all", "p99_minute_to_mean_minute", quantile(sorted(counts), 0.99) / mean, "ratio")
    summary("burstgpt", "all", "days_covered", n_days, "days")
    for s, (req, resp) in sorted(tokens.items()):
        req.sort()
        resp.sort()
        for q in QUANTILES:
            TOKEN_ROWS.append(["burstgpt", s, q, quantile(req, q), quantile(resp, q)])
        TOKEN_ROWS.append(["burstgpt", s, "mean", statistics.fmean(req), statistics.fmean(resp)])


# ---------------------------------------------------------------- Alibaba GPU 2020
def alibaba() -> None:
    d = EXTERNAL / SOURCES["alibaba"]["dir"]
    csv_path = d / "pai_task_table.csv"
    if not csv_path.exists():
        with tarfile.open(d / "pai_task_table.tar.gz") as tar:
            member = tar.getmember("pai_task_table.csv")
            if not member.isfile():
                raise SystemExit("unexpected archive member")
            tar.extract(member, d, filter="data")
    arrivals = [0.0] * HOURS_PER_WEEK
    gpu_hours_by_how = [0.0] * HOURS_PER_WEEK
    durations, gpu_hours, gpus = [], [], []
    by_type = defaultdict(lambda: [0, 0.0])
    first_day, last_day = math.inf, -math.inf
    with open(csv_path, newline="") as fh:
        for rec in csv.reader(fh):
            r = dict(zip(ALIBABA_TASK_COLUMNS, rec))
            if r["status"] != "Terminated" or not r["start_time"] or not r["end_time"] or not r["plan_gpu"]:
                continue
            plan_gpu = float(r["plan_gpu"])
            if plan_gpu <= 0:
                continue
            start, end = float(r["start_time"]), float(r["end_time"])
            if end <= start:
                continue
            local = dt.datetime.fromtimestamp(start + SHANGHAI_UTC_OFFSET_S, UTC)  # original local clock
            how = local.weekday() * 24 + local.hour
            dur_h = (end - start) / SECONDS_PER_HOUR
            n_gpu = float(r["inst_num"] or 1) * plan_gpu / 100.0  # plan_gpu is % of one GPU
            arrivals[how] += 1
            gpu_hours_by_how[how] += n_gpu * dur_h
            durations.append(dur_h)
            gpu_hours.append(n_gpu * dur_h)
            gpus.append(n_gpu)
            by_type[r["gpu_type"]][0] += 1
            by_type[r["gpu_type"]][1] += n_gpu * dur_h
            first_day, last_day = min(first_day, start // SECONDS_PER_DAY), max(last_day, start // SECONDS_PER_DAY)
    weeks = (last_day - first_day + 1) / 7
    rel = normalise(arrivals)
    relg = normalise(gpu_hours_by_how)
    write_csv("research-alibaba2020-hour-of-week.csv",
              ["hour_of_week_local", "weekday_local", "hour_local", "task_arrivals", "arrivals_rel",
               "gpu_hours_started", "gpu_hours_started_rel"],
              [[h, WEEKDAYS[h // 24], h % 24, int(arrivals[h]), rel[h], gpu_hours_by_how[h], relg[h]]
               for h in range(HOURS_PER_WEEK)])
    hod = normalise([sum(arrivals[d * 24 + h] for d in range(7)) for h in range(24)])
    summary("alibaba2020", "gpu tasks", "tasks", len(durations), "tasks", "Terminated, plan_gpu > 0")
    summary("alibaba2020", "gpu tasks", "weeks_covered", weeks, "weeks")
    summary("alibaba2020", "gpu tasks", "peak_to_trough_arrivals_hour_of_week", peak_trough(rel), "ratio")
    summary("alibaba2020", "gpu tasks", "peak_to_trough_arrivals_hour_of_day", peak_trough(hod), "ratio")
    wk = [sum(arrivals[:120]) / 120, sum(arrivals[120:]) / 48]
    summary("alibaba2020", "gpu tasks", "weekend_to_weekday_arrivals", wk[1] / wk[0], "ratio")
    rows = duration_rows("alibaba2020", "gpu tasks", durations, gpu_hours)
    s_dur, s_gpu = sorted(durations), sorted(gpus)
    for q in QUANTILES:
        summary("alibaba2020", "gpu tasks", f"duration_h_q{int(q * 100)}", quantile(s_dur, q), "h")
    summary("alibaba2020", "gpu tasks", "duration_h_mean", statistics.fmean(durations), "h")
    for q in (0.5, 0.9, 0.99):
        summary("alibaba2020", "gpu tasks", f"gpus_requested_q{int(q * 100)}", quantile(s_gpu, q), "GPUs",
                "inst_num x plan_gpu/100")
    summary("alibaba2020", "gpu tasks", "share_tasks_below_1_gpu", sum(g < 1 for g in gpus) / len(gpus), "share")
    total_gh = sum(gpu_hours)
    for t, (n, gh) in sorted(by_type.items(), key=lambda kv: -kv[1][1]):
        summary("alibaba2020", "gpu tasks", f"gpu_hour_share_{t}", gh / total_gh, "share", f"{n} tasks")
    write_csv("job-durations.csv", ["source", "series", "duration_bin", "jobs", "share_jobs", "share_gpu_hours"],
              rows)


# ---------------------------------------------------------------- GetDeploying GPU price history
GPU_PRICE_SNAPSHOT = ROOT / "data" / "raw" / "demand-traces" / "getdeploying-gpu-prices" / "weekly-2026-11-21.csv"
GPU_PRICE_SLUGS = ["nvidia-h100", "nvidia-a100", "nvidia-l40s", "nvidia-l4", "nvidia-rtx-5090",
                   "nvidia-rtx-4090", "nvidia-rtx-3090", "nvidia-t4"]
# USD->CHF as used throughout data/raw/market (docs/research/ai-services.md, WP1, 2026-11-20).
USD_TO_CHF = 0.8402


def gpu_prices() -> None:
    """Weekly provider-median on-demand and spot price per GPU-hour, with the spot/on-demand ratio."""
    weeks = defaultdict(dict)  # (slug, date) -> billing -> provider_median_price
    with open(GPU_PRICE_SNAPSHOT, newline="") as fh:
        for rec in csv.DictReader(fh):
            if rec["gpu_slug"] in GPU_PRICE_SLUGS and rec["billing_type"] in ("ON_DEMAND", "SPOT"):
                weeks[(rec["gpu_slug"], rec["date"])][rec["billing_type"]] = (
                    float(rec["provider_median_price"]), int(rec["provider_count"]))
    rows = []
    for (slug, date), b in sorted(weeks.items()):
        od, sp = b.get("ON_DEMAND"), b.get("SPOT")
        rows.append([slug, date,
                     od[0] if od else "", od[1] if od else "", sp[0] if sp else "", sp[1] if sp else "",
                     od[0] * USD_TO_CHF if od else "", sp[0] * USD_TO_CHF if sp else "",
                     sp[0] / od[0] if od and sp else ""])
    write_csv("gpu-price-history.csv",
              ["gpu_slug", "week_monday", "on_demand_usd_per_gpu_h", "on_demand_providers",
               "spot_usd_per_gpu_h", "spot_providers", "on_demand_chf_per_gpu_h", "spot_chf_per_gpu_h",
               "spot_to_on_demand"], rows)
    for slug in GPU_PRICE_SLUGS:
        series = [(d, b["ON_DEMAND"][0]) for (s, d), b in sorted(weeks.items()) if s == slug and "ON_DEMAND" in b]
        if len(series) >= 2:
            (d0, p0), (d1, p1) = series[0], series[-1]
            summary("getdeploying", slug, "on_demand_change", p1 / p0 - 1, "share", f"{d0} -> {d1}")
        ratios = [b["SPOT"][0] / b["ON_DEMAND"][0] for (s, d), b in weeks.items()
                  if s == slug and "SPOT" in b and "ON_DEMAND" in b]
        if ratios:
            summary("getdeploying", slug, "spot_to_on_demand_median", statistics.median(ratios), "ratio",
                    f"{len(ratios)} weeks")


# ---------------------------------------------------------------- main
def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--no-download", action="store_true", help="use files already in data/external/")
    ap.add_argument("--only", default="azure2024,burstgpt,alibaba,gpuprices", help="comma-separated sources")
    args = ap.parse_args()
    chosen = [s.strip() for s in args.only.split(",") if s.strip()]
    for s in chosen:
        if s not in SOURCES:
            raise SystemExit(f"unknown source {s}")
        if not args.no_download:
            download(s)
    for s in chosen:
        {"azure2024": azure2024, "burstgpt": burstgpt, "alibaba": alibaba, "gpuprices": gpu_prices}[s]()
    tag = "-".join(chosen) if args.only != ap.get_default("only") else "all"
    write_csv(f"summary{'' if tag == 'all' else '-' + tag}.csv",
              ["source", "series", "metric", "value", "unit", "note"], SUMMARY)
    if TOKEN_ROWS:
        write_csv(f"llm-token-quantiles{'' if tag == 'all' else '-' + tag}.csv",
                  ["source", "series", "quantile", "input_tokens", "output_tokens"], TOKEN_ROWS)


if __name__ == "__main__":
    main()
