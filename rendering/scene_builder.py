import bpy, math, random, json
from mathutils import Vector

def build_scene(settings, recipe_id):
    random.seed(settings['seed'])
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    scene=bpy.context.scene
    scene.render.resolution_x=settings['width']; scene.render.resolution_y=settings['height']
    scene.render.resolution_percentage=100; scene.render.image_settings.file_format='PNG'
    scene.render.fps=settings['fps']; scene.frame_start=1; scene.frame_end=settings['frames']
    def mat(name,color,rough=.7,metal=0):
        m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
        p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=rough;p.inputs['Metallic'].default_value=metal
        return m
    rock=mat('Alpine stone',(.21,.27,.29));grass=mat('Meadow',(.12,.23,.08));snow=mat('Snow',(.8,.85,.9));trunk=mat('Bark',(.16,.07,.025));pine=mat('Pine foliage',(.025,.13,.065));water=mat('Alpine lake',(.025,.19,.24),.12,.35)
    def height(x,y):
        return .25*math.sin(x*.8)*math.cos(y*.7)+3.4*math.exp(-((x+5)**2+(y-6)**2)/15)+4.2*math.exp(-((x-3)**2+(y-8)**2)/12)+2.5*math.exp(-((x-8)**2+(y-5)**2)/10)-.35
    n=100;verts=[];faces=[]
    for j in range(n+1):
     for i in range(n+1):
        x=-13+26*i/n;y=-9+22*j/n;verts.append((x,y,height(x,y)))
    for j in range(n):
     for i in range(n):
        a=j*(n+1)+i;faces.append((a,a+1,a+n+2,a+n+1))
    mesh=bpy.data.meshes.new('Landscape mesh');mesh.from_pydata(verts,[],faces);mesh.update()
    o=bpy.data.objects.new('Mountain landscape',mesh);scene.collection.objects.link(o)
    for m in (grass,rock,snow):o.data.materials.append(m)
    for poly in mesh.polygons:
        z=sum(mesh.vertices[v].co.z for v in poly.vertices)/len(poly.vertices);poly.material_index=2 if z>2.8 else 1 if z>1 else 0;poly.use_smooth=True
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.15));bpy.context.object.name='Lake';bpy.context.object.data.materials.append(water)
    for k in range(100):
     x=random.uniform(-11,11);y=random.uniform(-4,9);z=height(x,y)
     if z<0 or z>1.8:continue
     scale=random.uniform(.35,.75)
     bpy.ops.mesh.primitive_cone_add(vertices=7,radius1=.09*scale,depth=1.3*scale,location=(x,y,z+.6*scale));bpy.context.object.data.materials.append(trunk)
     for dz,r in ((.65,.5),(.95,.4),(1.2,.3)):
        bpy.ops.mesh.primitive_cone_add(vertices=9,radius1=r*scale,depth=.8*scale,location=(x,y,z+dz*scale));bpy.context.object.data.materials.append(pine)
    for k in range(35):
     x=random.uniform(-10,10);y=random.uniform(-4,7);z=height(x,y)
     if z<-.1:continue
     bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=random.uniform(.1,.35),location=(x,y,z));bpy.context.object.scale=(1.4,1,.7);bpy.context.object.data.materials.append(rock)
    bpy.ops.object.light_add(type='SUN',location=(0,-5,10));bpy.context.object.rotation_euler=(.4,-.5,-.3);bpy.context.object.data.energy=3
    scene.world.color=(.25,.35,.5)

    bpy.ops.object.camera_add()
    camera=bpy.context.object;camera.data.lens=32;scene.camera=camera
    for frame in range(1,settings['frames']+1):
        t=(frame-1)/max(1,settings['frames']-1); angle=-.25+.5*t
        camera.location=(11*math.sin(angle),-15+1.5*t,6.5)
        camera.rotation_euler=(Vector((0,3,1))-camera.location).to_track_quat('-Z','Y').to_euler()
        camera.keyframe_insert('location',frame=frame);camera.keyframe_insert('rotation_euler',frame=frame)
    scene['digitalheat_recipe_id']=recipe_id
    scene['digitalheat_settings']=json.dumps(settings,sort_keys=True)
    scene.frame_set(1)
    return scene
