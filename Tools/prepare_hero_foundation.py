"""Prepare an attributed anatomical sculpt foundation from Blender's official CC0 bundle."""
import bpy,glob,os,json
from mathutils import Vector, Matrix
ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT=os.path.join(ROOT,'ArtSource/Characters/HeroSculpt');os.makedirs(OUT,exist_ok=True)
source=glob.glob(os.path.join(ROOT,'ArtSource/ThirdParty/HumanBaseMeshes/v1.4.1/**/*.blend'),recursive=True)[0]
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
with bpy.data.libraries.load(source,link=False) as (src,dst):dst.collections=['Head (Sculpting) - Realistic']
collection=dst.collections[0];bpy.context.scene.collection.children.link(collection);collection.name='SCULPT FOUNDATION | Blender Studio CC0'
objects=list(collection.all_objects)
bpy.context.view_layer.update()
main=next(o for o in objects if o.name=='GEO-head_sculpting_realistic')
# Recenter source while preserving the artist-authored anatomy and multires detail.
world_bounds=[o.matrix_world@Vector(p) for o in objects for p in o.bound_box]
center=Vector(((min(p.x for p in world_bounds)+max(p.x for p in world_bounds))/2,0,min(p.z for p in world_bounds)))
matrices={o:o.matrix_world.copy() for o in objects}
for o in objects:
 o.parent=None;o.matrix_world=Matrix.Translation(-center)@matrices[o];o.hide_set(False);o.hide_render=False
bpy.context.view_layer.update()

def mat(name,c,rough):
 m=bpy.data.materials.new(name);m.diffuse_color=(*c,1);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*c,1);p.inputs['Roughness'].default_value=rough;return m
clay=mat('Neutral sculpt clay',(.30,.285,.26),.7)
eyes=mat('Eyes / clay study',(.41,.40,.37),.5)
for o in objects:
 o.data.materials.clear();o.data.materials.append(clay if o==main else eyes)
 for p in o.data.polygons:p.use_smooth=True
 if o==main:
  for mod in o.modifiers:
   if mod.type=='MULTIRES':mod.levels=mod.total_levels;mod.sculpt_levels=mod.total_levels;mod.render_levels=mod.total_levels
  mod=o.modifiers.new('Surface preview','SUBSURF');mod.levels=2;mod.render_levels=2
 else:
  mod=o.modifiers.new('Eye preview','SUBSURF');mod.levels=2
scene=bpy.context.scene
scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.09,.10,.12,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.25
for name,loc,power,size in [('Key',(.5,-.7,.8),30,.7),('Fill',(-.6,-.4,.35),8,.5),('Rim',(.15,.45,.6),35,.45)]:
 bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.name=name;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector((0,0,.22))-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(.6,-1,.5));cam=bpy.context.object;scene.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=.48;cam.rotation_euler=(Vector((0,-.025,.225))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.engine='CYCLES';scene.cycles.samples=48;scene.cycles.use_denoising=True
scene.render.resolution_x=1100;scene.render.resolution_y=1250;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX';scene.render.filepath=os.path.join(OUT,'anatomy-foundation.png')
notes=bpy.data.texts.new('READ ME — source and scope');notes.write('Anatomical starting mesh from Blender Studio and community Human Base Meshes v1.4.1, CC0. Source: https://www.blender.org/download/demo-files/ . This is an imported foundation, not an original completed hero. The original topology and sculpt detail are retained. Next: hero likeness sculpt, hair grooming, garment construction. Rejected procedural models were not reused.')
bpy.ops.object.select_all(action='DESELECT');main.select_set(True);bpy.context.view_layer.objects.active=main
for area in bpy.context.screen.areas:
 if area.type=='VIEW_3D':
  area.spaces.active.region_3d.view_distance=.65;area.spaces.active.region_3d.view_location=Vector((0,0,.23))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'HeroFoundation.blend'))
bpy.ops.render.render(write_still=True)
print('ANATOMY_FOUNDATION_READY')
