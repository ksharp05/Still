"""Refine the existing hero study; preserve earlier versions for comparison."""
import bpy,bmesh,math,os,json,random
from mathutils import Vector
from mathutils.bvhtree import BVHTree
R=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT=os.path.join(R,'ArtSource/Characters/HeroSculpt')
bpy.ops.wm.open_mainfile(filepath=os.path.join(OUT,'HeroTailored.blend'))
wardrobe=bpy.data.collections['02 | Tailored garments'];hardware=bpy.data.collections['04 | Clockwork and blade'];details=bpy.data.collections['03 | Hair and face'];studio=bpy.data.collections['05 | Presentation']
coat=bpy.data.materials['Midnight petrol wool'];leather=bpy.data.materials['Oxblood leather'];gold=bpy.data.materials['Worn brass'];ivory=bpy.data.materials['Warm ivory jacquard'];skin=bpy.data.materials['Warm skin / subtle pores'];red=bpy.data.materials['Aged crimson silk']

def remove_prefixes(prefixes):
 for o in list(bpy.data.objects):
  if any(o.name.startswith(p) for p in prefixes):bpy.data.objects.remove(o,do_unlink=True)
def mesh(name,v,f,m,col=wardrobe,sub=0,thick=0):
 me=bpy.data.meshes.new(name);me.from_pydata(v,[],f);me.update();o=bpy.data.objects.new(name,me);col.objects.link(o);me.materials.append(m)
 for p in me.polygons:p.use_smooth=True
 if sub:mod=o.modifiers.new('Tailoring subdivision','SUBSURF');mod.levels=sub;mod.render_levels=sub
 if thick:mod=o.modifiers.new('Garment thickness','SOLIDIFY');mod.thickness=thick
 return o
def path(name,pts,r,m=gold,col=hardware):
 cu=bpy.data.curves.new(name,'CURVE');cu.dimensions='3D';cu.resolution_u=8;cu.bevel_depth=r;cu.bevel_resolution=3
 sp=cu.splines.new('POLY');sp.points.add(len(pts)-1)
 for p,co in zip(sp.points,pts):p.co=(*co,1)
 o=bpy.data.objects.new(name,cu);col.objects.link(o);cu.materials.append(m);return o
def loft(name,rings,m,n=64,sub=2,cap=False):
 v=[(x+rx*math.cos(i*2*math.pi/n),y+ry*math.sin(i*2*math.pi/n),z) for z,rx,ry,x,y in rings for i in range(n)]
 f=[(j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i) for j in range(len(rings)-1) for i in range(n)]
 if cap:f+=[tuple(range(n-1,-1,-1)),tuple((len(rings)-1)*n+i for i in range(n))]
 return mesh(name,v,f,m,sub=sub)

# Keep the same material palette, but reduce plastic reflections on fabrics.
for m in [coat,ivory,red,bpy.data.materials['Charcoal twill']]:
 p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Specular IOR Level'].default_value=.16;p.inputs['Sheen Weight'].default_value=.25;p.inputs['Roughness'].default_value=.82 if m!=red else .64
 bump=m.node_tree.nodes.get('Bump')
 if bump:bump.inputs['Distance'].default_value=.00025
coat.node_tree.nodes.get('Color Ramp').color_ramp.elements[0].color=(.006,.012,.018,1)
coat.node_tree.nodes.get('Color Ramp').color_ramp.elements[1].color=(.022,.041,.053,1)
leather.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.58

# Replace overlapping sleeve/yoke/torso pieces with one continuous body-derived shell.
remove_prefixes(['Tailored coat sleeves','Shoulder yoke','Tailored coat bodice','Raised collar','Collar brass seam','Rolled coat lapel','Lapel edge piping'])
body=bpy.data.objects['GEO-body_male_realistic'];body.hide_set(False);bpy.context.view_layer.update()
deps=bpy.context.evaluated_depsgraph_get();src=bpy.data.meshes.new_from_object(body.evaluated_get(deps));src.transform(body.matrix_world)
verts=[v.co.copy() for v in src.vertices]
selected=[]
for f in src.polygons:
 c=sum((verts[i] for i in f.vertices),Vector())/len(f.vertices)
 if c.z<(1.037 if c.x>.29 else .90) or c.z>1.44:continue
 if c.z>1.365 and abs(c.x)<.065:continue
 if c.y<-.058 and abs(c.x)<.085:continue
 selected.append(list(f.vertices))
ids=sorted({i for f in selected for i in f});lookup={i:j for j,i in enumerate(ids)}
jacket=mesh('Continuous tailored jacket',[verts[i]+src.vertices[i].normal*.027 for i in ids],[[lookup[i] for i in f] for f in selected],coat)
bm=bmesh.new();bm.from_mesh(jacket.data)
for k in range(16):bmesh.ops.smooth_vert(bm,verts=list(bm.verts),factor=.5,use_axis_x=True,use_axis_y=True,use_axis_z=True)
bm.to_mesh(jacket.data);bm.free();jacket.data.update()
mod=jacket.modifiers.new('Smooth silhouette','SUBSURF');mod.levels=1
mod=jacket.modifiers.new('Wool thickness','SOLIDIFY');mod.thickness=.003
body.hide_set(True)

# A continuous stand collar with a deliberate open front.
v=[];N=50
for j in range(4):
 t=j/3
 for i in range(N):
  a=-.76+(math.pi+1.52)*i/(N-1)
  v.append(((.078+.011*t)*math.cos(a),.006+(.079+.008*t)*math.sin(a),1.35+.084*t+.02*t*abs(math.cos(a))))
f=[(j*N+i,j*N+i+1,(j+1)*N+i+1,(j+1)*N+i) for j in range(3) for i in range(N-1)]
mesh('Continuous standing collar',v,f,coat,sub=1,thick=.004)
path('Collar stitched edge',v[3*N:],.0016,gold,wardrobe)
# A shirt under the open coat closes the neckline and supports the scarf knot.
loft('Ivory shirt collar',[(1.315,.113,.104,0,-.018),(1.342,.099,.098,0,-.018),(1.385,.076,.087,0,-.016),(1.409,.069,.080,0,-.009)],ivory)
remove_prefixes(['Scarf fold'])
wrapped=bpy.data.objects['Crimson wrapped scarf'];wrapped.location.z=.016;wrapped.location.y=-.023
loft('Tied scarf knot',[(1.341,.021,.012,0,-.154),(1.348,.027,.019,0,-.153),(1.366,.028,.020,0,-.147),(1.386,.021,.014,0,-.13)],red,cap=True)

# Lapels hug the front, with rounded edges and small stitch geometry.
for s in [-1,1]:
 rows=[(.966,.069,-.15,.028),(1.035,.083,-.167,.028),(1.13,.105,-.17,.030),(1.27,.162,-.15,.038),(1.345,.091,-.117,.04),(1.405,.074,-.091,.030)]
 v=[]
 for z,x,y,w in rows:
  for i in range(5):
   t=i/4;v.append((s*(x+w*t),y-.008*math.sin(t*math.pi),z))
 mesh('Fitted lapel',v,[(j*5+i,j*5+i+1,(j+1)*5+i+1,(j+1)*5+i) for j in range(len(rows)-1) for i in range(4)],coat,sub=1,thick=.004)
 path('Lapel edge stitch',[v[j*5] for j in range(len(rows))],.0014,gold,wardrobe)

# Replace the separated oval toe/shaft components with continuous boot surfaces.
remove_prefixes(['Stacked leather sole','Rounded boot vamp','Boot ankle and shaft','Boot cuff band'])
for s in [-1,1]:
 x=s*.176
 loft('Stitched boot',[(.033,.058,.128,x,-.018),(.045,.061,.13,x,-.018),(.060,.061,.129,x,-.016),(.084,.058,.109,x,-.006),(.11,.05,.072,x,.02),(.14,.044,.046,x,.047),(.19,.047,.047,x,.049),(.265,.057,.052,x,.042),(.28,.058,.053,x,.042)],leather,cap=True)
 loft('Boot welt and sole',[(.01,.056,.126,x,-.017),(.018,.062,.133,x,-.017),(.035,.062,.133,x,-.017),(.042,.06,.132,x,-.017)],leather,sub=1,cap=True)
 for z in [.175,.245]:
  loft('Boot strap',[(z-.012,.050 if z<.2 else .059,.05 if z<.2 else .054,x,.045),(z+.012,.050 if z<.2 else .059,.05 if z<.2 else .054,x,.045)],leather,sub=1)
  path('Boot strap buckle',[(x-.012,-.008,z-.009),(x+.012,-.008,z-.009),(x+.012,-.008,z+.009),(x-.012,-.008,z+.009),(x-.012,-.008,z-.009)],.0015)
 path('Boot vamp seam',[(x+.053*math.cos(a),-.018+.112*math.sin(a),.066) for a in [math.pi+i*math.pi/32 for i in range(33)]],.0009,gold,wardrobe)

# Cape depth folds: vary depth along the fabric, not just the hem height.
cape=bpy.data.objects['Ivory shoulder half cape']
for v in cape.data.vertices:
 p=v.co;t=max(0,min(1,(1.395-p.z)/.60));p.y+=.018*math.sin((p.x-.10)*43+t*2.5)*math.sin(t*1.8)
cape.data.update();bpy.context.view_layer.update()
deformed=cape.evaluated_get(bpy.context.evaluated_depsgraph_get());me=bpy.data.meshes.new_from_object(deformed)
bvh_cape=BVHTree.FromPolygons([v.co for v in me.vertices],[list(f.vertices) for f in me.polygons])
def on_cape(x,z):
 hit=bvh_cape.ray_cast(Vector((x,1,z)),Vector((0,-1,0)))
 return hit[0]+Vector((0,.003,0)) if hit[0] is not None else None
for radius in [.060,.069]:
 pts=[on_cape(.11+radius*math.cos(a),1.045+radius*math.sin(a)) for a in [i*2*math.pi/100 for i in range(101)]]
 if all(p is not None for p in pts):path('Cape embroidered time seal',pts,.0014,gold,wardrobe)
for line in [[(-.029,.037),(.029,.037),(-.029,-.037),(.029,-.037),(-.029,.037)],[(-.034,.043),(.034,.043)],[(-.034,-.043),(.034,-.043)]]:
 samples=[Vector(a).lerp(Vector(b),j/20) for a,b in zip(line,line[1:]) for j in range(21)]
 pts=[on_cape(.11+x,1.045+z) for x,z in samples]
 if all(p is not None for p in pts):path('Cape hourglass embroidery',pts,.0015,gold,wardrobe)
for o in list(wardrobe.objects):
 if o.name.startswith(('Cape woven border','Cape hem embroidery')):
  for sp in o.data.splines:
   for p in sp.bezier_points:
    hit=bvh_cape.find_nearest(p.co)
    if hit[0] is not None:p.co=hit[0]+hit[1]*.003

# Less startled eyes; brow and jaw changes remain localized to the anatomical mesh.
face=bpy.data.objects['Exposed face, neck and hands']
def gauss(v,c,w):return math.exp(-((v-c)/w)**2)
for v in face.data.vertices:
 x,y,z=v.co
 if z<1.44:continue
 front=max(0,min(1,(-y-.05)/.06));ax=abs(x)
 # Broad, angular lower jaw and cheekbone planes.
 v.co.x*=1+.055*gauss(z,1.477,.026)*front
 v.co.y-=.0035*gauss(ax,.048,.023)*gauss(z,1.555,.021)*front
 v.co.y-=.002*gauss(ax,.025,.029)*gauss(z,1.605,.009)*front
 v.co.z-=.0024*gauss(ax,.033,.020)*gauss(z,1.588,.006)*front
face.data.update()
for o in [x for x in details.objects if x.name.startswith('Eyebrow groom')]:
 for sp in o.data.splines:
  for p in sp.points:
   x,y,z,w=p.co;p.co.z-=.004*(1-min(abs(x)/.064,1));p.co.y-=.0018
random.seed(9)
groom=bpy.data.objects['Fine jaw stubble'].data
for sp in list(groom.splines):
 x,y,z,w=sp.points[0].co
 density=max(0,min(1,(1.535-z)/.012))
 if z>1.483:density*=max(0,min(1,(abs(x)-.025)/.015))
 if random.random()>density:groom.splines.remove(sp)
for mname in ['Eye sclera','Blue green iris','Pupil']:
 p=bpy.data.materials[mname].node_tree.nodes.get('Principled BSDF');p.inputs['Specular IOR Level'].default_value=.25;p.inputs['Roughness'].default_value=.36

# Keep skin color variation subtle and tied to anatomical regions.
attr=face.data.color_attributes.new(name='Skin tones',type='FLOAT_COLOR',domain='POINT')
for v,c in zip(face.data.vertices,attr.data):
 x,y,z=v.co;front=max(0,min(1,(-y-.07)/.055));lip=gauss(x,0,.030)*gauss(z,1.501,.008)*front
 beard=gauss(z,1.472,.037)*front
 c.color=(.31+.065*lip-.025*beard,.16-.035*lip-.014*beard,.09-.016*lip-.009*beard,1)
nodes=skin.node_tree.nodes;col=nodes.new('ShaderNodeVertexColor');col.layer_name=attr.name
skin.node_tree.links.new(col.outputs['Color'],nodes.get('Principled BSDF').inputs['Base Color'])

# Point the existing portrait camera closer to eye level; retain neutral full/back views.
scene=bpy.context.scene
portrait=bpy.data.objects['Portrait'];portrait.location=(.63,-3,1.71);portrait.rotation_euler=(Vector((0,-.02,1.5))-portrait.location).to_track_quat('-Z','Y').to_euler()
for o in bpy.data.objects:
 if o.type in {'CAMERA','LIGHT'} or o.name=='Ground':
  for c in list(o.users_collection):c.objects.unlink(o)
  studio.objects.link(o)
bpy.data.objects['Warm key'].data.energy=235;bpy.data.objects['Cool fill'].data.energy=65;bpy.data.objects['Rim'].data.energy=260
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.23
scene.cycles.samples=48;scene.render.resolution_x=1200;scene.render.resolution_y=1600
scene.camera=bpy.data.objects['Full character'];scene.render.filepath=os.path.join(OUT,'refined-full.png')
bpy.ops.object.select_all(action='DESELECT');jacket.select_set(True);bpy.context.view_layer.objects.active=jacket
notes=bpy.data.texts.new('REFINEMENT NOTES');notes.write('STILL stylized hero refinement. Based on HeroTailored.blend, retains attributed Blender Studio/community CC0 anatomy. Continuous jacket/collar and boots replace overlapping shells; cape folds and embroidery, more restrained cloth shading, facial/brow edits and skin variation. This is an editable art study. No production rig, baked textures or game integration in this pass.')
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'HeroRefined.blend'))
for name,cam in [('refined-full','Full character'),('refined-portrait','Portrait'),('refined-back','Back')]:
 scene.camera=bpy.data.objects[cam];scene.render.filepath=os.path.join(OUT,name+'.png');bpy.ops.render.render(write_still=True)
print('HERO_REFINEMENT_RENDERED')
