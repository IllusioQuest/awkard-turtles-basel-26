# Trusted procedural scene only. Both CPU preparation and GPU frames use this script.
import bpy, gpu, json, sys, time, os, hashlib, traceback
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent))
from scene_builder import build_scene
def save(out,name,value):
    p=out/name;t=p.with_suffix(p.suffix+'.tmp')
    t.write_text(json.dumps(value,sort_keys=True),encoding='utf-8');os.replace(t,p)
def context():
    return {'device':gpu.platform.renderer_get(),'vendor':gpu.platform.vendor_get(),'graphics_api':gpu.platform.backend_type_get()}
loaded_scene=None
rendered_scene=None

def execute(spec,keep_scene=False,publish_completion=True):
    global loaded_scene,rendered_scene
    out=Path(spec['directory']);out.mkdir(parents=True,exist_ok=True)
    if spec.get('activation'):
        deadline=time.monotonic()+60
        while not (out/'activate').exists():
            if time.monotonic()>deadline: raise RuntimeError('Owner never activated this attempt')
            time.sleep(.1)
        if (out/'activate').read_text()!=spec['activation']: raise RuntimeError('Activation identity mismatch')
    settings=json.loads(Path(spec['settings_file']).read_text())
    mode=spec['mode'];started=time.monotonic()
    meta={'ffmpeg_available':getattr(bpy.app.build_options,'codec_ffmpeg',False),'stage':mode,'blender':bpy.app.version_string,'build_hash':bpy.app.build_hash.decode(),'recipe_id':spec['recipe_id'],**settings}
    if mode=='info':
        save(out,'result.json',meta)
    elif mode=='prepare':
        scene=build_scene(settings,spec['recipe_id'])
        scene.render.engine='BLENDER_EEVEE'
        if hasattr(scene,'eevee'):scene.eevee.taa_render_samples=settings['samples']
        bpy.ops.file.pack_all()
        if bpy.data.libraries or any(i.filepath and not i.packed_file for i in bpy.data.images if i.source=='FILE'):
            raise RuntimeError('External scene dependencies are not permitted')
        target=out/'scene.blend';bpy.ops.wm.save_as_mainfile(filepath=str(target),compress=True)
        meta.update(scene_sha256=hashlib.sha256(target.read_bytes()).hexdigest(),prepare_seconds=time.monotonic()-started,external_assets='none; packed procedural scene')
        save(out,'result.json',meta)
    elif mode=='assemble':
        path=Path(spec['manifest_path']);raw=path.read_bytes();manifest=json.loads(raw)
        if hashlib.sha256(raw).hexdigest()!=path.name.split('.')[0]:raise RuntimeError('Assembly manifest hash mismatch')
        if manifest['recipe_id']!=spec['recipe_id'] or manifest['build_hash']!=bpy.app.build_hash.decode():raise RuntimeError('Assembly recipe/build mismatch')
        frames=manifest['frames']
        if len(frames)!=spec['expected_frames']:raise RuntimeError('Incomplete assembly frame set')
        for index,f in enumerate(frames,1):
            target=(path.parent/f['file']).resolve()
            if target.parent!=path.parent.resolve() or f['frame']!=index or hashlib.sha256(target.read_bytes()).hexdigest()!=f['sha256']:raise RuntimeError('Assembly frame identity mismatch')
        scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU'
        editor=scene.sequence_editor_create()
        strip=editor.strips.new_image('Rendered frames',filepath=str(path.parent/frames[0]['file']),channel=1,frame_start=1)
        for f in frames[1:]:strip.elements.append(f['file'])
        strip.frame_final_duration=len(frames)
        scene.render.use_sequencer=True;scene.frame_start=1;scene.frame_end=len(frames)
        scene.render.fps=manifest['fps'];scene.render.resolution_x=settings['width'];scene.render.resolution_y=settings['height'];scene.render.resolution_percentage=100
        scene.render.image_settings.media_type='VIDEO';scene.render.image_settings.file_format='FFMPEG';scene.render.ffmpeg.format='MPEG4';scene.render.ffmpeg.codec='H264';scene.render.ffmpeg.audio_codec='NONE'
        scene.render.filepath=str(out/'video.mp4');begin=time.monotonic();bpy.ops.render.render(animation=True)
        candidates=list(out.glob('video*.mp4'))
        if len(candidates)!=1:raise RuntimeError('Expected exactly one encoded video')
        if candidates[0]!=out/'video.mp4':os.replace(candidates[0],out/'video.mp4')
        clip=bpy.data.movieclips.load(str(out/'video.mp4'))
        if clip.frame_duration!=len(frames):raise RuntimeError('Encoded video frame count mismatch')
        meta.update(stage='assemble',frames=len(frames),fps=manifest['fps'],encode_seconds=time.monotonic()-begin,manifest_sha256=hashlib.sha256(raw).hexdigest(),scene_sha256=manifest['scene_sha256'],encoded_frames=clip.frame_duration,device='CPU video encoding')
        save(out,'result.json',meta)
    elif mode in ('frame','probe'):
        if mode=='frame':
            path=Path(spec['scene_path'])
            if hashlib.sha256(path.read_bytes()).hexdigest()!=spec['scene_sha256']:raise RuntimeError('Scene content mismatch')
            identity=(spec['scene_sha256'],spec['recipe_id'])
            if not keep_scene or loaded_scene!=identity:
                bpy.ops.wm.open_mainfile(filepath=str(path),load_ui=False,use_scripts=False)
                loaded_scene=identity
            if bpy.context.scene.get('digitalheat_recipe_id')!=spec['recipe_id']:raise RuntimeError('Scene recipe mismatch')
        scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE'
        if hasattr(scene,'eevee'):scene.eevee.taa_render_samples=settings['samples']
        scene.render.resolution_x=8 if mode=='probe' else settings['width']
        scene.render.resolution_y=8 if mode=='probe' else settings['height']
        scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG'
        scene.frame_set(spec.get('frame',1));scene.render.filepath=str(out/'frame.png')
        meta['warm_scene_reused']=bool(keep_scene and rendered_scene==loaded_scene)
        start=time.monotonic();bpy.ops.render.render(write_still=True)
        meta.update(context());meta.update(backend='EEVEE',engine='BLENDER_EEVEE',frame=spec.get('frame',1),scene_sha256=spec.get('scene_sha256'),render_seconds=time.monotonic()-start)
        name=(meta['device'] or '').lower()
        if not name or any(s in name for s in ('llvmpipe','softpipe','software','gdi generic','swiftshader','lavapipe')):
            raise RuntimeError('No verified hardware graphics context: '+name)
        rendered_scene=loaded_scene
        save(out,'result.json',meta)
    else:raise RuntimeError('Unknown trusted job stage')
    if publish_completion:save(out,'exit.json',{'code':0,'activation':spec.get('activation'),'recipe_id':spec['recipe_id']})

if __name__=='__main__':
    spec=json.loads(Path(sys.argv[sys.argv.index('--')+1]).read_text(encoding='utf-8-sig'))
    try:execute(spec)
    except BaseException as error:
        save(Path(spec['directory']),'exit.json',{'code':1,'activation':spec.get('activation'),'recipe_id':spec['recipe_id'],'error':str(error)})
        traceback.print_exc();raise
