"""Controlled real MeshPeer benchmark, not a replacement placement policy.
Run only on an idle trusted local presentation lab; retains every attempt/output.
Uses existing pinned_node admission to constrain the measured worker count.
"""
import argparse, collections, json, pathlib, statistics, time, urllib.request, uuid

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--lab',required=True)
parser.add_argument('--frames',type=int,default=12)
parser.add_argument('--order',default='3,1,2,2,1,3')
parser.add_argument('--timeout-seconds',type=float,default=300)
args=parser.parse_args()
if not 0<args.timeout_seconds<=600:raise ValueError('Timeout must be within (0,600] seconds')
lab=pathlib.Path(args.lab).resolve()
configs=[json.loads(p.read_text(encoding='utf-8-sig')) for p in lab.glob('*/config.json')]
origin=next(c for c in configs if c['serve_ui'])
base=origin['peers'][origin['id']]
token=(lab/'token.txt').read_text().strip()
def request(url,data=None):
    headers={'X-Mesh-Token':token}
    if data is not None:headers['Content-Type']='application/json'
    with urllib.request.urlopen(urllib.request.Request(url,data=None if data is None else json.dumps(data).encode(),headers=headers),timeout=15) as r:
        raw=r.read();return json.loads(raw) if raw else None
snapshot=json.load(urllib.request.urlopen(base+'/ui/status'))
if snapshot['phase']!='complete' or not snapshot['can_start_new']:raise RuntimeError('Finish the current UI render before benchmarking')
if not 1<=args.frames<=origin['frames']:raise ValueError('Invalid frame count')
workers=sorted(c['id'] for c in configs if c['worker_role']=='render')
order=[int(v) for v in args.order.split(',')]
if any(v<1 or v>len(workers) for v in order):raise ValueError('Invalid concurrency')
all_peers=[request(url+'/status') for url in origin['peers'].values()]
if any(a['phase']!='released' for p in all_peers for a in p['attempts']):raise RuntimeError('Unreleased prior work; inspect ownership before benchmarking')
scene=snapshot['preparation']['scene_sha256'];prefix='bench-'+uuid.uuid4().hex[:10]
output=lab/(prefix+'.json')
report={'method':'Existing heat-first MeshPeer, benchmark-only pinned worker subset, identical prepared scene and frame range. Includes normal admission and dispatch. Dispatch wall clock is not scheduler microbenchmark latency.','order':order,'frames_per_sample':args.frames,'job_ids':[],'samples':[],'limitations':'One Iris Xe. Warm scene reuse recorded per sample; first-frame compilation included. No default concurrency change.'}
def checkpoint():output.write_text(json.dumps(report,indent=2),encoding='utf-8')
checkpoint()
def sample(count,frames,label):
    selected=workers[:count];ids=[];begin=time.perf_counter()
    for i in range(frames):
        local=prefix+'-'+label+'-'+str(i);ids.append(origin['id']+'.'+local);report['job_ids'].append(ids[-1]);checkpoint()
        request(base+'/frames',{'job_id':local,'frame':i+1,'backend':'EEVEE','pinned_node':selected[i%count]+'-worker','scene_sha256':scene})
    deadline=time.monotonic()+args.timeout_seconds
    while time.monotonic()<deadline:
        peers=[request(origin['peers'][w]+'/status') for w in selected]
        if any(p.get('error') for p in peers):raise RuntimeError('Peer error; inspect retained lab logs')
        done=[r for p in peers for r in p['results'] if r['job_id'] in ids]
        failed=[a for p in peers for a in p['attempts'] if a['attempt']['request']['id'] in ids and a['phase']=='released' and not any(r['job_id']==a['attempt']['request']['id'] for r in done)]
        if failed:raise RuntimeError('A benchmark attempt was cancelled or failed; retained IDs: '+str(ids))
        if len(done)==frames:
            return {'gpu_workers':count,'frames':frames,'wall_seconds':time.perf_counter()-begin,'render_seconds_sum':sum(r['metadata']['render_seconds'] for r in done),'warm_scene_reused_frames':sum(r['metadata'].get('warm_scene_reused',False) for r in done),'frames_by_worker':dict(collections.Counter(r['peer_id'] for r in done))}
        report['partial_sample']={'label':label,'gpu_workers':count,'completed':len(done),'job_ids':ids};checkpoint()
        time.sleep(.2)
    raise TimeoutError('Benchmark jobs timed out; retained IDs: '+str(ids))
# Warm all workers, then retain the actual per-frame reuse flags (idle retirement may occur).
try:
    report['warmup']=sample(max(order),max(order),'warmup');checkpoint();print('Warmup:',json.dumps(report['warmup']),flush=True)
    for i,count in enumerate(order):
        row=sample(count,args.frames,str(i));report['samples'].append(row);checkpoint();print(json.dumps(row),flush=True)
    report['median_wall_seconds_by_concurrency']={str(n):statistics.median(r['wall_seconds'] for r in report['samples'] if r['gpu_workers']==n) for n in sorted(set(order))}
    report['status']='complete';checkpoint()
except BaseException as error:
    report['status']='failed';report['error']=str(error);checkpoint()
    try:
        for url in origin['peers'].values():request(url+'/hold',{'hold':True})
        for url in origin['peers'].values():request(url+'/cancel',{'job_ids':report['job_ids']})
    except Exception as cancel_error:report['cancel_error']=str(cancel_error);checkpoint()
    raise
finally:print('Report:',str(output),flush=True)
