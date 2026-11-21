# One trusted Blender process; each frame remains a separately owned scheduler attempt.
import sys,os,json,time,traceback
from pathlib import Path
from contextlib import contextmanager
sys.path.insert(0,str(Path(__file__).parent))
from native_worker import execute,save

spec=json.loads(Path(sys.argv[sys.argv.index('--')+1]).read_text(encoding='utf-8-sig'))
root=Path(spec['directory']);state_path=root/'owned.json'

@contextmanager
def gate():
    # Same byte-range lock as .NET FileStream.Lock(0,1), on Windows and Linux.
    with (root/'gate.lock').open('a+b') as lock:
        lock.seek(0)
        if os.name=='nt':
            import msvcrt
            msvcrt.locking(lock.fileno(),msvcrt.LK_LOCK,1)
        else:
            import fcntl
            fcntl.lockf(lock,fcntl.LOCK_EX,1,0)
        try:yield
        finally:
            lock.seek(0)
            if os.name=='nt':msvcrt.locking(lock.fileno(),msvcrt.LK_UNLCK,1)
            else:fcntl.lockf(lock,fcntl.LOCK_UN,1,0)

# The owner grants activation only after durably recording this exact PID/start identity.
ready=time.monotonic()+60
while not (root/'activate').exists():
    if time.monotonic()>ready:raise RuntimeError('Warm worker activation not granted')
    time.sleep(.05)
if (root/'activate').read_text()!=spec['activation']:raise RuntimeError('Wrong warm worker activation')
idle=time.monotonic()
while True:
    with gate():
        state=json.loads(state_path.read_text(encoding='utf-8-sig'))
        if state['process']['activation']!=spec['activation'] or state['retiring']:break
        activation=state.get('active_activation');directory=state.get('active_directory')
        if not activation:
            if time.monotonic()-idle>=spec['idle_timeout_seconds']:
                state['retiring']=True;save(root,'owned.json',state);break
        else:idle=time.monotonic()
    if not activation:
        time.sleep(.05);continue
    out=Path(directory);job=json.loads((out/'spec.json').read_text(encoding='utf-8-sig'))
    if job['activation']!=activation or job['recipe_id']!=spec['recipe_id']:raise RuntimeError('Warm frame activation/recipe mismatch')
    failed=False
    try:execute(job,keep_scene=True,publish_completion=False)
    except BaseException as error:
        failure_message=str(error)
        traceback.print_exc();failed=True
    # Completion is atomic before assignment release. Dispatch/idle/stop share this gate.
    with gate():
        state=json.loads(state_path.read_text(encoding='utf-8-sig'))
        if state.get('active_activation')!=activation:raise RuntimeError('Active frame ownership changed')
        marker={'code':1 if failed else 0,'activation':activation,'recipe_id':spec['recipe_id']}
        if failed:marker['error']=failure_message
        save(out,'exit.json',marker)
        state['active_activation']=None;state['active_directory']=None
        if failed:state['retiring']=True
        save(root,'owned.json',state)
    idle=time.monotonic()
    if failed:break
