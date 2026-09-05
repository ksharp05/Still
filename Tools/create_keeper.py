"""STILL / The Keeper: original stylized character, pose rig and Unity export."""
import bpy, math, os, json
from mathutils import Vector
ROOT=r'D:/Work/UnityProjects/Still'
OUT=os.path.join(ROOT,'ArtSource/Characters/Keeper')
os.makedirs(OUT,exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
parts=[]
def material(name,col,metal=0,rough=.5,emit=0):
 m=bpy.data.materials.new(name); m.diffuse_color=(*col,1); m.use_nodes=True
 p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*col,1); p.inputs['Metallic'].default_value=metal; p.inputs['Roughness'].default_value=rough
 p.inputs['Emission Color'].default_value=(*col,1); p.inputs['Emission Strength'].default_value=emit
 return m
armor=material('Armor',(.055,.10,.14),.65,.32)
edge=material('Edge',(.64,.72,.69),.45,.31)
cloth=material('Cloth',(.025,.045,.065),0,.88)
core=material('Core',(.05,.75,.94),.25,.3,2.8)
def finish(o,name,mat,bone='spine',bevel=0):
 o.name=name; o.data.materials.append(mat)
 if bevel:
  b=o.modifiers.new('Edge highlights','BEVEL'); b.width=bevel; b.segments=2
  bpy.context.view_layer.objects.active=o; bpy.ops.object.modifier_apply(modifier=b.name)
 o.vertex_groups.new(name=bone).add(list(range(len(o.data.vertices))),1,'REPLACE')
 parts.append(o); return o

def loft(name,rings,mat,bone='spine',n=10):
 # ring tuples: z, x radius, y radius, x offset, y offset
 v=[(cx+rx*math.cos(2*math.pi*i/n),cy+ry*math.sin(2*math.pi*i/n),z) for z,rx,ry,cx,cy in rings for i in range(n)]
 f=[tuple(range(n-1,-1,-1))]
 for j in range(len(rings)-1):
  for i in range(n): f.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
 f.append(tuple((len(rings)-1)*n+i for i in range(n)))
 mesh=bpy.data.meshes.new(name); mesh.from_pydata(v,[],f); mesh.update()
 o=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(o)
 return finish(o,name,mat,bone)

def ell(name,pos,scale,mat,bone='spine'):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=8,location=pos); o=bpy.context.object; o.scale=scale
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 return finish(o,name,mat,bone)

def bar(name,a,b,r,mat,bone='spine',r2=None):
 d=Vector(b)-Vector(a)
 bpy.ops.mesh.primitive_cone_add(vertices=10,radius1=r,radius2=r if r2 is None else r2,depth=d.length,location=(Vector(a)+Vector(b))/2)
 o=bpy.context.object; o.rotation_euler=d.to_track_quat('Z','Y').to_euler()
 bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
 return finish(o,name,mat,bone)

def plate(name,coords,depth,mat,bone='spine'):
 # Front-facing contour in x/z, extruded behind its varying front y.
 v=list(coords)+[(x,y-depth,z) for x,y,z in coords]; n=len(coords)
 f=[tuple(range(n)),tuple(range(2*n-1,n-1,-1))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 mesh=bpy.data.meshes.new(name); mesh.from_pydata(v,[],f); mesh.update(); o=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(o)
 return finish(o,name,mat,bone,.008)

# Lean, human proportions; overlapping shell armor over a dark flexible undersuit.
loft('Undersuit',[(.95,.17,.11,0,0),(1.12,.16,.105,0,0),(1.40,.245,.145,0,0),(1.48,.20,.12,0,0)],cloth)
loft('Cuirass',[(1.09,.155,.115,0,0),(1.20,.19,.15,0,.01),(1.40,.255,.17,0,0),(1.47,.18,.13,0,0)],armor)
for s in [-1,1]:
 plate('Split ivory breastplate',[(s*.025,.181,1.41),(s*.22,.13,1.39),(s*.19,.162,1.24),(s*.035,.177,1.18)],.035,edge)
bar('Sternum light',(0,.195,1.24),(0,.195,1.39),.018,core)
loft('Waist belt',[(1.0,.185,.135,0,0),(1.06,.185,.135,0,0)],armor,'pelvis',12)
ell('Belt clasp',(0,.145,1.035),(.048,.025,.043),edge,'pelvis')
ell('Belt core',(0,.174,1.035),(.018,.012,.023),core,'pelvis')
for s in [-1,1]:
 tag='L' if s<0 else 'R'; thigh='thigh.'+tag; shin='shin.'+tag; arm='arm.'+tag; fore='forearm.'+tag
 hip=(s*.12,0,.99); knee=(s*.15,.005,.57); ankle=(s*.16,-.015,.17)
 bar('Flexible thigh',hip,knee,.087,cloth,thigh,.075)
 loft('Thigh shell',[(.63,.08,.082,s*.147,.035),(.86,.10,.105,s*.13,.02),(.94,.077,.08,s*.12,0)],armor,thigh)
 ell('Knee joint',knee,(.079,.077,.075),cloth,shin)
 plate('Knee shield',[(s*.15-.07,.105,.63),(s*.15+.07,.105,.63),(s*.15+.058,.12,.55),(s*.15,.14,.51),(s*.15-.058,.12,.55)],.04,edge,shin)
 bar('Shin undersuit',knee,ankle,.066,cloth,shin)
 loft('Tapered greave',[(.17,.07,.077,s*.16,0),(.25,.079,.09,s*.16,.018),(.49,.067,.082,s*.15,.024)],edge,shin)
 bar('Greave inlay',(s*.16,.109,.25),(s*.15,.109,.45),.010,armor,shin)
 loft('Foot',[(.035,.092,.16,s*.16,.07),(.105,.09,.16,s*.16,.07),(.18,.068,.083,s*.16,-.005)],armor,'foot.'+tag)
 shoulder=(s*.27,0,1.40); elbow=(s*.34,.01,1.15); wrist=(s*.37,.045,.93)
 ell('Shoulder joint',shoulder,(.10,.105,.10),cloth,arm)
 bar('Upper arm',shoulder,elbow,.070,cloth,arm,.057)
 ell('Elbow',elbow,(.065,.066,.062),armor,fore)
 bar('Bracer',wrist,(s*.345,.017,1.115),.075,edge,fore,.091)
 bar('Bracer seam',(s*.37,.12,.975),(s*.35,.12,1.085),.012,armor,fore)
 ell('Glove',(s*.375,.05,.875),(.061,.062,.085),armor,'hand.'+tag)
 # Asymmetrical layered shoulder plates rather than identical box shoulders.
 if s<0:
  for j in range(3):
   loft('Overlapping pauldron',[(1.36-j*.055,.12+j*.006,.145,s*(.28+j*.034),0),(1.47-j*.065,.09,.105,s*(.27+j*.034),0)],edge,arm)
 else:
  loft('Light shoulder',[(1.34,.105,.135,s*.28,0),(1.46,.08,.105,s*.27,0)],armor,arm)

# Helmet: faceted crown, recessed face, sloped cheek guards, thin split visor.
bar('Neck',(0,0,1.45),(0,0,1.59),.068,cloth,'head')
loft('Raised gorget',[(1.44,.13,.12,0,0),(1.51,.145,.125,0,0),(1.55,.11,.09,0,0)],edge)
loft('Helmet crown',[(1.60,.115,.107,0,0),(1.75,.148,.127,0,0),(1.87,.11,.10,0,-.008),(1.915,.025,.035,0,-.01)],edge,'head',12)
plate('Recessed face',[(-.104,.126,1.79),(.104,.126,1.79),(.088,.145,1.65),(0,.16,1.59),(-.088,.145,1.65)],.018,cloth,'head')
for s in [-1,1]:
 plate('Swept cheek',[(s*.115,.13,1.78),(s*.14,.065,1.74),(s*.09,.13,1.60),(s*.023,.169,1.585),(s*.054,.159,1.68)],.025,armor,'head')
 bar('Visor slit',(s*.014,.151,1.743),(s*.088,.141,1.759),.009,core,'head')
plate('Brow crest',[(-.12,.135,1.79),(0,.16,1.82),(.12,.135,1.79),(.085,.155,1.768),(0,.18,1.79),(-.085,.155,1.768)],.026,armor,'head')
bar('Crown filament',(0,.09,1.85),(0,-.005,1.916),.008,core,'head')

# Sculpted mantle and split fabric tails, with actual folds in their surface.
loft('Shoulder mantle',[(1.37,.29,.17,0,-.045),(1.47,.24,.145,0,-.025),(1.53,.13,.105,0,0)],cloth)
def cape_panel(name,x0,x1,zbottom):
 verts=[]; cols=10; rows=10
 for row in range(rows):
  t=row/(rows-1)
  for col in range(cols):
   u=col/(cols-1); x=(x0+(x1-x0)*u)*(.64+.36*t)
   z=1.45*(1-t)+(zbottom+.055*math.cos(u*math.pi*3))*t
   y=-.15-.15*t-.055*math.sin(u*math.pi*4)*t-.075*t*t
   verts.append((x,y,z))
 faces=[]
 for j in range(rows-1):
  for i in range(cols-1):
   a=j*cols+i; faces.append((a,a+1,a+1+cols,a+cols))
 mesh=bpy.data.meshes.new(name); mesh.from_pydata(verts,[],faces); mesh.update(); o=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(o)
 finish(o,name,cloth)
 sol=o.modifiers.new('Fabric thickness','SOLIDIFY'); sol.thickness=.012
 bpy.context.view_layer.objects.active=o; bpy.ops.object.modifier_apply(modifier=sol.name)
 for p in o.data.polygons:p.use_smooth=True
cape_panel('Left split cloak',-.32,-.024,.54)
cape_panel('Right split cloak',.024,.32,.63)
# Readable circular time device on the back.
ell('Chronometer housing',(0,-.195,1.28),(.104,.035,.104),armor)
for i in range(12):
 a=2*math.pi*i/12; b=a+.24
 bar('Clock rune',(math.sin(a)*.077,-.236,1.28+math.cos(a)*.077),(math.sin(b)*.077,-.236,1.28+math.cos(b)*.077),.008,core)
bar('Clock hand',(0,-.242,1.28),(.036,-.242,1.326),.009,edge)

# Poseable rigid armor rig: export-ready named chains, no pretending this is finished locomotion.
bpy.ops.object.armature_add(); rig=bpy.context.object; rig.name='Keeper_Rig'; bpy.ops.object.mode_set(mode='EDIT'); rig.data.edit_bones.remove(rig.data.edit_bones[0])
def bone(name,a,b,parent=None):
 e=rig.data.edit_bones.new(name);e.head=a;e.tail=b
 if parent:e.parent=rig.data.edit_bones[parent]
bone('root',(0,0,0),(0,0,.18));bone('pelvis',(0,0,.90),(0,0,1.06),'root');bone('spine',(0,0,1.06),(0,0,1.46),'pelvis');bone('head',(0,0,1.46),(0,0,1.91),'spine')
for s in [-1,1]:
 t='L' if s<0 else 'R'
 bone('thigh.'+t,(s*.12,0,.99),(s*.15,.005,.57),'pelvis');bone('shin.'+t,(s*.15,.005,.57),(s*.16,-.015,.17),'thigh.'+t);bone('foot.'+t,(s*.16,-.015,.17),(s*.16,.2,.08),'shin.'+t)
 bone('arm.'+t,(s*.27,0,1.40),(s*.34,.01,1.15),'spine');bone('forearm.'+t,(s*.34,.01,1.15),(s*.37,.045,.93),'arm.'+t);bone('hand.'+t,(s*.37,.045,.93),(s*.375,.05,.82),'forearm.'+t)
bpy.ops.object.mode_set(mode='OBJECT'); rig.show_in_front=True
# Save editable individual plates and their weights before export grouping.
for o in parts:
 mod=o.modifiers.new('Pose rig','ARMATURE');mod.object=rig;o.parent=rig

# Render studio, kept separate from character export.
scene=bpy.context.scene
scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.05,.07,.10,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.35
stage=material('Studio charcoal',(.025,.035,.05),.15,.6)
bpy.ops.mesh.primitive_cylinder_add(vertices=96,radius=.72,depth=.10,location=(0,0,-.05));bpy.context.object.name='Display plinth';bpy.context.object.data.materials.append(stage)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.106));bpy.context.object.data.materials.append(stage)
for name,pos,power,size,col in [('Key',(2,3,4),400,3,(.82,.92,1)),('Rim',(-2,-2,3),500,2,(.12,.65,1)),('Warm fill',(-3,2,1.8),180,2,(1,.78,.55))]:
 bpy.ops.object.light_add(type='AREA',location=pos);o=bpy.context.object;o.name=name;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.data.color=col;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(2.7,4.6,2.6));cam=bpy.context.object;scene.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=2.7
cam.rotation_euler=(Vector((0,0,.98))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.engine='CYCLES';scene.cycles.samples=48
scene.render.resolution_x=1100;scene.render.resolution_y=1300;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
# Save source with editable parts and complete lighting before merging the runtime copy.
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'Keeper.blend'))
for name,pos,scale in [('hero',(2.7,4.6,2.6),2.7),('back',(-2.7,-4.6,2.7),2.7),('game-angle',(2.2,3.4,5.6),2.8)]:
 cam.location=pos;cam.data.ortho_scale=scale;cam.rotation_euler=(Vector((0,0,.98))-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=os.path.join(OUT,name+'.png');bpy.ops.render.render(write_still=True)
# Join by material, preserving weights; apply a single armature modifier to each group.
groups={m:[o for o in parts if o.data.materials[0]==m] for m in (armor,edge,cloth,core)}
merged=[]
for m,objs in groups.items():
 bpy.ops.object.select_all(action='DESELECT')
 for o in objs:
  for mod in list(o.modifiers):o.modifiers.remove(mod)
  o.select_set(True)
 bpy.context.view_layer.objects.active=objs[0];bpy.ops.object.join();o=bpy.context.object;o.name=m.name
 mod=o.modifiers.new('Pose rig','ARMATURE');mod.object=rig;merged.append(o)
bpy.ops.object.empty_add(location=(0,.5,1)); facing=bpy.context.object; facing.name='FacingMarker'; facing.parent=rig
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);facing.select_set(True)
for o in merged:o.select_set(True)
bpy.context.view_layer.objects.active=rig
fbx=os.path.join(ROOT,'Assets/_Project/Resources/Characters/Wanderer.fbx')
bpy.ops.export_scene.fbx(filepath=fbx,use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,use_mesh_modifiers=True)
triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in merged)
json.dump({'name':'Keeper','triangles':triangles,'renderers':len(merged),'bones':len(rig.data.bones),'height_m':1.925,'animation':'pose rig only; no locomotion clips'},open(os.path.join(OUT,'asset-report.json'),'w'),indent=2)
print('KEEPER_EXPORT_OK',triangles)
