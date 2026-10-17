"""Blender native 3D-to-sprite animation proof. No Meshy dependency.
Run setup(), then render_frames(size, start=1, end=11) through Blender bridge.
The control hierarchy is a rigid-part animation rig, not a skinned character.
"""
import bpy, math, os, json
from mathutils import Vector, Matrix, Quaternion
from bpy_extras.object_utils import world_to_camera_view

OUT = os.path.dirname(os.path.abspath(__file__))
PALETTE = {
    'blue': ['1B2857', '354D9A', '6A82CC'],
    'ivory': ['8E9AAE', 'CDD5D5', 'FFF0CE'],
    'gold': ['92502F', 'CC8C3E', 'F4C866'],
    'navy': ['131A30', '263650', '43566A'],
}

def linear(h):
    values=[int(h[i:i+2],16)/255 for i in (0,2,4)]
    return [v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in values]+[1]

def toon(group):
    m=bpy.data.materials.new('PixelKnight_'+group); m.use_nodes=True
    n=m.node_tree.nodes; n.clear(); links=m.node_tree.links
    diffuse=n.new('ShaderNodeBsdfDiffuse'); diffuse.inputs['Color'].default_value=(1,1,1,1)
    rgb=n.new('ShaderNodeShaderToRGB'); ramp=n.new('ShaderNodeValToRGB')
    ramp.color_ramp.interpolation='CONSTANT'
    ramp.color_ramp.elements.remove(ramp.color_ramp.elements[1])
    for i,h in enumerate(PALETTE[group]):
        e=ramp.color_ramp.elements[0] if i==0 else ramp.color_ramp.elements.new([0,.38,.72][i])
        e.color=linear(h)
    emission=n.new('ShaderNodeEmission'); output=n.new('ShaderNodeOutputMaterial')
    links.new(diffuse.outputs[0],rgb.inputs[0]); links.new(rgb.outputs[0],ramp.inputs[0])
    links.new(ramp.outputs['Color'],emission.inputs[0]); links.new(emission.outputs[0],output.inputs[0])
    return m

def reparent(o,parent):
    bpy.context.view_layer.update()
    world=o.matrix_world.copy(); o.parent=parent
    bpy.context.view_layer.update(); o.matrix_world=world
    bpy.context.view_layer.update()

def pivot(o,xyz):
    children=[(c,c.matrix_world.copy()) for c in o.children]
    o.location=xyz; bpy.context.view_layer.update()
    for c,matrix in children: c.matrix_world=matrix

def joint(name,parent,position):
    bpy.context.view_layer.update()
    o=bpy.data.objects.new(name,None); scene.collection.objects.link(o)
    o.parent=parent; o.matrix_world=Matrix.Translation(position)
    bpy.context.view_layer.update()
    o.rotation_mode='QUATERNION'
    return o

def pose_sword(hand,direction):
    """Two rigid arm segments plus wrist orientation, in unmirrored author space."""
    upper,elbow,wrist=(controls[k] for k in ('right','elbow','wrist'))
    bpy.context.view_layer.update()
    shoulder=upper.matrix_world.translation.copy()
    upper_rest=rest['elbow'][0]; lower_rest=rest['wrist'][0]
    l1,l2=upper_rest.length,lower_rest.length
    delta=Vector(hand)-shoulder; distance=min(delta.length,l1+l2-.005)
    forward=delta.normalized()
    bend=Vector((1,.1,0)); bend=(bend-forward*bend.dot(forward)).normalized()
    along=(l1*l1-l2*l2+distance*distance)/(2*distance)
    height=math.sqrt(max(0,l1*l1-along*along))
    elbow_pos=shoulder+forward*along+bend*height
    hand_pos=shoulder+forward*distance
    upper_world=upper_rest.rotation_difference(elbow_pos-shoulder)
    lower_world=lower_rest.rotation_difference(hand_pos-elbow_pos)
    upper.rotation_quaternion=upper.parent.matrix_world.to_quaternion().inverted()@upper_world
    elbow.rotation_quaternion=upper_world.inverted()@lower_world
    blade_world=Vector((.46,0,1.22)).rotation_difference(Vector(direction).normalized())
    wrist.rotation_quaternion=lower_world.inverted()@blade_world
    bpy.context.view_layer.update()

def smear():
    # The slash is on the opponent-facing (-Y) side. Actual mesh depth lets
    # the knight and cape occlude it from our rear camera; no back overlay.
    points=[Vector(p) for p in [(.74,3.15),(.75,2.80),(.51,2.24),(.08,1.66),(-.48,1.02),(-.98,.65)]]
    for label,factor,color in [('Gold edge',1,'F4C866'),('Ivory core',.55,'FFF0CE')]:
        widths=[.008,.035,.075,.105,.13,.025]; verts=[]
        for i,p in enumerate(points):
            tangent=points[min(i+1,len(points)-1)]-points[max(0,i-1)]
            normal=Vector((-tangent.y,tangent.x)).normalized()*widths[i]*factor
            for side in (-1,1):
                q=p+normal*side
                verts.append(root.matrix_world@Vector((q.x,-.50+(.002 if factor<1 else 0),q.y)))
        faces=[(i*2,i*2+1,i*2+3,i*2+2) for i in range(len(points)-1)]
        data=bpy.data.meshes.new('Diagonal smear '+label); data.from_pydata(verts,[],faces); data.update()
        obj=bpy.data.objects.new('Diagonal smear '+label,data); scene.collection.objects.link(obj)
        material=bpy.data.materials.new('Pixel smear '+label); material.use_nodes=True
        nodes=material.node_tree.nodes; nodes.clear(); e=nodes.new('ShaderNodeEmission'); e.inputs[0].default_value=linear(color)
        output=nodes.new('ShaderNodeOutputMaterial'); material.node_tree.links.new(e.outputs[0],output.inputs[0]); data.materials.append(material)
        for f in range(1,12): obj.hide_render=f!=10; obj.keyframe_insert(data_path='hide_render',frame=f)

def setup():
    global scene, root, controls, rest
    source=bpy.data.objects.get('EnchantedKnight')
    if source is None:
        path=os.path.join(OUT,'..','Models','EnchantedKnight','enchanted-knight.blend')
        with bpy.data.libraries.load(path,link=False) as (a,b): b.scenes=['Enchanted Knight Studio']
        source=bpy.data.objects['EnchantedKnight']
    scene=bpy.data.scenes.new('Pixel Knight Animation Proof')
    bpy.context.window.scene=scene
    source_objects=[source]+list(source.children_recursive); copies={}
    for old in source_objects:
        new=old.copy()
        if old.data is not None: new.data=old.data.copy()
        scene.collection.objects.link(new); copies[old]=new
        new.animation_data_clear()
    for old,new in copies.items():
        new.parent=copies.get(old.parent); new.matrix_local=old.matrix_local.copy()
        new.hide_render=False; new.hide_set(False)
    bpy.context.view_layer.update()
    root=copies[source]; root.name='PixelKnight_Root'
    # Tiny fingers, badges and inlays add flicker instead of readable shape.
    for old,new in list(copies.items()):
        if any(s in old.name for s in ['Finger plate','Tasset ivory inlay','Temple center','Buckle inset']):
            bpy.data.objects.remove(new,do_unlink=True)
    mats={g:toon(g) for g in PALETTE}
    for o in root.children_recursive:
        if o.type!='MESH': continue
        name=o.data.materials[0].name if o.data.materials else ''
        group='gold' if 'Gold' in name or 'gold' in name else 'ivory' if 'Ivory' in name or 'Blade' in name else 'navy' if 'navy' in name or 'Deep blue' in name else 'blue'
        o.data.materials.clear(); o.data.materials.append(mats[group])
        for p in o.data.polygons: p.material_index=0; p.use_smooth=False
        if 'Pleated gold cape' in o.name:
            # Keep the feet and low sword visible from the elevated rear camera.
            for v in o.data.vertices: v.co.z=1.67+(v.co.z-1.67)*.80
            o.data.update()
    groups={old.name:copies[old] for old in source.children}
    core=bpy.data.objects.new('PixelKnight_TorsoControl',None); scene.collection.objects.link(core)
    core.parent=root; core.location=(0,0,1.05)
    for name in ['Torso','HorseHelmet','GoldenCape','LeftArm','SwordArm']: reparent(groups[name],core)
    pivot(groups['HorseHelmet'],(0,.015,.70))
    pivot(groups['GoldenCape'],(0,.25,.62))
    pivot(groups['LeftArm'],(-.45,0,.57)); pivot(groups['SwordArm'],(.45,0,.57))
    controls={'core':core,'head':groups['HorseHelmet'],'cape':groups['GoldenCape'],'left':groups['LeftArm'],'right':groups['SwordArm']}
    elbow=joint('PixelKnight_RightElbow',groups['SwordArm'],(.61,-.02,1.25))
    wrist=joint('PixelKnight_RightWrist',elbow,(.72,-.33,1.04))
    source_arm=next(o for o in source.children if o.name=='SwordArm')
    for old in source_arm.children:
        if old.type=='EMPTY' or old.name.startswith('Gauntlet'):
            reparent(copies[old],wrist)
        elif old.name.startswith(('Forearm vambrace','Wrist cuff','Elbow hinge')):
            reparent(copies[old],elbow)
    controls.update(elbow=elbow,wrist=wrist)
    controls['right'].rotation_mode='QUATERNION'
    rest={k:(o.location.copy(),o.rotation_euler.copy(),o.scale.copy()) for k,o in controls.items()}
    tip=joint('SwordTip_Measurement',wrist,(1.18,-.33,2.26))
    for f in range(1,12):
        for k,o in controls.items():
            o.location,o.rotation_euler,o.scale=[v.copy() for v in rest[k]]
            if o.rotation_mode=='QUATERNION': o.rotation_quaternion=Quaternion()
        if f<=8:
            t=(f-1)*math.tau/8
            core.location.z+=.017*math.sin(t)
            controls['head'].rotation_euler.z=.025*math.sin(t)
            controls['cape'].rotation_euler.x=.055*math.sin(t-.8)
            controls['left'].rotation_euler.y=.035*math.sin(t+.8)
            controls['right'].rotation_quaternion=Quaternion((0,1,0),.025*math.sin(t))
        else:
            i=f-9
            core.rotation_euler.z=[.12,-.12,-.18][i]
            core.rotation_euler.x=[-.06,.12,.16][i]
            core.location.z+=[-.015,-.035,-.045][i]
            controls['head'].rotation_euler.z=[-.05,.04,.06][i]
            controls['left'].rotation_euler.y=[.12,-.18,-.1][i]
            controls['cape'].rotation_euler.x=[0,-.10,-.05][i]
            pose_sword([(.62,-.10,2.10),(.08,-.28,1.60),(-.26,-.10,1.25)][i],
                       [(.10,-.03,1),(-.80,0,-.60),(-.48,0,-.88)][i])
        for o in controls.values():
            for path in ['location','rotation_quaternion' if o.rotation_mode=='QUATERNION' else 'rotation_euler','scale']:
                o.keyframe_insert(data_path=path,frame=f)
    # The original mockup put the weapon in the viewer's right in front view.
    # Mirror the authoring rig so the weapon is anatomically right in rear view.
    root.scale.x=-1
    scene.frame_start=1; scene.frame_end=11; scene.render.fps=12
    scene.timeline_markers.new('Idle (frames 1-8)',frame=1)
    scene.timeline_markers.new('Raised - 33 ms',frame=9)
    scene.timeline_markers.new('Diagonal cut - 33 ms',frame=10)
    scene.timeline_markers.new('Low finish - 67 ms',frame=11)
    world=bpy.data.worlds.new('Pixel knight ambient'); world.use_nodes=True
    world.node_tree.nodes['Background'].inputs[0].default_value=(1,1,1,1)
    world.node_tree.nodes['Background'].inputs[1].default_value=.25; scene.world=world
    light=bpy.data.lights.new('Fixed pixel key','SUN'); light.energy=2
    obj=bpy.data.objects.new('Fixed pixel key',light); scene.collection.objects.link(obj)
    obj.rotation_euler=(Vector((0,0,1))-Vector((-3,4,6))).to_track_quat('-Z','Y').to_euler()
    d=bpy.data.cameras.new('Pixel sprite camera'); cam=bpy.data.objects.new('Pixel sprite camera',d)
    scene.collection.objects.link(cam); scene.camera=cam
    cam.location=(0,7,6.14); cam.rotation_euler=(Vector((0,0,1.42))-cam.location).to_track_quat('-Z','Y').to_euler()
    d.type='ORTHO'; d.ortho_scale=4.2
    # Blender 5.x uses BLENDER_EEVEE; older builds use BLENDER_EEVEE_NEXT.
    found=False
    for engine in ['BLENDER_EEVEE','BLENDER_EEVEE_NEXT']:
        try: scene.render.engine=engine; found=True; break
        except TypeError: pass
    if not found: raise RuntimeError('Eevee renderer required for palette materials')
    scene.eevee.taa_render_samples=16
    scene.render.film_transparent=True
    scene.render.image_settings.file_format='PNG'; scene.render.image_settings.color_mode='RGBA'
    scene.render.resolution_percentage=100
    scene.render.resolution_x=64; scene.render.resolution_y=64
    scene.view_settings.view_transform='Standard'; scene.view_settings.look='None'; scene.view_settings.exposure=0; scene.view_settings.gamma=1
    scene.render.filter_size=.1
    bpy.context.view_layer.update()
    # Slash VFX is rendered separately by Unity's pixel-grid shader. Keep all
    # three character drawings free of a baked smear so it can be timed/tuned.
    metrics=[]
    for f in (9,10,11):
        scene.frame_set(f); bpy.context.view_layer.update()
        tip_world=tip.matrix_world.translation
        grip_world=wrist.matrix_world.translation
        p=world_to_camera_view(scene,cam,tip_world)
        g=world_to_camera_view(scene,cam,grip_world)
        metrics.append({'frame':f,'tipWorld':list(tip_world),'gripWorld':list(grip_world),'tipScreen':list(p),'gripScreen':list(g)})
    if not (metrics[0]['tipScreen'][0]>.5 and metrics[2]['tipScreen'][0]<.5):
        raise RuntimeError('Sword must travel from screen right to screen left')
    if not (metrics[0]['tipWorld'][2]>2.8 and metrics[2]['tipWorld'][2]<.35):
        raise RuntimeError('Sword must start above helmet and finish near ground')
    if not metrics[2]['tipWorld'][2]<metrics[2]['gripWorld'][2]-.6:
        raise RuntimeError('Finishing blade must point downward')
    with open(os.path.join(OUT,'pose-validation.json'),'w') as fp: json.dump(metrics,fp,indent=2)
    scene.frame_set(1)
    os.makedirs(OUT,exist_ok=True)
    bpy.data.libraries.write(os.path.join(OUT,'pixel-knight-animation.blend'),{scene},fake_user=True,compress=True)
    print('Rear-view proof: 8 idle + 3 capture poses. Sword-tip path and down-pointing finish validated.')

def render_frames(size,start=1,end=11):
    scene=bpy.context.scene
    scene.render.resolution_x=size; scene.render.resolution_y=size
    folder=os.path.join(OUT,'renders',str(size)); os.makedirs(folder,exist_ok=True)
    for frame in range(start,end+1):
        scene.frame_set(frame); scene.render.filepath=os.path.join(folder,'%02d.png'%frame)
        bpy.ops.render.render(write_still=True)
    print('Rendered %dpx frames %d-%d'%(size,start,end))
