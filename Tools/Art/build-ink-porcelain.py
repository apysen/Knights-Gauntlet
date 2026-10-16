"""Build the approved five-piece ink-painted porcelain set in an isolated Blender scene."""
import bpy, math, json
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Resources/Pieces/InkPorcelain'
SOURCE = ROOT / 'ArtSource/Models/InkPorcelain'
OUT.mkdir(parents=True, exist_ok=True)
SOURCE.mkdir(parents=True, exist_ok=True)
previous = bpy.context.window.scene
scene = bpy.data.scenes.new('KG_InkPorcelain')
bpy.context.window.scene = scene

def material(name, rgb, rough=.3, metal=0):
    mat = bpy.data.materials.new('InkPorcelain_' + name)
    mat.diffuse_color = (*rgb, 1)
    mat.use_nodes = True
    node = mat.node_tree.nodes.get('Principled BSDF')
    node.inputs['Base Color'].default_value = (*rgb, 1)
    node.inputs['Roughness'].default_value = rough
    node.inputs['Metallic'].default_value = metal
    return mat

ivory = material('Ivory', (.88,.80,.64))
ink = material('Ink', (.018,.028,.040), .72)
gold = material('Gold', (.69,.40,.105), .24, .65)
blue = material('PaintBlue', (.035,.11,.34))
teal = material('PaintTeal', (.065,.29,.27))
clay = material('PaintClay', (.44,.16,.085))
jade = material('PaintJade', (.055,.24,.14))
plum = material('PaintPlum', (.27,.055,.15))

def finish(obj, mat, bevel=0, smooth=True):
    obj.data.materials.append(mat)
    bpy.context.view_layer.objects.active = obj
    if bevel:
        mod = obj.modifiers.new('Rounded porcelain edge', 'BEVEL')
        mod.width = bevel
        mod.segments = 3
        bpy.ops.object.modifier_apply(modifier=mod.name)
    for face in obj.data.polygons: face.use_smooth = smooth
    return obj

def mesh_object(name, verts, faces, mat, smooth=True):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    return finish(obj, mat, smooth=smooth)

def lathe(name, profile, mat=ivory, paint=None, segments=64):
    # Subdivide long profile sections so painted sweeps retain a flowing edge.
    dense=[]
    for a,b in zip(profile[:-1],profile[1:]):
        steps=max(1,math.ceil(abs(b[1]-a[1])/.045))
        for k in range(steps):
            t=k/steps
            dense.append((a[0]*(1-t)+b[0]*t,a[1]*(1-t)+b[1]*t))
    dense.append(profile[-1])
    verts=[(r*math.cos(i*math.tau/segments),r*math.sin(i*math.tau/segments),z)
           for r,z in dense for i in range(segments)]
    faces=[]
    for j in range(len(dense)-1):
        for i in range(segments):
            a=j*segments+i; b=j*segments+(i+1)%segments
            faces.append((a,b,b+segments,a+segments))
    obj=mesh_object(name,verts,faces,mat)
    if paint:
        obj.data.materials.append(paint)
        for face in obj.data.polygons:
            center=face.center
            angle=math.atan2(center.y,center.x)
            # Two broad winding brush regions read from both player facings.
            edge=math.sin(2*angle+center.z*3.5)+.10*math.sin(angle*19+center.z*47)
            if edge>.28: face.material_index=1
    return obj

def box(name, center, size, mat=ivory, bevel=.015):
    bpy.ops.mesh.primitive_cube_add(size=1,location=center)
    obj=bpy.context.object; obj.name=name; obj.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish(obj,mat,bevel,False)

def ball(name, center, radius, mat=ivory, segments=24, rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,radius=radius,location=center)
    obj=bpy.context.object; obj.name=name
    return finish(obj,mat)

def profile_mesh(name, profile, half_depth, mat=ivory, bevel=.018, y=0):
    n=len(profile)
    verts=[(x,side+y,z) for side in (-half_depth,half_depth) for x,z in profile]
    faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    obj=mesh_object(name,verts,faces,mat,False)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True); bpy.context.view_layer.objects.active=obj
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False); bpy.ops.object.mode_set(mode='OBJECT')
    if bevel:
        mod=obj.modifiers.new('Soft carved edges','BEVEL'); mod.width=bevel; mod.segments=3
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj

def seam(name, points, radius=.0035, mat=gold):
    data=bpy.data.curves.new(name,'CURVE'); data.dimensions='3D'
    data.bevel_depth=radius; data.bevel_resolution=1; data.resolution_u=2
    line=data.splines.new('POLY'); line.points.add(len(points)-1)
    for p,co in zip(line.points,points): p.co=(*co,1)
    obj=bpy.data.objects.new(name,data); scene.collection.objects.link(obj)
    data.materials.append(mat)
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.convert(target='MESH')
    return bpy.context.object

def lathe_seam(profile, phase=-.95):
    points=[]
    for index,(r,z) in enumerate(profile):
        if r<.04: continue
        angle=phase+.09*math.sin(index*2.1)+.20*z
        points.append(((r+.003)*math.cos(angle),(r+.003)*math.sin(angle),z))
    return seam('Fine gold repaired seam',points)

def base():
    return [
        lathe('Matte ink foot',[(0,0),(.32,0),(.355,.018),(.36,.038),(.36,.102),(.35,.12),(0,.12)],ink),
        lathe('Lower gold rim',[(.35,.017),(.362,.021),(.362,.034),(.35,.039)],gold),
        lathe('Upper gold rim',[(.35,.100),(.361,.105),(.361,.116),(.35,.120)],gold),
        lathe('Ivory pedestal',[(0,.112),(.35,.112),(.354,.132),(.34,.16),(.30,.182),(.255,.203),(0,.203)])
    ]

def combine(name, parts):
    bpy.ops.object.select_all(action='DESELECT')
    for part in parts: part.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]; bpy.ops.object.join()
    obj=bpy.context.object; obj.name=name
    scene.cursor.location=(0,0,0); bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    # Remove empty material slots created by booleans/join while preserving palette.
    bpy.ops.object.material_slot_remove_unused()
    return obj

models=[]
try:
    parts=base()
    horse=[(-.235,.19),(.205,.19),(.20,.30),(.11,.46),(.08,.64),(.13,.78),
           (.26,.70),(.34,.75),(.34,.85),(.19,.97),(.075,1.035),
           (.025,1.17),(-.045,1.16),(-.08,1.06),(-.16,1.03),
           (-.265,.88),(-.29,.64),(-.285,.42)]
    parts.append(profile_mesh('Sculpted horse',horse,.135,bevel=.035))
    mane=[(-.29,.42),(-.335,.58),(-.32,.86),(-.22,1.07),(-.06,1.20),
          (-.04,1.12),(-.16,1.00),(-.245,.83),(-.25,.62),(-.22,.46)]
    parts.append(profile_mesh('Cobalt angular mane',mane,.155,blue,.012))
    patch=[(-.21,.23),(.07,.23),(.04,.35),(-.09,.53),(-.12,.73),
           (-.10,.91),(-.17,.88),(-.21,.69),(-.19,.48),(-.08,.31)]
    for sign in (-1,1):
        parts.append(profile_mesh('Painted neck sweep',patch,.0025,blue,.002,y=sign*.137))
        eye=box('Carved eye slit',(.115,sign*.139,.947),(.055,.008,.016),ink,.003)
        eye.rotation_euler[1]=-.18
        parts.append(eye)
        points=[(-.24,sign*.142,.38),(-.19,sign*.142,.43),(-.20,sign*.142,.50),
                (-.15,sign*.142,.55),(-.12,sign*.142,.66)]
        parts.append(seam('Horse gold repair',points))
    models.append(combine('knight',parts))

    parts=base()
    parts.append(lathe('Tower plinth',[(0,.195),(.26,.195),(.26,.24),(.235,.28),(0,.28)]))
    parts.append(box('Square tower body',(0,0,.49),(.43,.43,.46),ivory,.038))
    for side in range(4):
        angle=side*math.pi/2
        panel=box('Inset terracotta panel',(0,-.216,.49),(.255,.008,.30),clay,.012)
        panel.location=Vector((-math.sin(angle)*panel.location.y,math.cos(angle)*panel.location.y,.49))
        panel.rotation_euler[2]=angle
        parts.append(panel)
    parts.append(lathe('Tower ink undercut',[(.23,.69),(.27,.69),(.27,.72),(.23,.72)],ink))
    for sign in (-1,1):
        parts.append(box('Crown wall',(sign*.225,0,.78),(.10,.54,.13),clay,.012))
        parts.append(box('Crown wall',(0,sign*.225,.78),(.35,.10,.13),clay,.012))
    parts.append(box('Dark tower well',(0,0,.737),(.35,.35,.018),ink,.006))
    for x in (-.22,.22):
        for y in (-.22,.22):
            parts.append(box('Ivory battlement',(x,y,.91),(.17,.17,.21),ivory,.022))
    parts.append(seam('Tower repaired edge',[(.16,-.217,.29),(.14,-.217,.36),(.17,-.217,.43),(.145,-.217,.50),(.15,-.217,.61)]))
    models.append(combine('rook',parts))

    parts=base()
    stem=[(0,.195),(.255,.195),(.23,.24),(.19,.30),(.145,.43),(.123,.56),(.13,.65),(.19,.69),(0,.69)]
    parts.append(lathe('Painted bishop stem',stem,paint=teal))
    parts.append(lathe('Bishop ivory collar',[(0,.665),(.21,.665),(.235,.69),(.235,.725),(.21,.75),(0,.75)]))
    head_profile=[(0,.73),(.12,.73),(.18,.77),(.215,.85),(.22,.92),(.195,1.03),(.145,1.15),(.075,1.28),(.025,1.36),(0,1.39)]
    head=lathe('Deep-cut mitre',head_profile,paint=teal)
    solid=head.copy(); solid.data=head.data.copy(); scene.collection.objects.link(solid)
    cut=box('Slit tool',(.045,0,1.16),(.083,.8,.54),ink,0)
    cut.rotation_euler[1]=math.radians(-26)
    bpy.context.view_layer.objects.active=head
    mod=head.modifiers.new('Open diagonal mitre cut','BOOLEAN'); mod.operation='DIFFERENCE'; mod.solver='EXACT'; mod.object=cut
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cut,do_unlink=True)
    cavity=box('Dark carved mitre interior',(.045,0,1.16),(.075,.026,.54),ink,0)
    cavity.rotation_euler[1]=math.radians(-26)
    bpy.context.view_layer.objects.active=cavity
    mod=cavity.modifiers.new('Bound the slit lining','BOOLEAN'); mod.operation='INTERSECT'; mod.object=solid
    bpy.ops.object.modifier_apply(modifier=mod.name)
    cavity.data.materials.clear(); cavity.data.materials.append(ink)
    for face in cavity.data.polygons: face.material_index=0
    bpy.data.objects.remove(solid,do_unlink=True)
    parts.extend([head,cavity,lathe_seam(stem,phase=-1.5)])
    models.append(combine('bishop',parts))

    parts=base()
    stem=[(0,.195),(.24,.195),(.215,.23),(.175,.30),(.125,.42),(.115,.51),(.135,.55),(0,.55)]
    parts.append(lathe('Pawn jade brush stem',stem,paint=jade))
    parts.append(lathe('Pawn ivory collar',[(0,.53),(.18,.53),(.21,.55),(.215,.58),(.19,.61),(0,.61)]))
    parts.append(ball('Round pawn head',(0,0,.785),.205,segments=32,rings=20))
    points=[]
    for i in range(14):
        theta=.2+i*2.65/13
        a=-.85+.09*math.sin(i*1.7)
        points.append((.208*math.sin(theta)*math.cos(a),.208*math.sin(theta)*math.sin(a),.785+.208*math.cos(theta)))
    parts.append(seam('Pawn gold repair',points))
    models.append(combine('pawn',parts))

    parts=base()
    stem=[(0,.195),(.255,.195),(.24,.24),(.205,.30),(.17,.40),(.135,.57),(.12,.76),(.14,.88),(.20,.94),(0,.94)]
    parts.append(lathe('Queen plum brush stem',stem,paint=plum))
    parts.append(lathe('Queen ivory collar',[(0,.915),(.22,.915),(.255,.94),(.255,.975),(.22,1.005),(0,1.005)]))
    # A hollow six-point crown, with a smooth outside and plum inside.
    verts=[]; faces=[]; n=48
    for ring in range(4):
        for i in range(n):
            a=i*math.tau/n
            peak=(.5+.5*math.cos(a*6))**1.7
            if ring==0: r,z=.185,.99
            elif ring==1: r,z=.27+.035*peak,1.20+.23*peak
            elif ring==2: r,z=.244+.035*peak,1.20+.23*peak
            else: r,z=.162,1.015
            verts.append((r*math.cos(a),r*math.sin(a),z))
    for ring in range(3):
        for i in range(n):
            a=ring*n+i; b=ring*n+(i+1)%n
            faces.append((a,b,b+n,a+n))
    crown=mesh_object('Open six-point queen crown',verts,faces,ivory)
    crown.data.materials.append(plum)
    for face in crown.data.polygons:
        if face.index>=2*n or (face.index<n and (face.index//8)%2==1): face.material_index=1
    parts.append(crown)
    parts.append(lathe('Crown interior floor',[(0,1.014),(.165,1.014),(.165,1.027),(0,1.027)],plum))
    for i in range(6):
        a=i*math.tau/6
        parts.append(ball('Porcelain crown pearl',(.305*math.cos(a),.305*math.sin(a),1.45),.044,segments=16,rings=10))
    parts.append(lathe_seam(stem,phase=-.8))
    models.append(combine('queen',parts))

    report=[]
    for obj in models:
        bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
        bpy.context.view_layer.objects.active=obj
        bpy.context.view_layer.update()
        obj.data.calc_loop_triangles()
        bpy.ops.export_scene.fbx(filepath=str(OUT/(obj.name+'.fbx')),use_selection=True,
            object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,
            add_leaf_bones=False,path_mode='STRIP',use_mesh_modifiers=True)
        report.append({'name':obj.name,'triangles':len(obj.data.loop_triangles),
            'materials':len(obj.data.materials),'height':round(obj.dimensions.z,4),'diameter':.724})
    # Add a useful inspection setup to the editable source without exporting it.
    bpy.ops.object.camera_add(location=(3,-5,3))
    camera=bpy.context.object; camera.name='Porcelain inspection camera'
    camera.rotation_euler=(Vector((0,0,.7))-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.type='ORTHO'; camera.data.ortho_scale=2; scene.camera=camera
    bpy.ops.object.light_add(type='AREA',location=(-3,-4,6))
    bpy.context.object.name='Porcelain softbox'; bpy.context.object.data.energy=650
    bpy.context.object.data.shape='DISK'; bpy.context.object.data.size=5
    # Separate models in the source for convenient editing; exports stay at origin.
    for i,obj in enumerate(models): obj.location.x=(i-2)*1.05
    bpy.data.libraries.write(str(SOURCE/'ink-porcelain-set.blend'),{scene},compress=True)
    (SOURCE/'build-report.json').write_text(json.dumps(report,indent=2))
    print(json.dumps(report))
finally:
    bpy.context.window.scene=previous
    for obj in list(scene.objects): bpy.data.objects.remove(obj,do_unlink=True)
    bpy.data.scenes.remove(scene)
