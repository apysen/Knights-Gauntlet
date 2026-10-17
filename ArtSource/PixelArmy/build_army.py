"""Original rigid-part characters. Forward -Y. Match knight native pixel camera."""
import bpy, math, os, json, types
from mathutils import Vector
OUT=os.path.dirname(os.path.abspath(__file__))
# Reuse the knight's exact stepped material implementation.
source=os.path.join(OUT,'../PixelKnightTest/render_knight.py')
shared=types.ModuleType('army_shading');shared.__file__=source
exec(compile(open(source,encoding='utf-8').read(),source,'exec'),shared.__dict__)
shared.PALETTE.update({'rook':['542028','A63B38','EA6D59'],'bishop':['211541','483184','8A72C4'],
 'queen':['450F36','941C61','DF5694'],'pawn':['092C3D','126077','36A0AE']})

def empty(name,parent=None):
 o=bpy.data.objects.new(name,None);scene.collection.objects.link(o);o.parent=parent;return o
def box(name,p,size,mat,parent,bevel=.035):
 bpy.ops.mesh.primitive_cube_add(size=1,location=p);o=bpy.context.object;o.name=name;o.dimensions=size
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.parent=parent;o.data.materials.append(mat)
 if bevel:
  mod=o.modifiers.new('Single plane bevel','BEVEL');mod.width=bevel;mod.segments=1
  bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
 return o
def link(name,a,b,r1,r2,mat,parent,n=8):
 delta=Vector(b)-Vector(a)
 bpy.ops.mesh.primitive_cone_add(vertices=n,radius1=r1,radius2=r2,depth=delta.length,location=(Vector(a)+Vector(b))*.5)
 o=bpy.context.object;o.name=name;o.rotation_euler=delta.to_track_quat('Z','Y').to_euler();o.parent=parent;o.data.materials.append(mat);return o
def plate(name,pts,depth,mat,parent):
 n=len(pts);verts=pts+[(x,y+depth,z) for x,y,z in pts]
 faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 d=bpy.data.meshes.new(name);d.from_pydata(verts,[],faces);d.update()
 o=bpy.data.objects.new(name,d);scene.collection.objects.link(o);o.parent=parent;d.materials.append(mat);return o
def cape(parent,color,w,top,bottom):
 plate('Back mantle',[(-w*.62,.26,top),(w*.62,.26,top),(w,.34,bottom),(.12,.39,bottom-.10),(-w,.34,bottom)],.065,color,parent)
 plate('Back center fold',[(-.07,.414,top-.14),(.07,.414,top-.14),(.15,.43,bottom-.06),(-.03,.43,bottom-.1)],.015,mats['gold'],parent)

def character(kind):
 root=empty(kind.title());body=empty('Torso',root);head=empty('Head',root)
 left=empty('LeftArm',root);right=empty('RightArm',root)
 color=mats[kind];dark=mats['navy'];gold=mats['gold'];ivory=mats['ivory']
 w={'rook':.48,'bishop':.30,'queen':.31,'pawn':.25}[kind]
 hip={'rook':.83,'bishop':.86,'queen':.95,'pawn':.55}[kind]
 shoulder={'rook':1.62,'bishop':1.63,'queen':1.70,'pawn':1.18}[kind]
 for side in [-1,1]:
  leg=empty(('Left' if side<0 else 'Right')+'Leg',root);x=side*w*.49
  box('Boot',(x,-.09,.15),(.28 if kind=='rook' else .22,.38,.27),dark,leg)
  link('Leg armor',(x,0,.24),(x,0,hip),.12,.13,color,leg)
  box('Knee',(x,-.13,hip*.55),(.23,.12,.20),gold if kind=='rook' else color,leg)
 link('Torso shell',(0,0,hip),(0,0,shoulder),w*.75,w,color,body)
 box('Belt',(0,-.02,hip+.1),(w*1.65,.44,.12),gold,body)
 box('Buckle',(0,-.265,hip+.1),(.16,.07,.16),ivory,body)
 for side,arm in [(-1,left),(1,right)]:
  x=side*(w+.12)
  link('Upper arm',(x,0,shoulder-.04),(x+side*.06,-.03,shoulder-.36),.15,.18,color,arm)
  link('Forearm',(x+side*.06,-.03,shoulder-.36),(x+side*.03,-.12,shoulder-.61),.14,.12,dark,arm)
  box('Gauntlet',(x+side*.03,-.14,shoulder-.61),(.24,.26,.25),color,arm)
 neck=shoulder+.08
 if kind=='rook':
  box('Great helm',(0,0,neck+.34),(.68,.55,.62),color,head)
  box('Visor',(0,-.296,neck+.38),(.55,.045,.10),dark,head,.008)
  box('Nasal bar',(0,-.33,neck+.30),(.08,.035,.24),gold,head,.005)
  for x in [-.26,0,.26]:box('Tower battlement',(x,0,neck+.75),(.17,.51,.22),color,head,.015)
  for side,arm in [(-1,left),(1,right)]:box('Heavy pauldron',(side*.60,0,shoulder),(.51,.54,.44),color,arm,.09)
  box('Chest plate',(0,-.29,shoulder-.30),(.68,.15,.48),color,body,.08)
  box('Chest band',(0,-.38,shoulder-.23),(.64,.04,.07),gold,body,.01)
  plate('Tower shield',[(-1.02,-.34,1.52),(-.56,-.34,1.52),(-.48,-.34,.77),(-.78,-.34,.55),(-1.06,-.34,.77)],.15,dark,left)
  box('Shield boss',(-.78,-.43,1.06),(.25,.05,.31),gold,left)
  link('Hammer haft',(.70,-.19,.49),(.70,-.19,1.73),.055,.055,gold,right)
  box('Hammer head',(.70,-.19,1.78),(.63,.38,.36),ivory,right)
  cape(body,color,.48,shoulder,.66)
 elif kind=='bishop':
  link('Long robe',(0,0,.23),(0,0,shoulder-.1),.43,.29,color,body)
  plate('Front stole',[(-.18,-.30,shoulder),(.18,-.30,shoulder),(.25,-.44,.37),(-.25,-.44,.37)],.04,ivory,body)
  plate('Gold stole center',[(-.04,-.455,.4),(.04,-.455,.4),(.05,-.35,shoulder),(-.05,-.35,shoulder)],.015,gold,body)
  box('Hood shadow',(0,0,neck+.20),(.43,.40,.43),dark,head,.09)
  for x in [-.09,.09]:box('Eye',(x,-.218,neck+.24),(.055,.02,.045),ivory,head,.002)
  plate('Mitre',[(-.30,-.18,neck+.29),(.30,-.18,neck+.29),(.26,-.18,neck+.74),(0,-.18,neck+1.02),(-.26,-.18,neck+.74)],.39,color,head)
  plate('Mitre split',[(-.035,-.20,neck+.47),(.035,-.20,neck+.50),(.10,-.20,neck+.91),(.03,-.20,neck+.99)],.015,dark,head)
  link('Staff',(.63,-.14,.08),(.63,-.14,2.40),.045,.045,gold,right)
  link('Staff jewel',(.63,-.14,2.38),(.63,-.14,2.65),.13,0,ivory,right,4)
  cape(body,color,.35,shoulder+.08,.38)
 elif kind=='queen':
  link('Armored skirt',(0,0,.66),(0,0,hip+.14),.37,.24,color,body)
  plate('Ivory breastplate',[(-.25,-.28,shoulder),(.25,-.28,shoulder),(.19,-.32,shoulder-.36),(0,-.35,shoulder-.49),(-.19,-.32,shoulder-.36)],.05,ivory,body)
  box('Helmet',(0,0,neck+.23),(.44,.43,.48),color,head,.07)
  box('Visor',(0,-.235,neck+.27),(.34,.025,.10),dark,head,.006)
  link('Crown rim',(0,0,neck+.43),(0,0,neck+.54),.29,.29,gold,head)
  for x,y,z in [(-.23,-.11,.80),(0,-.20,.94),(.23,-.11,.80),(-.19,.17,.76),(.19,.17,.76)]:
   link('Crown point',(x,y,neck+.50),(x,y,neck+z),.085,0,gold,head,4)
  cape(body,color,.42,shoulder+.05,.48)
  link('Rapier',(.56,-.20,.64),(.88,-.20,2.18),.032,0,ivory,right,4)
  box('Rapier guard',(.58,-.20,.81),(.32,.16,.08),gold,right)
 else:
  box('Scout hood',(0,.025,neck+.17),(.50,.46,.49),color,head,.10)
  box('Face shadow',(0,-.23,neck+.16),(.34,.035,.23),dark,head,.035)
  for x in [-.09,.09]:box('Eye',(x,-.256,neck+.19),(.05,.02,.04),ivory,head,.002)
  box('Scarf',(0,-.07,neck-.09),(.54,.43,.16),color,body)
  plate('Scarf tail',[(.21,.29,neck),(.38,.29,neck-.04),(.49,.31,.57),(.27,.31,.71)],.04,color,body)
  link('Dagger',(.41,-.22,.66),(.56,-.22,1.12),.065,0,ivory,right,4)
  box('Belt pouch',(-.24,-.24,.68),(.22,.16,.24),gold,body)
 return root

def build_all():
 global scene,mats
 scene=bpy.data.scenes.new('Pixel Army Originals');bpy.context.window.scene=scene
 mats={key:shared.toon(key) for key in shared.PALETTE}
 scene.render.engine='BLENDER_EEVEE';scene.eevee.taa_render_samples=16
 scene.render.film_transparent=True;scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
 scene.render.resolution_percentage=100;scene.render.filter_size=.1
 scene.view_settings.view_transform='Standard';scene.view_settings.look='None'
 world=bpy.data.worlds.new('Army ambient');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[1].default_value=.25;scene.world=world
 light=bpy.data.lights.new('Army key','SUN');light.energy=2
 sun=bpy.data.objects.new('Army key',light);scene.collection.objects.link(sun)
 sun.rotation_euler=(Vector((0,0,1))-Vector((-3,4,6))).to_track_quat('-Z','Y').to_euler()
 data=bpy.data.cameras.new('Matched knight camera');cam=bpy.data.objects.new('Matched knight camera',data);scene.collection.objects.link(cam);scene.camera=cam
 data.type='ORTHO';data.ortho_scale=4.2
 cam.location=(0,7,6.14);cam.rotation_euler=(Vector((0,0,1.42))-cam.location).to_track_quat('-Z','Y').to_euler()
 roots={kind:character(kind) for kind in ['rook','bishop','queen','pawn']}
 for kind,root in roots.items():
  for other,r in roots.items():
   for o in [r]+list(r.children_recursive):o.hide_render=other!=kind
  for facing in (['front'] if kind=='pawn' else ['front','back']):
   root.rotation_euler.z=math.pi if facing=='front' else 0
   for size in [48,64,96]:
    folder=os.path.join(OUT,'renders',str(size));os.makedirs(folder,exist_ok=True)
    scene.render.resolution_x=size;scene.render.resolution_y=size
    scene.render.filepath=os.path.join(folder,kind+'-'+facing+'.png');bpy.ops.render.render(write_still=True)
  root.rotation_euler.z=0
 for root in roots.values():
  for o in [root]+list(root.children_recursive):o.hide_render=False
 bpy.data.libraries.write(os.path.join(OUT,'pixel-army.blend'),{scene},fake_user=True,compress=True)
 with open(os.path.join(OUT,'palette.json'),'w') as f:json.dump(shared.PALETTE,f,indent=2)
 print('Created four editable characters and seven views at 48/64/96 px.')
