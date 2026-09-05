"""Full character study built on Blender Studio's CC0 anatomical mesh.
Body-derived garment shells, editable tailoring, strand hair and mechanical accessories.
Run with Blender 5.2 --background --disable-autoexec --python this_file.
"""
import bpy, math, os, glob, random, json
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
from mathutils.noise import noise_vector
R=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT=os.path.join(R,'ArtSource/Characters/HeroSculpt');os.makedirs(OUT,exist_ok=True)
random.seed(31)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
source=glob.glob(os.path.join(R,'ArtSource/ThirdParty/HumanBaseMeshes/v1.4.1/**/*.blend'),recursive=True)[0]
with bpy.data.libraries.load(source,link=False) as (a,b):b.collections=['Body Male - Realistic']
anatomy=b.collections[0];bpy.context.scene.collection.children.link(anatomy);anatomy.name='01 | CC0 source anatomy'
bpy.context.view_layer.update();body=next(o for o in anatomy.objects if len(o.data.vertices)>5000)
offset=Vector((-body.matrix_world.translation.x,0,.006))
matrices={o:o.matrix_world.copy() for o in anatomy.all_objects}
for o in list(matrices):
 o.parent=None;o.matrix_world=Matrix.Translation(offset)@matrices[o];o.hide_set(False);o.hide_render=False
bpy.context.view_layer.update()
for m in body.modifiers:
 if m.type=='MULTIRES':m.levels=1;m.render_levels=2
bpy.context.view_layer.update()
deps=bpy.context.evaluated_depsgraph_get();evaluated=body.evaluated_get(deps)
base=bpy.data.meshes.new_from_object(evaluated);base.transform(body.matrix_world)
coords=[v.co.copy() for v in base.vertices];normals=[v.normal.copy() for v in base.vertices]
faces=[list(p.vertices) for p in base.polygons]
bvh=BVHTree.FromPolygons(coords,faces)
body.hide_render=True;body.hide_set(True)
wardrobe=bpy.data.collections.new('02 | Tailored garments');bpy.context.scene.collection.children.link(wardrobe)
details=bpy.data.collections.new('03 | Hair and face');bpy.context.scene.collection.children.link(details)
hardware=bpy.data.collections.new('04 | Clockwork and blade');bpy.context.scene.collection.children.link(hardware)
studio=bpy.data.collections.new('05 | Presentation');bpy.context.scene.collection.children.link(studio)

def mat(name,c,rough=.5,metal=0,texture=0):
 m=bpy.data.materials.new(name);m.diffuse_color=(*c,1);m.use_nodes=True;n=m.node_tree.nodes;p=n.get('Principled BSDF');p.inputs['Base Color'].default_value=(*c,1);p.inputs['Roughness'].default_value=rough;p.inputs['Metallic'].default_value=metal
 if texture:
  tex=n.new('ShaderNodeTexNoise');tex.inputs['Scale'].default_value=texture;tex.inputs['Detail'].default_value=3
  ramp=n.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].position=.22;ramp.color_ramp.elements[0].color=(*(x*.65 for x in c),1);ramp.color_ramp.elements[1].position=.8;ramp.color_ramp.elements[1].color=(*(min(x*1.15,1) for x in c),1)
  m.node_tree.links.new(tex.outputs['Fac'],ramp.inputs[0]);m.node_tree.links.new(ramp.outputs[0],p.inputs['Base Color'])
  bump=n.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.2;bump.inputs['Distance'].default_value=.0007;m.node_tree.links.new(tex.outputs['Fac'],bump.inputs['Height']);m.node_tree.links.new(bump.outputs[0],p.inputs['Normal'])
 return m
skin=mat('Warm skin / subtle pores',(.31,.16,.09),.51,texture=650)
skin.node_tree.nodes.get('Bump').inputs['Distance'].default_value=.00012
skin.node_tree.nodes.get('Principled BSDF').inputs['Subsurface Weight'].default_value=.075
coat=mat('Midnight petrol wool',(.016,.037,.041),.66,texture=350)
leather=mat('Oxblood leather',(.028,.017,.014),.42,texture=150)
pants=mat('Charcoal twill',(.026,.026,.022),.75,texture=420)
red=mat('Aged crimson silk',(.19,.017,.021),.5,texture=230)
ivory=mat('Warm ivory jacquard',(.47,.42,.32),.8,texture=340)
gold=mat('Worn brass',(.36,.215,.075),.3,.8,70)
steel=mat('Blackened steel',(.052,.068,.07),.29,.8,140)
edge=mat('Polished steel',(.37,.45,.46),.22,.88)
hair=mat('Espresso hair',(.012,.008,.005),.62)
hairlight=mat('Silver forelock',(.31,.27,.20),.42)
white=mat('Eye sclera',(.27,.285,.25),.28)
iris=mat('Blue green iris',(.009,.033,.033),.34)
pupil=mat('Pupil',(.001,.002,.002),.12)
glow=mat('Chronometer luminous enamel',(.025,.36,.4),.24,.3)
p=glow.node_tree.nodes.get('Principled BSDF');p.inputs['Emission Color'].default_value=(.025,.45,.52,1);p.inputs['Emission Strength'].default_value=1.6

def mesh(name,v,f,material,collection=wardrobe,sub=0,thick=0):
 me=bpy.data.meshes.new(name);me.from_pydata(v,[],f);me.update();o=bpy.data.objects.new(name,me);collection.objects.link(o);me.materials.append(material)
 for p in me.polygons:p.use_smooth=True
 if sub:m=o.modifiers.new('Smooth tailoring','SUBSURF');m.levels=sub;m.render_levels=sub
 if thick:m=o.modifiers.new('Fabric thickness','SOLIDIFY');m.thickness=thick
 return o
def surface(name,predicate,material,expand=0,fold=0,collection=wardrobe):
 selected=[f for f in faces if predicate(sum((coords[i] for i in f),Vector())/len(f))]
 ids=sorted({i for f in selected for i in f});lookup={i:j for j,i in enumerate(ids)};verts=[]
 for i in ids:
  p=coords[i].copy();wave=fold*(.6*math.sin(p.z*100+p.x*38)+.4*math.sin(p.z*167-p.y*33))
  p+=normals[i]*(expand+wave);verts.append(p)
 o=mesh(name,verts,[[lookup[i] for i in f] for f in selected],material,collection,1,.002 if expand else 0)
 if expand:
  import bmesh
  bm=bmesh.new();bm.from_mesh(o.data)
  boundary=[v for v in bm.verts if v.is_boundary]
  for iteration in range(6):bmesh.ops.smooth_vert(bm,verts=boundary,factor=.6,use_axis_x=True,use_axis_y=True,use_axis_z=True)
  bm.to_mesh(o.data);bm.free()
 return o
def path(name,pts,r,material=gold,collection=hardware,radii=None):
 cu=bpy.data.curves.new(name,'CURVE');cu.dimensions='3D';cu.resolution_u=12;cu.bevel_depth=r;cu.bevel_resolution=3
 sp=cu.splines.new('BEZIER');sp.bezier_points.add(len(pts)-1)
 for i,(p,co) in enumerate(zip(sp.bezier_points,pts)):p.co=co;p.handle_left_type=p.handle_right_type='AUTO';p.radius=radii[i] if radii else 1
 o=bpy.data.objects.new(name,cu);collection.objects.link(o);cu.materials.append(material);return o
def ell(name,pos,scale,material,collection=hardware):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=32,ring_count=20,location=pos);o=bpy.context.object;o.name=name;o.scale=scale
 for c in list(o.users_collection):c.objects.unlink(o)
 collection.objects.link(o);o.data.materials.append(material)
 for p in o.data.polygons:p.use_smooth=True
 return o
def ring(name,pos,r,t,material=gold,axis='Y'):
 bpy.ops.mesh.primitive_torus_add(major_segments=64,minor_segments=10,major_radius=r,minor_radius=t,location=pos);o=bpy.context.object;o.name=name
 for c in list(o.users_collection):c.objects.unlink(o)
 hardware.objects.link(o);o.data.materials.append(material)
 if axis=='Y':o.rotation_euler.x=math.pi/2
 if axis=='X':o.rotation_euler.y=math.pi/2
 for p in o.data.polygons:p.use_smooth=True
 return o
def loft(name,rings,material,n=64,sub=2):
 v=[(x+rx*math.cos(2*math.pi*i/n),y+ry*math.sin(2*math.pi*i/n),z) for z,rx,ry,x,y in rings for i in range(n)]
 f=[(j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i) for j in range(len(rings)-1) for i in range(n)]
 return mesh(name,v,f,material,wardrobe,sub,.004)

# Actual anatomy, with covered source polygons omitted to prevent poke-through.
surface('Exposed face, neck and hands',lambda p:p.z>1.405 or (p.z>1.33 and abs(p.x)<.078) or (abs(p.x)>.342 and p.z<.90),skin,collection=details)
for o in list(anatomy.objects):
 if o!=body:
  o.data.materials.clear();o.data.materials.append(white)
  for p in o.data.polygons:p.use_smooth=True
for s in [-1,1]:
 ell('Iris', (s*.0329,-.1345,1.5797),(.0055,.0018,.0055),iris,details)
 ell('Pupil',(s*.0329,-.1361,1.5797),(.0024,.0009,.0026),pupil,details)
surface('Fitted trousers',lambda p:.13<p.z<.918 and abs(p.x)<.285,pants,.008,.0025)
for s in [-1,1]:
 # Source feet point forward (-Y), slightly outward.
 o=ell('Stacked leather sole',(s*.184,-.018,.024),(.064,.136,.023),leather,wardrobe);o.rotation_euler.z=-s*.18
 o=ell('Rounded boot vamp',(s*.184,-.018,.068),(.062,.129,.055),leather,wardrobe);o.rotation_euler.z=-s*.18
 loft('Boot ankle and shaft',[(.065,.047,.069,s*.17,.036),(.10,.049,.061,s*.17,.048),(.15,.047,.049,s*.17,.053),(.21,.05,.051,s*.17,.049),(.27,.058,.055,s*.17,.045),(.285,.06,.057,s*.17,.045)],leather,sub=2)
 for z in [.19,.245]:
  loft('Boot cuff band',[(z-.013,.058,.055,s*.169,.045),(z+.013,.058,.055,s*.169,.045)],leather,n=40,sub=1)
surface('Tailored coat sleeves',lambda p:(1.035 if p.x>0 else .89)<p.z<1.44 and abs(p.x)>.165,coat,.026,.0017)
surface('Gauntlet leather underglove',lambda p:.87<p.z<1.06 and p.x>.29,leather,.013,0)
surface('Shoulder yoke',lambda p:1.31<p.z<1.415 and abs(p.x)>.061,coat,.024,0)
o=loft('Tailored coat bodice',[(.915,.155,.126,0,-.012),(.96,.16,.131,0,-.012),(1.10,.158,.133,0,-.02),(1.24,.202,.145,0,-.018),(1.32,.21,.133,0,-.012),(1.38,.11,.092,0,-.007),(1.40,.076,.074,0,-.007)],coat)
# Open the jacket front to expose the waistcoat instead of intersecting it.
import bmesh
bm=bmesh.new();bm.from_mesh(o.data)
bmesh.ops.delete(bm,geom=[f for f in bm.faces if f.calc_center_median().y<-.05 and abs(f.calc_center_median().x)<.09],context='FACES')
bm.to_mesh(o.data);bm.free()
# Front insert and lapels sit outside the source chest.
v=[]
for z,w,y in [(.955,.098,-.150),(1.01,.10,-.161),(1.11,.113,-.170),(1.22,.133,-.181),(1.32,.103,-.161),(1.365,.06,-.115)]:
 for i in range(9):
  u=i/8*2-1;v.append((u*w,y+.025*u*u,z))
mesh('Waistcoat front',v,[(j*9+i,j*9+i+1,(j+1)*9+i+1,(j+1)*9+i) for j in range(5) for i in range(8)],leather,sub=2,thick=.003)
for z,y in [(1,-.162),(1.055,-.168),(1.11,-.174),(1.165,-.181),(1.22,-.184)]:ell('Waistcoat button',(0,y,z),(.004,.002,.004),gold)
for s in [-1,1]:
 pts=[(s*.065,-.156,.97),(s*.084,-.177,1.12),(s*.153,-.167,1.30),(s*.07,-.103,1.395)]
 v=[]
 for i,p in enumerate(pts):v.extend([p,(p[0]+s*(.032 if i<2 else .045),p[1]-.008,p[2]+.012)])
 mesh('Rolled coat lapel',v,[(i*2,i*2+1,i*2+3,i*2+2) for i in range(3)],coat,sub=1,thick=.004)
 path('Lapel edge piping',pts,.0018,gold,wardrobe)
 # Standing collar with swept points.
 mesh('Raised collar',[(s*.056,-.083,1.33),(s*.11,-.068,1.37),(s*.066,.063,1.36),(s*.051,-.071,1.445),(s*.127,-.06,1.455),(s*.072,.078,1.412)],[(0,1,4,3),(1,2,5,4)],coat,sub=0,thick=.005)
 path('Collar brass seam',[(s*.051,-.071,1.445),(s*.127,-.06,1.455),(s*.072,.078,1.412)],.0018,gold,wardrobe)
# Open long coat, continuous tailored fabric panels with gravity-oriented folds.
for s in [-1,1]:
 v=[];rows=38;cols=32
 for j in range(rows):
  t=j/(rows-1);z=.945-.76*t
  for i in range(cols):
   u=i/(cols-1);a=-1.18+u*2.75;rx=.158+.095*t;ry=.123+.065*t
   wave=.008*math.sin(u*29+t*3)*t+.006*math.sin(u*53-t*7)*t
   v.append((s*(rx+wave)*math.cos(a)+s*.06*t*t,(ry+wave)*math.sin(a)+.04*t,z+.03*math.sin(u*7)*t+.035*t*t*math.cos(u*11)))
 f=[(j*cols+i,j*cols+i+1,(j+1)*cols+i+1,(j+1)*cols+i) for j in range(rows-1) for i in range(cols-1)]
 o=mesh('Long split coat tail',v,f,coat,sub=1,thick=.004);o.data.materials.append(red);o.modifiers[-1].material_offset=1
 path('Coat tail edge braid',[v[j*cols] for j in range(0,rows,3)]+[v[(rows-1)*cols]],.0024,gold,wardrobe)
 path('Coat hem braid',v[(rows-1)*cols:],.002,gold,wardrobe)
# Belt and hanging chain.
loft('Leather waist belt',[(.912,.155,.131,0,-.015),(.95,.155,.131,0,-.015)],leather,sub=1)
path('Belt buckle',[(-.025,-.154,.913),(.026,-.154,.913),(.026,-.154,.951),(-.025,-.154,.951),(-.025,-.154,.913)],.004)
for j in range(24):
 t=j/23;ring('Waist chain link',(.075+.102*t,-.142-.012*math.sin(t*math.pi),.936-.13*math.sin(t*math.pi)),.006,.001,'Y' if False else gold,axis='Y' if j%2 else 'X')
# Scarf and narrow hanging ends.
loft('Crimson wrapped scarf',[(1.336,.069,.068,0,-.014),(1.367,.075,.071,0,-.016),(1.391,.067,.067,0,-.014)],red)
for j in range(4):path('Scarf fold',[(-.06,-.035,1.344+j*.009),(-.025,-.088,1.339+j*.01),(.043,-.074,1.35+j*.008),(.064,-.02,1.353+j*.008)],.0028,red,wardrobe)
for side in [0,1]:
 v=[]
 for j in range(30):
  t=j/29
  for i in range(7):
   u=i/6-.5;v.append((-.02-.14*t+side*.05+u*(.052-.022*t),-.185-.065*math.sin(t*2.3)+.006*math.sin(t*13+u*5),1.36-(.37+side*.09)*t+.014*math.sin(t*12+u*2)))
 mesh('Hanging silk scarf',v,[(j*7+i,j*7+i+1,(j+1)*7+i+1,(j+1)*7+i) for j in range(29) for i in range(6)],red,sub=2,thick=.002)
# Shoulder cape: cloth arcs from clasp over left shoulder, then falls down the back.
v=[];rows=40;cols=35
for j in range(rows):
 t=j/(rows-1)
 for i in range(cols):
  u=i/(cols-1);a=-.30+u*2.70
  radius=.09+.24*(1-math.exp(-t*4));x=.105+radius*math.cos(a);y=.045+radius*.72*math.sin(a)+.13*t
  z=1.397-.065*t-.60*t*t+.016*math.sin(u*35+t*6)*(t+.15)
  v.append((x,y,z))
f=[(j*cols+i,j*cols+i+1,(j+1)*cols+i+1,(j+1)*cols+i) for j in range(rows-1) for i in range(cols-1)]
mesh('Ivory shoulder half cape',v,f,ivory,sub=2,thick=.003)
for i in [2,cols-3]:path('Cape woven border',[v[j*cols+i] for j in range(rows)],.003,leather,wardrobe)
path('Cape hem embroidery',v[(rows-2)*cols:(rows-1)*cols],.0025,gold,wardrobe)
# Chronometer clasp, layered mechanical face and radial engraving.
def clockface(name,c,r):
 x,y,z=c;ell(name+' housing',c,(r,.012,r),steel)
 for ratio,width in [(1,.004),(.9,.0018),(.7,.002),(.29,.002)]:ring(name+' machined bezel',(x,y-.014,z),r*ratio,width)
 for j in range(60):
  a=2*math.pi*j/60;inner=.74 if j%5==0 else .81
  path(name+' minute index',[(x+r*inner*math.sin(a),y-.018,z+r*inner*math.cos(a)),(x+r*.87*math.sin(a),y-.018,z+r*.87*math.cos(a))],.0008,glow if name=='Gauntlet' else gold)
 for a,l in [(.75,.60),(-1.9,.42)]:path(name+' hand',[(x,y-.022,z),(x+r*l*math.sin(a),y-.022,z+r*l*math.cos(a))],.0015,glow if name=='Gauntlet' else gold)
 ell(name+' spindle',(x,y-.024,z),(.004,.003,.004),gold)
 for j in range(8):
  a=j*math.pi/4;ell(name+' screw',(x+r*.965*math.cos(a),y-.017,z+r*.965*math.sin(a)),(.0018,.0015,.0018),steel)
clockface('Shoulder clasp',(.17,-.158,1.34),.04)
# Gauntlet follows left forearm rather than a floating sphere.
for k in range(5):
 z=.895+k*.029;x=.36-(z-.895)*.40
 loft('Articulated gauntlet cuff',[(z-.014,.054,.065,x,-.047),(z+.014,.053,.064,x-.008,-.047)],steel,n=48,sub=1)
 for zz in [z-.011,z+.01]:ring('Gauntlet brass rim',(x,-.047,zz),.057,.0025,gold,'Z')
clockface('Gauntlet',(.337,-.096,.979),.047)
for i in [-1,0,1]:path('Gauntlet exposed conduit',[(.32+i*.018,-.066,1.04),(.35+i*.018,-.088,.91),(.39+i*.012,-.075,.85)],.003, gold if i else glow)
# Right-hand duelling blade, with narrow forged cross section.
a=Vector((-.405,-.071,.79));tip=Vector((-.69,-.17,.055));axis=(tip-a).normalized();side=Vector((axis.z,0,-axis.x)).normalized();basept=a+axis*.085
path('Sword leather grip',[a-axis*.04,a+axis*.067],.014,leather)
path('Swept sword guard',[basept-side*.10-axis*.02,basept-side*.045,basept,basept+side*.08-axis*.022,basept+side*.065-axis*.065],.005,gold)
verts=[]
for p,w in [(basept,.022),(basept+axis*.10,.024),(tip-axis*.08,.009),(tip,0)]:verts.extend([p+side*w,p+Vector((0,-.005,0)),p-side*w,p+Vector((0,.005,0))])
o=mesh('Forged duelling blade',verts,[(j*4+i,j*4+(i+1)%4,(j+1)*4+(i+1)%4,(j+1)*4+i) for j in range(3) for i in range(4)],edge,hardware)
for p in o.data.polygons:p.use_smooth=False
path('Blade temporal fuller',[basept+axis*.08+Vector((0,-.006,0)),tip-axis*.09+Vector((0,-.006,0))],.0012,glow)

# Hair follows head surface. Thousands of tapered strands share a small number of curves objects.
def strands_object(name,strands,material,radius):
 cu=bpy.data.curves.new(name,'CURVE');cu.dimensions='3D';cu.resolution_u=2;cu.bevel_depth=radius;cu.bevel_resolution=2
 for pts in strands:
  sp=cu.splines.new('POLY');sp.points.add(len(pts)-1)
  for j,(p,co) in enumerate(zip(sp.points,pts)):p.co=(*co,1);p.radius=max(.06,(1-j/(len(pts)-1))**.6)
 o=bpy.data.objects.new(name,cu);details.objects.link(o);cu.materials.append(material);return o
def scalp(th,ph,extra=0):
 center=Vector((0,-.042,1.608));d=Vector((math.sin(th)*math.cos(ph),math.sin(th)*math.sin(ph),math.cos(th)))
 hit=bvh.ray_cast(center+d*.24,-d,.3)
 return hit[0]+hit[1]*extra if hit[0] is not None else center+d*.083
v=[];nr=24;nc=80
for j in range(nr):
 for i in range(nc):
  ph=2*math.pi*i/nc;limit=1.70+.50*math.sin(ph);th=.02+(limit-.02)*j/(nr-1);v.append(scalp(th,ph,.0015))
mesh('Hair underlayer',v,[(j*nc+i,j*nc+(i+1)%nc,(j+1)*nc+(i+1)%nc,(j+1)*nc+i) for j in range(nr-1) for i in range(nc)],hair,details,1)
dark=[];silver=[]
for k in range(500):
 ph=random.uniform(-math.pi,math.pi);limit=1.70+.50*math.sin(ph);th=random.uniform(.15,limit)
 root=scalp(th,ph,.002);top=th<1.3;length=random.uniform(.04,.08) if top else random.uniform(.025,.055)
 for h in range(14):
  jitter=Vector((random.gauss(0,.002),random.gauss(0,.002),random.gauss(0,.0015)));pts=[]
  for j in range(13):
   t=j/12
   p=scalp(th+(.30 if top else .40)*t,ph+.65*t,.002+(.016 if top else .006)*math.sin(t*math.pi))+jitter
   p+=Vector((.0015*math.sin(t*7+k),.0015*math.sin(t*8+k),.001*math.sin(t*8+k)))*t;pts.append(p)
  if max((pts[j]-pts[j-1]).length for j in range(1,len(pts)))<.016:
   (silver if root.x>.012 and root.x<.033 and ph<-.65 and th<1.28 else dark).append(pts)
strands_object('Swept hair / dark strands',dark,hair,.0002);strands_object('Signature silver forelock',silver,hairlight,.00022)
def front_y(x,z):
 hit=bvh.ray_cast(Vector((x,-.5,z)),Vector((0,1,0)))
 return hit[0].y-.001 if hit[0] is not None else -.13
brows=[]
for s in [-1,1]:
 for i in range(230):
  t=random.random();x=s*(.016+.041*t);z=1.602+.006*math.sin(t*math.pi)-.009*t+random.uniform(-.002,.002);y=front_y(x,z)
  brows.append([Vector((x,y,z)),Vector((x+s*.002,y-.0006,z+.002)),Vector((x+s*.004,y,z+.003))])
strands_object('Eyebrow groom',brows,hair,.0003)
stubble=[]
for i in range(4200):
 x=random.uniform(-.061,.061);z=random.uniform(1.444,1.537)
 if z>1.483 and abs(x)<.025:continue
 y=front_y(x,z)
 if y>-.066:continue
 stubble.append([Vector((x,y,z)),Vector((x+random.uniform(-.0006,.0006),y-.00035,z-.0015))])
strands_object('Fine jaw stubble',stubble,hair,.00013)
scar=mat('Healed scar',(.34,.18,.115),.64)
pts=[]
for j in range(16):
 t=j/15;z=1.619-.069*t;x=.039+.005*math.sin(t*3.6)
 if 1.573<z<1.588:continue
 pts.append((x,front_y(x,z)-.0003,z))
# Split at the eye rather than drawing over the eyeball.
path('Brow scar',pts[:7],.00055,scar,details)
path('Cheek scar',pts[7:],.00045,scar,details)

# Studio and useful saved cameras.
stage=mat('Studio charcoal',(.025,.031,.035),.73)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.003));o=bpy.context.object;o.name='Ground';o.data.materials.append(stage)
scene=bpy.context.scene;scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.08,.095,.11,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.35
for name,loc,power,size,color in [('Warm key',(-2,-3,3.8),280,2.5,(1,.85,.70)),('Cool fill',(2,-2,2.4),110,2,(.66,.8,1)),('Rim',(1,1.4,3.0),390,1.5,(.65,.86,1))]:
 bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.name=name;o.data.energy=power;o.data.size=size;o.data.color=color;o.rotation_euler=(Vector((0,0,1.1))-o.location).to_track_quat('-Z','Y').to_euler()
def camera(name,pos,target,scale):
 bpy.ops.object.camera_add(location=pos);o=bpy.context.object;o.name=name;o.data.type='ORTHO';o.data.ortho_scale=scale;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler();return o
hero_cam=camera('Full character', (2,-6,2.35),(0,0,.875),2.02)
portrait_cam=camera('Portrait',(.8,-3,1.89),(0,-.02,1.49),.52)
back_cam=camera('Back',(-2,6,2.4),(0,0,.87),2.05)
scene.camera=hero_cam;scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True;scene.render.resolution_x=1100;scene.render.resolution_y=1500;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX'
notes=bpy.data.texts.new('SOURCE AND SCOPE');notes.write('STILL hero study. Body and eyes: Blender Studio/community CC0 Human Base Meshes v1.4.1. Garment shells derived from source anatomy; additional tailoring, hair curves and hardware authored by script. Design study, not production-ready: no rig, baked maps or LODs. Reference is concept-reference.png in sibling LastSecond folder. Do not confuse concept with render. Workflow references and license recorded in README.')
for area in bpy.context.screen.areas:
 if area.type=='VIEW_3D':area.spaces.active.region_3d.view_distance=2.5;area.spaces.active.region_3d.view_location=Vector((0,0,.9))
scene.render.filepath=os.path.join(OUT,'hero-full.png');bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'HeroTailored.blend'))
for cam,name in [(hero_cam,'hero-full'),(portrait_cam,'hero-portrait'),(back_cam,'hero-back')]:
 scene.camera=cam;scene.render.filepath=os.path.join(OUT,name+'.png');bpy.ops.render.render(write_still=True)
print('HERO_TAILORED_COMPLETE')
