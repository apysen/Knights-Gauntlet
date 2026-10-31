"""Editable lightweight idle rig for the concept-matched Meshy knight.
Uses existing 20k triangle source, never modifies the original asset.
Blender Z up, character faces +Y. Unity export Y up, faces -Z.
"""
import bpy, os, json, math, array
from mathutils import Vector, Matrix
OUT=os.path.dirname(os.path.abspath(__file__))
PROJECT=os.path.abspath(os.path.join(OUT,'../../..'))

def smooth(a,b,x):
 t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)

def build():
 source=bpy.data.objects.get('MeshyKnightMobile')
 if source is None:
  path=os.path.join(OUT,'../MeshyKnight/meshy-knight.blend')
  with bpy.data.libraries.load(path,link=False) as (s,d):d.objects=['MeshyKnightMobile']
  source=d.objects[0]
 scene=bpy.data.scenes.new('Concept Knight 3D Pixel Proof');bpy.context.window.scene=scene
 mesh=source.copy();mesh.data=source.data.copy();mesh.name='ConceptKnightMesh';mesh.animation_data_clear()
 scene.collection.objects.link(mesh);mesh.matrix_world=source.matrix_world.copy()
 # Bake the existing normalization; preserve UVs and painted surface.
 mesh.data.transform(mesh.matrix_world);mesh.matrix_world=Matrix.Identity(4)
 for m in list(mesh.modifiers):mesh.modifiers.remove(m)
 points=[v.co for v in mesh.data.vertices]
 feet=[p for p in points if p.z<.065]
 feet_by_side=[[p for p in feet if p.x<0],[p for p in feet if p.x>=0]]
 centers=[]
 for group in feet_by_side:
  centers.append(Vector(((min(p.x for p in group)+max(p.x for p in group))/2,
    (min(p.y for p in group)+max(p.y for p in group))/2,0)))
 center=(centers[0]+centers[1])*.5;bottom=min(p.z for p in points)
 for v in mesh.data.vertices:v.co-=Vector((center.x,center.y,bottom))
 data=bpy.data.armatures.new('ConceptKnightRig');rig=bpy.data.objects.new('ConceptKnightRig',data);scene.collection.objects.link(rig)
 bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
 specs={'Root':((0,0,0),(0,0,.2),None),
 'Chest':((0,0,.38),(0,0,.68),'Root'),
 'Head':((0,0,.69),(0,0,.95),'Chest'),
 'Cape':((0,-.08,.67),(0,-.16,.30),'Chest'),
 'SwordArm':((.22,.03,.62),(.34,.03,.38),'Chest'),
 'FreeArm':((-.22,.03,.62),(-.34,.03,.38),'Chest')}
 for name,(a,b,parent) in specs.items():
  bone=data.edit_bones.new(name);bone.head=a;bone.tail=b
  if parent:bone.parent=data.edit_bones[parent]
 bpy.ops.object.mode_set(mode='OBJECT')
 groups={name:mesh.vertex_groups.new(name=name) for name in specs}
 counts={name:0 for name in specs}
 colors=mesh.data.color_attributes.new(name='CapeMask',type='FLOAT_COLOR',domain='POINT')
 # Use the painted surface to distinguish blue/gold cloth from dark boots at
 # the same low height. Spatial height alone cannot separate this fused mesh.
 image=next(n.image for mat in source.data.materials for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image)
 iw,ih=image.size;pixels=array.array('f',[0])*(iw*ih*4);image.pixels.foreach_get(pixels)
 painted=[None]*len(mesh.data.vertices)
 for loop in mesh.data.loops:
  if painted[loop.vertex_index] is not None:continue
  uv=mesh.data.uv_layers.active.data[loop.index].uv;px=int(uv.x*iw)%iw;py=int(uv.y*ih)%ih
  painted[loop.vertex_index]=pixels[(py*iw+px)*4:(py*iw+px)*4+3]
 for v in mesh.data.vertices:
  x,y,z=v.co
  upper=smooth(.27,.48,z)
  head=smooth(.66,.74,z)*(1-smooth(.20,.28,abs(x)))
  # Coordinates are already relative to the boot midpoint, which shifts Y by
  # about .087m. Use that centered space when isolating the rear cloth.
  back=1-smooth(.10,.20,y)
  cape=back*(1-smooth(.66,.74,z))*smooth(.18,.32,z)
  r,g,b=painted[v.index]
  blue=b>r*1.3 and b>g*1.1 and b>.12
  gold=r>g*1.12 and g>b*1.2 and r>.18
  lowcloth=back*(1-smooth(.28,.36,z))*float(blue or (gold and (y<-.04 or abs(x)>.25)))
  cape=max(cape,lowcloth) if z>.045 else 0
  upper=max(upper,lowcloth if z>.045 else 0)
  colors.data[v.index].color=(cape,0,0,1)
  # Pull the overly flared generated cape toward the back and lift its hem.
  # Preserve the boot contacts and torso; the rear silhouette must reveal feet.
  v.co.x=x*(1-.14*cape)
  v.co.y=y+(-.08+(y+.08)*.5-y)*cape
  v.co.z=z+(.67+(z-.67)*.60-z)*cape
  arm=smooth(.19,.29,abs(x))*(1-cape)*(1-head)*upper
  weights={'Root':1-upper,'Head':head*upper,'Cape':cape*upper,
   'SwordArm':arm if x>0 else 0,'FreeArm':arm if x<0 else 0}
  total=sum(weights.values());weights['Chest']=max(0,1-total)
  total=sum(weights.values())
  for name,value in weights.items():
   if value>.001:groups[name].add([v.index],value/total,'REPLACE');counts[name]+=1
 mesh.parent=rig;mod=mesh.modifiers.new('Idle deformation rig','ARMATURE');mod.object=rig
 # Preserve the posed design; only idle deformation is exported in this proof.
 rig['purpose']='Native 3D, foot-rooted idle proof; mobile source from approved knight concept.'
 mesh['source']='MeshyKnightMobile, 20k triangles; original source preserved.'
 bpy.context.view_layer.update()
 folder=os.path.join(PROJECT,'Assets/Art/PixelKnight3D');os.makedirs(folder,exist_ok=True)
 bpy.ops.object.select_all(action='DESELECT');mesh.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
 bpy.ops.export_scene.fbx(filepath=os.path.join(folder,'ConceptKnightRig.fbx'),use_selection=True,
  object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
  bake_anim=False,add_leaf_bones=False,path_mode='AUTO',use_mesh_modifiers=True)
 bpy.data.libraries.write(os.path.join(OUT,'concept-knight-rig.blend'),{scene},fake_user=True,compress=True)
 report={'triangles':sum(len(p.vertices)-2 for p in mesh.data.polygons),'vertices':len(mesh.data.vertices),
  'footAnchorBeforeCorrection':list(center),'bottom':bottom,'boneVertexCounts':counts,
  'idle':'Unity bone transforms; feet stay in Root; no board translation.'}
 with open(os.path.join(OUT,'rig-metrics.json'),'w') as f:json.dump(report,f,indent=2)
 print(json.dumps(report))
