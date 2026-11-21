import bpy, math, random, sys, json, time, os
from mathutils import Vector
args=sys.argv[sys.argv.index('--')+1:]
random.seed(66)
def device_report():
    prefs=bpy.context.preferences.addons['cycles'].preferences
    supported=[item[0] for item in prefs.get_device_types(bpy.context) if item[0]!='NONE']
    found=[]; errors={}
    for backend in supported:
        try:
            prefs.compute_device_type=backend
            prefs.get_devices()
            for d in prefs.devices:
                if d.type==backend: found.append({'name':d.name,'backend':backend,'id':d.id})
        except Exception as error: errors[backend]=str(error)
    return {'blender':bpy.app.version_string,'compiled_backends':supported,
            'gpus':found,'backend_errors':errors,'cpu':True}

if '--probe' in args:
    print('RENDER_DEVICES='+json.dumps(device_report()))
    sys.exit(0)
if '--verify-gpu' in args:
    try:
        backend=args[1]
        prefs=bpy.context.preferences.addons['cycles'].preferences
        prefs.compute_device_type=backend;prefs.get_devices()
        gpu=[d for d in prefs.devices if d.type==backend]
        if not gpu:raise RuntimeError('No compatible GPU')
        for d in prefs.devices:d.use=d.type==backend
        s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.device='GPU';s.cycles.samples=1;s.cycles.use_denoising=False
        s.render.resolution_x=8;s.render.resolution_y=8;s.render.resolution_percentage=100
        bpy.ops.render.render()
        print('RENDER_GPU_VERIFIED='+backend)
        sys.exit(0)
    except Exception as error:
        print("RENDER_GPU_ERROR="+str(error),flush=True)
        raise
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent))
from scene_builder import build_scene
settings=json.loads((Path(__file__).parent/'scene_settings.json').read_text())
frame=int(args[0]);backend=args[1] if len(args)>1 else 'CPU'
scene=build_scene(settings,'shared-procedural-landscape')
scene.render.engine='CYCLES';scene.cycles.samples=settings['samples'];scene.cycles.use_denoising=False
scene.cycles.seed=settings['seed'];scene.cycles.use_animated_seed=False
scene.render.threads_mode='FIXED';scene.render.threads=1
used='CPU';device='CPU fallback'
if backend!='CPU':
    prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type=backend;prefs.get_devices()
    selected=[d for d in prefs.devices if d.type==backend]
    if not selected:raise RuntimeError('Requested GPU unavailable; no silent device substitution')
    for d in prefs.devices:d.use=d.type==backend
    scene.cycles.device='GPU';used=backend;device=', '.join(d.name for d in selected)
else:scene.cycles.device='CPU'
scene.frame_set(frame);scene.render.filepath='/work/frame.png'
start=time.monotonic();bpy.ops.render.render(write_still=True)
meta={'frame':frame,'backend':used,'device':device,'blender':bpy.app.version_string,'render_seconds':time.monotonic()-start,**settings}
with open('/work/result.json','w') as f:json.dump(meta,f)
print('RENDER_RESULT='+json.dumps(meta))
