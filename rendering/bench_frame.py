"""T-79 diagnostic: does a warm Blender process render later frames slower than its first?

Compares two ways of rendering the same prepared scene, one frame at a time, on an idle GPU:
  warm  - one Blender process renders every frame (what the warm GPU workers do, T-71/T-77)
  fresh - a new Blender process per frame (startup + scene load + shader compile each time)
Both call the production code path, native_worker.execute, with the production Blender flags
(NativeRenderRuntime.cs). Nothing here changes the renderer, scheduler or quality settings.

Run on the presentation laptop with the render lab stopped (rendering/Stop-RenderLab.ps1):
  python rendering/bench_frame.py --blender <path-to-blender.exe>
Results: rendering/TestResults/bench-frame-<utc>/bench-results.json plus a printed summary.

Limits: sequential single-worker timings, not the three-concurrent presentation setup; one
machine, few rounds. Use it to decide whether process recycling is worth building, not as an SLA.
"""
import hashlib, json, statistics, subprocess, sys, time
from datetime import datetime, timezone
from pathlib import Path

HERE = Path(__file__).resolve().parent
SOFTWARE_CONTEXT_ERROR = 'No verified hardware graphics context'


def inner(plan_path):
    """Runs inside Blender: execute each frame spec in this process, timing every call."""
    sys.path.insert(0, str(HERE))
    from native_worker import execute
    plan = json.loads(Path(plan_path).read_text(encoding='utf-8'))
    rows = []
    for index, spec in enumerate(plan['specs']):
        started = time.monotonic(); software = False
        try:
            execute(spec, keep_scene=plan['keep_scene'], publish_completion=False)
        except RuntimeError as error:
            # The production check rejects software OpenGL after rendering. Only test setups pass this flag.
            if not (plan['allow_software'] and str(error).startswith(SOFTWARE_CONTEXT_ERROR)): raise
            software = True
        call_seconds = time.monotonic() - started
        out = Path(spec['directory']); result = out / 'result.json'
        meta = json.loads(result.read_text()) if result.exists() else {}
        rows.append({'index_in_process': index, 'frame': spec['frame'], 'call_seconds': call_seconds,
                     'render_seconds': meta.get('render_seconds'), 'scene_load_seconds': meta.get('scene_load_seconds'),
                     'device': meta.get('device'), 'software_context': software,
                     'png_pixels_sha256': png_pixels_sha256(out / 'frame.png')})
    Path(plan['report']).write_text(json.dumps(rows), encoding='utf-8')


def png_pixels_sha256(path):
    """Hash of the PNG image data chunks only: Blender stamps date and render time into text chunks."""
    data = Path(path).read_bytes(); pos = 8; digest = hashlib.sha256()
    while pos < len(data):
        length = int.from_bytes(data[pos:pos + 4], 'big'); kind = data[pos + 4:pos + 8]
        if kind in (b'IHDR', b'IDAT'): digest.update(data[pos + 8:pos + 8 + length])
        pos += 12 + length
    return digest.hexdigest()


def blender(args, script, spec_path):
    """Same flags as NativeRenderRuntime.cs; returns wall seconds including process startup."""
    command = [args.blender, '--factory-startup', '--disable-autoexec', '--background', '--gpu-backend', args.gpu_backend,
               '--threads', str(args.threads), '--python-exit-code', '1', '--python', str(script), '--', str(spec_path)]
    started = time.monotonic()
    done = subprocess.run(command, capture_output=True, text=True, encoding='utf-8', errors='replace')
    seconds = time.monotonic() - started
    (Path(spec_path).parent / 'blender.log').write_text(done.stdout + done.stderr, encoding='utf-8')
    if done.returncode != 0: raise RuntimeError(f'Blender failed ({done.returncode}); see {Path(spec_path).parent / "blender.log"}')
    return seconds


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True); path.write_text(json.dumps(value, indent=1), encoding='utf-8')
    return path


def run_mode(args, mode, round_no, frames, scene, root):
    base = root / f'{mode}-r{round_no}'
    specs = [{'mode': 'frame', 'directory': str(base / f'{i:02d}-f{f:03d}'), 'frame': f, 'scene_path': scene['path'],
              'scene_sha256': scene['sha256'], 'settings_file': str(HERE / 'scene_settings.json'), 'recipe_id': args.recipe_id}
             for i, f in enumerate(frames)]
    batches = [specs] if mode == 'warm' else [[s] for s in specs]
    rows = []
    for b, batch in enumerate(batches):
        plan = {'specs': batch, 'keep_scene': mode == 'warm', 'allow_software': args.allow_software,
                'report': str(base / f'report-{b:02d}.json')}
        process_seconds = blender(args, Path(__file__).resolve(), write(base / f'plan-{b:02d}.json', plan))
        for row in json.loads(Path(plan['report']).read_text()):
            rows.append({'mode': mode, 'round': round_no, 'process': b, 'process_seconds': process_seconds, **row})
    return rows


def median(values):
    values = [v for v in values if v is not None]
    return round(statistics.median(values), 3) if values else None


def summarize(rows):
    warm = [r for r in rows if r['mode'] == 'warm']; fresh = [r for r in rows if r['mode'] == 'fresh']
    warm_wall = sum({(r['round'], r['process']): r['process_seconds'] for r in warm}.values())
    fresh_wall = sum({(r['round'], r['process']): r['process_seconds'] for r in fresh}.values())
    images = {}
    for r in rows: images.setdefault(r['frame'], set()).add(r['png_pixels_sha256'])
    return {
        'warm_first_render_median_s': median([r['render_seconds'] or r['call_seconds'] for r in warm if r['index_in_process'] == 0]),
        'warm_later_render_median_s': median([r['render_seconds'] or r['call_seconds'] for r in warm if r['index_in_process'] > 0]),
        'fresh_render_median_s': median([r['render_seconds'] or r['call_seconds'] for r in fresh]),
        'fresh_process_median_s': median([r['process_seconds'] for r in fresh]),
        'warm_wall_seconds_per_frame': round(warm_wall / len(warm), 3) if warm else None,
        'fresh_wall_seconds_per_frame': round(fresh_wall / len(fresh), 3) if fresh else None,
        'identical_images_across_modes': all(len(h) == 1 for h in images.values()),
        'software_context': any(r['software_context'] for r in rows),
    }


def outer():
    import argparse
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument('--blender', required=True, help='Blender executable used by the render lab')
    p.add_argument('--frames', type=int, default=6, help='frames per mode and round, spread over the animation')
    p.add_argument('--rounds', type=int, default=2, help='rounds; mode order alternates to limit order bias')
    p.add_argument('--threads', type=int, help='Blender threads (default: presentation-profile cpu_threads_per_worker)')
    p.add_argument('--gpu-backend', help='default: presentation-profile graphics_api')
    p.add_argument('--out', type=Path, help='default: rendering/TestResults/bench-frame-<utc>')
    p.add_argument('--allow-software', action='store_true', help='test setups only: accept a software OpenGL context')
    args = p.parse_args()
    profile = json.loads((HERE / 'presentation-profile.json').read_text(encoding='utf-8'))
    settings = json.loads((HERE / 'scene_settings.json').read_text(encoding='utf-8'))
    args.threads = args.threads or profile['cpu_threads_per_worker']
    args.gpu_backend = args.gpu_backend or profile['graphics_api']
    args.recipe_id = 'bench-frame'
    stamp = datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ')
    root = (args.out or HERE / 'TestResults' / f'bench-frame-{stamp}').resolve()
    total = settings['frames']
    frames = sorted({1 + round(i * (total - 1) / max(1, args.frames - 1)) for i in range(args.frames)})

    prep = root / 'prepare'
    spec = write(prep / 'spec.json', {'mode': 'prepare', 'directory': str(prep), 'recipe_id': args.recipe_id,
                                       'settings_file': str(HERE / 'scene_settings.json')})
    prepare_seconds = blender(args, HERE / 'native_worker.py', spec)
    prepared = json.loads((prep / 'result.json').read_text())
    scene = {'path': str(prep / 'scene.blend'), 'sha256': prepared['scene_sha256']}

    rows = []
    for round_no in range(1, args.rounds + 1):
        for mode in (('warm', 'fresh') if round_no % 2 else ('fresh', 'warm')):
            print(f'round {round_no}: {mode} ({len(frames)} frames)', flush=True)
            rows += run_mode(args, mode, round_no, frames, scene, root)

    summary = summarize(rows)
    report = {'ticket': 'T-79', 'completed_utc': datetime.now(timezone.utc).isoformat(), 'blender': prepared['blender'],
              'build_hash': prepared['build_hash'], 'device': next((r['device'] for r in rows if r['device']), None),
              'gpu_backend': args.gpu_backend, 'threads': args.threads, 'frames': frames, 'rounds': args.rounds,
              'settings': settings, 'scene_sha256': scene['sha256'], 'prepare_process_seconds': prepare_seconds,
              'summary': summary, 'rows': rows}
    write(root / 'bench-results.json', report)
    print(json.dumps(summary, indent=1))
    print('Results:', root / 'bench-results.json')


if __name__ == '__main__':
    if '--' in sys.argv: inner(sys.argv[sys.argv.index('--') + 1])
    else: outer()
