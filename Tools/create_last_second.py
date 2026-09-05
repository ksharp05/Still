"""STILL: The Last Second. High-resolution hero design study; not a game-ready asset."""
import bpy, math, os, random
from mathutils import Vector
R=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT=os.path.join(R,'ArtSource','Characters','LastSecond')
os.makedirs(OUT,exist_ok=True)
random.seed(12)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
hero=bpy.data.collections.new('HERO | The Last Second');bpy.context.scene.collection.children.link(hero)
def move(o):
 for c in list(o.users_collection):c.objects.unlink(o)
 hero.objects.link(o)
 return o

def mat(name,col,metal=0,rough=.45,noise=0,subsurface=0,emit=0):
 m=bpy.data.materials.new(name);m.diffuse_color=(*col,1);m.use_nodes=True
 nd=m.node_tree.nodes;p=nd.get('Principled BSDF');p.inputs['Base Color'].default_value=(*col,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
 p.inputs['Subsurface Weight'].default_value=subsurface
 p.inputs['Emission Color'].default_value=(*col,1);p.inputs['Emission Strength'].default_value=emit
 if noise:
  tex=nd.new('ShaderNodeTexNoise');tex.inputs['Scale'].default_value=180 if metal==0 else 95;tex.inputs['Detail'].default_value=2
  bump=nd.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.18;bump.inputs['Distance'].default_value=noise
  m.node_tree.links.new(tex.outputs['Fac'],bump.inputs['Height']);m.node_tree.links.new(bump.outputs['Normal'],p.inputs['Normal'])
 return m
skin=mat('01 | warm ochre skin',(.34,.165,.088),rough=.57,noise=.001,subsurface=.08)
lips=mat('Lips',(.29,.105,.075),rough=.48,subsurface=.06)
coat=mat('02 | midnight teal wool',(.006,.023,.026),rough=.88,noise=.0018)
lining=mat('03 | oxblood silk lining',(.12,.006,.013),rough=.68,noise=.0006)
ivory=mat('04 | aged ivory twill',(.65,.56,.40),rough=.74,noise=.0015)
leather=mat('05 | oiled black leather',(.010,.014,.017),rough=.53,noise=.001)
gold=mat('06 | patinated brass',(.48,.275,.088),metal=.8,rough=.28,noise=.0006)
darkgold=mat('Old brass recesses',(.145,.071,.023),metal=.8,rough=.4)
steel=mat('07 | tempered dark steel',(.075,.13,.16),metal=.92,rough=.2)
edge=mat('Honed silver edge',(.53,.64,.67),metal=.92,rough=.18)
glow=mat('08 | temporal enamel',(.035,.65,.68),metal=.35,rough=.18,emit=1.4)
hair=mat('09 | espresso hair',(.009,.006,.004),rough=.56,noise=.0005)
hairlight=mat('Ivory forelock',(.63,.55,.40),rough=.38)
eye=mat('Eye sclera',(.55,.52,.43),rough=.25)
iris=mat('Iris teal',(.018,.20,.18),rough=.22)
pupil=mat('Pupil',(.002,.003,.003),rough=.1)

def smooth(o):
 for f in o.data.polygons:f.use_smooth=True
 return o

def ell(name,pos,scale,m,segments=40,rings=24):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,location=pos)
 o=move(bpy.context.object);o.name=name;o.scale=scale;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(m);smooth(o);return o

def mesh(name,v,f,m,sub=0,solid=0):
 me=bpy.data.meshes.new(name);me.from_pydata(v,[],f);me.update();o=bpy.data.objects.new(name,me);hero.objects.link(o);o.data.materials.append(m);smooth(o)
 if solid:mod=o.modifiers.new('Tailored thickness','SOLIDIFY');mod.thickness=solid
 if sub:mod=o.modifiers.new('Sculpted surface','SUBSURF');mod.levels=sub;mod.render_levels=sub
 return o

def loft(name,rings,m,n=40,sub=2):
 v=[(cx+rx*math.cos(2*math.pi*i/n),cy+ry*math.sin(2*math.pi*i/n),z) for z,rx,ry,cx,cy in rings for i in range(n)]
 f=[tuple(range(n-1,-1,-1))]
 for j in range(len(rings)-1):
  for i in range(n):f.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
 f.append(tuple((len(rings)-1)*n+i for i in range(n)))
 return mesh(name,v,f,m,sub)

def path(name,pts,r,m,radii=None):
 cu=bpy.data.curves.new(name,'CURVE');cu.dimensions='3D';cu.resolution_u=16;cu.bevel_depth=r;cu.bevel_resolution=4
 sp=cu.splines.new('BEZIER');sp.bezier_points.add(len(pts)-1)
 for i,(p,co) in enumerate(zip(sp.bezier_points,pts)):
  p.co=co;p.handle_left_type=p.handle_right_type='AUTO';p.radius=radii[i] if radii else 1
 o=bpy.data.objects.new(name,cu);hero.objects.link(o);cu.materials.append(m);return o

def bar(name,a,b,r,m,r2=None):
 d=Vector(b)-Vector(a)
 bpy.ops.mesh.primitive_cone_add(vertices=40,radius1=r,radius2=r if r2 is None else r2,depth=d.length,location=(Vector(a)+Vector(b))/2)
 o=move(bpy.context.object);o.name=name;o.rotation_euler=d.to_track_quat('Z','Y').to_euler();o.data.materials.append(m)
 be=o.modifiers.new('Machined edge','BEVEL');be.width=.005;be.segments=3;smooth(o);return o

def ring(name,pos,major,minor,m,axis='Y'):
 bpy.ops.mesh.primitive_torus_add(major_segments=64,minor_segments=12,location=pos,major_radius=major,minor_radius=minor)
 o=move(bpy.context.object);o.name=name;o.data.materials.append(m)
 if axis=='Y':o.rotation_euler.x=math.pi/2
 if axis=='X':o.rotation_euler.y=math.pi/2
 return smooth(o)

# Anatomy: an elongated, athletic adult silhouette under tailored clothes.
loft('Torso / fitted waistcoat',[(1.03,.16,.115,0,0),(1.11,.19,.13,0,0),(1.24,.17,.11,0,0),(1.40,.205,.135,0,0),(1.58,.26,.145,0,0),(1.64,.215,.125,0,0)],leather)
loft('Coat bodice',[(1.08,.192,.144,0,-.018),(1.18,.185,.13,0,-.018),(1.30,.18,.123,0,-.02),(1.47,.235,.145,0,-.02),(1.62,.28,.15,0,-.02),(1.66,.22,.12,0,-.02)],coat)
# Exposed central waistcoat insert, softly curved.
mesh('Waistcoat front',[(-.10,.148,1.14),(.10,.148,1.14),(-.085,.154,1.45),(.085,.154,1.45),(-.08,.13,1.61),(.08,.13,1.61)],[(0,1,3,2),(2,3,5,4)],leather,2,.008)
for z in [1.20,1.28,1.36,1.44]:ell('Brass waistcoat button',(0,.161,z),(.010,.006,.010),gold,20,12)
# Deep V lapels and edge piping define the chest instead of plate armor.
for s in [-1,1]:
 pts=[(s*.095,.174,1.18),(s*.125,.182,1.39),(s*.21,.155,1.57),(s*.135,.123,1.66)]
 verts=[]
 for i,p in enumerate(pts):verts.extend([p,(p[0]+s*(.035 if i<2 else .068),p[1]-.012,p[2]+.02)])
 mesh('Rolled lapel',verts,[(i*2,i*2+1,i*2+3,i*2+2) for i in range(3)],coat,2,.014)
 path('Lapel antique piping',pts,.004,gold)

# Legs in a relaxed asymmetric stance.
for s in [-1,1]:
 x=s*.135; bias=.045 if s>0 else -.03
 loft('Tailored trousers',[(.46,.064,.071,s*.185,bias),(.61,.077,.083,s*.175,bias),(.80,.101,.098,s*.15,bias-.02),(.99,.114,.111,x,-.025),(1.08,.105,.10,x,-.01)],leather)
 path('Trouser side seam',[(s*.24,-.01,1),(s*.26,bias,.8),(s*.25,bias,.62)],.003,coat)
 loft('Tall fitted boot',[(.075,.085,.115,s*.19,bias),(.19,.071,.087,s*.19,bias),(.34,.078,.086,s*.185,bias),(.48,.09,.094,s*.18,bias),(.54,.084,.09,s*.18,bias)],leather)
 ell('Boot toe',(s*.19,bias+.10,.095),(.092,.19,.076),leather)
 ell('Boot stacked sole',(s*.19,bias+.08,.039),(.096,.202,.025),leather)
 for z in [.46,.50]:
  ring('Boot cuff stitching',(s*.18,bias,z),.088,.0028,gold,'Z')
 for j in range(5):
  z=.22+j*.041
  path('Boot crossed laces',[(s*.19-.034,bias+.082,z),(s*.19+.034,bias+.089,z+.028)],.003,coat)
  path('Boot crossed laces',[(s*.19+.034,bias+.082,z),(s*.19-.034,bias+.089,z+.028)],.003,coat)

# Belts and travelling details.
loft('Broad waist belt',[(1.075,.201,.153,0,-.006),(1.10,.201,.153,0,-.006),(1.14,.19,.145,0,-.006),(1.155,.19,.145,0,-.006)],leather,48,1)
ring('Hourglass buckle rim',(0,.169,1.115),.047,.007,gold)
path('Hourglass buckle', [(-.020,.18,1.14),(.020,.18,1.14),(-.020,.18,1.09),(.020,.18,1.09)],.005,gold)
path('Slung second belt',[(-.185,.065,1.17),(-.12,.16,1.09),(.02,.173,1.04),(.17,.072,1.015)],.019,leather)
for i in range(10):
 a=i*.14+.20
 ell('Belt rivet',(.20*math.cos(a),.153*math.sin(a),1.12),(.004,.004,.004),gold,12,8)
loft('Side satchel',[(.90,.057,.045,.22,-.01),(.92,.069,.053,.22,-.01),(1.05,.068,.053,.22,-.01),(1.075,.052,.042,.22,-.01)],leather,28,2)
ell('Pouch stud',(.22,.05,1.025),(.009,.006,.009),gold)

# Flared coat tails with sculpted fabric folds, open in front.
def tails(name,matl,yoff=0):
 n=64;rows=15;verts=[]
 # Sweep around the back from left front to right front: opening is 100 degrees.
 for j in range(rows):
  t=j/(rows-1)
  for i in range(n):
   a=math.radians(145)+(math.radians(250))*i/(n-1)
   flare=.20+.19*t+.025*math.sin(t*math.pi)
   z=1.115-.62*t + .045*t*math.sin(a*3)+.06*t*(math.cos(a)**2)
   x=flare*math.cos(a)+.05*t*t
   y=(.15+.16*t)*math.sin(a)-.035-.07*t*t+yoff
   y+=.018*t*math.sin(a*11)
   verts.append((x,y,z))
 faces=[(j*n+i,j*n+i+1,(j+1)*n+i+1,(j+1)*n+i) for j in range(rows-1) for i in range(n-1)]
 o=mesh(name,verts,faces,matl,2,.006)
 for idx in [0,n-1]:path('Coat edge braid',[verts[j*n+idx] for j in range(rows)],.0045,gold)
 path('Hem gilded braid',[verts[(rows-1)*n+i] for i in range(n)],.004,gold)
 return o
# Separate lining stays just inside the outer surface.
tails('Oxblood coat lining',lining,.007);tails('Sweeping split coat',coat)

# Arms, one gauntleted, with relaxed hands.
for s in [-1,1]:
 shoulder=(s*.275,0,1.56); elbow=(s*.35,.008,1.29);wrist=(s*.39,.09,1.07)
 bar('Sleeve upper arm',shoulder,elbow,.088,coat,.064)
ell('Right sleeve shoulder',(.278,0,1.56),(.105,.113,.113),coat)
ell('Left sleeve shoulder',(-.278,0,1.56),(.105,.113,.113),coat)
for s in [-1,1]:
 elbow=(s*.35,.008,1.29);wrist=(s*.39,.09,1.07)
 bar('Forearm sleeve',elbow,wrist,.077,coat,.057)
 bar('Cuff', (s*.385,.082,1.075),(s*.38,.077,1.12),.071,ivory)
 ell('Leather glove palm',(s*.40,.095,1.015),(.056,.039,.07),leather)
 for k in range(4):
  x=s*.40+(k-1.5)*.023
  path('Articulated gloved finger',[(x,.100,1.0),(x,.126,.97),(x,.15,.97)],.0105,leather,[1,.9,.65])
 path('Glove thumb',[(s*.35,.10,1.035),(s*.34,.13,1.006),(s*.36,.15,.99)],.015,leather,[1,.85,.65])

# Asymmetrical ivory mantle draped over the left shoulder.
verts=[];nu=24;nv=12
for j in range(nv):
 t=j/(nv-1)
 for i in range(nu):
  a=math.pi*2*i/(nu-1)
  x=-.245+(.12+.045*t)*math.cos(a)-.085*t
  y=(.105+.035*t)*math.sin(a)-.025
  z=1.66-.32*t+.04*math.cos(a)-.012*t*math.cos(a*8)
  verts.append((x,y,z))
mesh('Draped ivory shoulder mantle',verts,[(j*nu+i,j*nu+i+1,(j+1)*nu+i+1,(j+1)*nu+i) for j in range(nv-1) for i in range(nu-1)],ivory,2,.009)
path('Mantle lower braid',verts[(nv-1)*nu:],.004,gold)
ring('Mantle clasp',(-.21,.149,1.56),.037,.006,gold)
ell('Clasp enamel',(-.21,.15,1.56),(.026,.009,.026),steel)
for i in range(8):
 a=i*math.pi/4
 ell('Clasp index',(-.21+math.sin(a)*.024,.162,1.56+math.cos(a)*.024),(.003,.003,.006),gold,16,8)

# High swept collar frames the face and creates the main silhouette.
for sign in [-1,1]:
 v=[]
 for j in range(12):
  t=j/11
  for i in range(12):
   u=i/11
   ang=.22+u*1.9
   x=sign*(.12+.075*t)*math.sin(ang)
   y=(.10+.025*t)*math.cos(ang)-.018
   z=1.57+t*(.195+.045*math.sin(ang))
   v.append((x,y,z))
 mesh('Sculpted standing coat collar',v,[(j*12+i,j*12+i+1,(j+1)*12+i+1,(j+1)*12+i) for j in range(11) for i in range(11)],coat,2,.010)
 path('Collar gilt edge',v[-12:],.0025,gold)
# Embroidered vine motifs across the coat skirts, geometric and restrained.
for sign in [-1,1]:
 for row in range(6):
  z=.63+row*.062;x=sign*(.30-(z-.63)*.22);y=.052
  path('Gold leaf embroidery',[(x,y,z),(x+sign*.013,y+.004,z+.015),(x+sign*.019,y,z+.034)],.0013,gold,[.4,.8,.15])
  path('Gold leaf embroidery',[(x,y,z+.014),(x-sign*.012,y+.003,z+.027),(x-sign*.016,y,z+.037)],.0012,gold,[.3,.8,.1])

# Chronometer gauntlet on left forearm: nested rings and working-looking mechanisms.
bar('Gauntlet leather bed',(-.357,.02,1.26),(-.389,.087,1.10),.084,leather,.071)
for i in range(4):
 z=1.12+i*.038
 ring('Gauntlet segmented brass band',(-.387+i*.008,.071-i*.014,z),.079,.009,gold,'Z')
center=Vector((-.389,.158,1.205))
bar('Chronometer drum',center-Vector((0,.016,0)),center+Vector((0,.016,0)),.075,darkgold)
for rad in [.078,.065,.052]:ring('Engraved clock concentric ring',center,rad,.0045,gold)
ell('Obsidian clock face',center+Vector((0,.005,0)),(.059,.012,.059),steel)
for i in range(12):
 a=2*math.pi*i/12
 a1=center+Vector((math.sin(a)*.042,.019,math.cos(a)*.042));a2=center+Vector((math.sin(a)*.052,.019,math.cos(a)*.052))
 path('Clock hour index',[a1,a2],.0028,glow if i%3==0 else gold)
path('Clock minute hand',[center+Vector((0,.023,0)),center+Vector((.014,.023,.035))],.003,glow)
path('Clock hour hand',[center+Vector((0,.025,0)),center+Vector((-.027,.025,.007))],.003,gold)
for i in range(3):ell('Gauntlet winding crown',(-.474,.133,1.17+i*.031),(.012,.012,.012),gold,24,16)

# Neck and visible sculpted face. Smooth profile rings shape the jaw/cheek planes.
loft('Neck',[(1.62,.070,.065,0,0),(1.76,.068,.065,0,.005),(1.79,.078,.065,0,.006)],skin)
# Continuous facial surface: anatomy is displaced into the head, not assembled as tubes.
profiles=[(1.755,.053,.041,.037),(1.775,.078,.063,.022),(1.815,.108,.083,.005),(1.865,.12,.097,0),(1.925,.128,.104,-.004),(1.99,.119,.103,-.010),(2.035,.095,.089,-.018),(2.065,.048,.058,-.02)]
def gauss(x,z,cx,cz,sx,sz):return math.exp(-((x-cx)/sx)**2-((z-cz)/sz)**2)
n=192;rows=160;vv=[]
for j in range(rows):
 z=1.755+(2.065-1.755)*j/(rows-1)
 k=next((k for k in range(len(profiles)-1) if profiles[k+1][0]>=z),len(profiles)-2)
 p0,p1=profiles[k],profiles[k+1];t=(z-p0[0])/(p1[0]-p0[0])
 def interp(c):
  va=profiles[max(0,k-1)][c];vb=p0[c];vc=p1[c];vd=profiles[min(len(profiles)-1,k+2)][c]
  return .5*((2*vb)+(-va+vc)*t+(2*va-5*vb+4*vc-vd)*t*t+(-va+3*vb-3*vc+vd)*t*t*t)
 rx=interp(1);ry=interp(2);cy=interp(3)
 for i in range(n):
  a=i*math.pi*2/n;x=rx*math.cos(a);y=cy+ry*math.sin(a)
  if math.sin(a)>0:
   d=.027*gauss(x,z,0,1.894,.016,.051)+.040*gauss(x,z,0,1.860,.021,.013)
   d+=.012*gauss(x,z,0,1.821,.034,.014)+.011*gauss(x,z,0,1.785,.043,.019)
   for sign in [-1,1]:
    d+=.010*gauss(x,z,sign*.070,1.888,.028,.024)
    d-=.010*gauss(x,z,sign*.052,1.925,.028,.017)
    d+=.007*gauss(x,z,sign*.052,1.951,.035,.013)
   y+=d*math.sin(a)**4
  vv.append((x,y,z))
head=mesh('Sculpt / continuous face',vv,[(j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i) for j in range(rows-1) for i in range(n)],skin,1)
for s in [-1,1]:
 ell('Nostril',(s*.015,.126,1.852),(.0045,.002,.002),lips,24,12)
 ell('Ear',(s*.124,-.002,1.886),(.024,.020,.048),skin)
 ell('Ear inner',(s*.139,.008,1.886),(.008,.010,.029),lips)
 # Eyes set into a socket delineated by curved upper and lower lids.
 x=s*.052
 ell('Eye',(x,.090,1.923),(.025,.013,.010),eye)
 ell('Iris',(x,.106,1.923),(.0075,.003,.0075),iris,32,20)
 ell('Pupil',(x,.110,1.923),(.0038,.002,.0057),pupil,24,16)
 ell('Eye catchlight',(x-.002,.112,1.927),(.0018,.001,.0018),ivory,16,8)
 path('Upper eyelid',[(x-.027,.094,1.923),(x-.014,.103,1.931),(x+.008,.103,1.931),(x+.027,.092,1.925)],.0028,skin)
 path('Lower eyelid',[(x-.026,.093,1.923),(x,.104,1.916),(x+.025,.094,1.922)],.0023,skin)
 path('Expressive brow',[(s*.020,.107,1.943),(s*.050,.111,1.951),(s*.082,.098,1.949)],.0045,hair,[.6,1,.2])
path('Upper lip',[(-.031,.094,1.822),(-.013,.110,1.826),(0,.112,1.822),(.012,.109,1.826),(.031,.094,1.822)],.006,lips,[.1,.9,.6,.85,.1])
path('Lower lip',[(-.028,.095,1.817),(0,.110,1.813),(.027,.095,1.819)],.0065,lips,[.1,1,.1])
path('Mouth line',[(-.029,.100,1.820),(0,.115,1.819),(.029,.100,1.822)],.0017,hair)
# Short scar crossing one brow.
path('Brow scar',[(.067,.103,1.975),(.061,.114,1.952),(.055,.111,1.94)],.0025,ivory,[.25,.65,.15])

# Continuous fitted hair cap with irregular, swept sculptural locks.
verts=[];n=64;rows=20
for j in range(rows):
 t=j/(rows-1)
 for i in range(n):
  a=i*math.pi*2/n
  end=1.87-.36*math.sin(a)
  phi=.035+(end-.035)*t
  verts.append((.132*math.sin(phi)*math.cos(a),-.017+.115*math.sin(phi)*math.sin(a),1.973+.112*math.cos(phi)))
mesh('Fitted sculpted hair cap',verts,[(j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i) for j in range(rows-1) for i in range(n)],hair,2)
for i in range(28):
 u=i/27
 x=.106-.208*u
 pts=[(x,.082-u*.015,1.988+.016*math.sin(i)),(x-.020,.080,2.035+.012*math.cos(i*.7)),(x-.034,.006,2.088-.024*u),(x-.026,-.095,2.044-.02*u),(x+.015,-.121,1.961)]
 path('Sculpted swept lock',pts,.009 if i%2 else .011,hairlight if i in [11,12] else hair,[.65,1.2,1,.65,.02])
 for j in [-1,1]:path('Hair carved strand',[(p[0]+j*.004,p[1]+.003,p[2]+.007) for p in pts],.0007,hairlight if i==3 else hair,[.3,1,.8,.5,.01])
# One loose strand breaks the forehead silhouette.
path('Loose forelock',[(.025,.08,2.06),(-.014,.135,2.06),(-.062,.137,2.014),(-.069,.118,1.969)],.006,hair,[1,1,.65,.01])
for sign in [-1,1]:
 for j in range(7):
  path('Temple swept lock',[(sign*(.09+j*.005),.034-j*.009,1.99),(sign*.131,-.034,1.957),(sign*.113,-.068,1.89-j*.004)],.009,hair,[.4,1,.01])
# Fine beard growth reinforces an adult face without a cartoon moustache.
for i in range(850):
 z=random.uniform(1.78,1.866);x=random.uniform(-.104,.104)
 rx=.077+(z-1.78)*.5;ry=.066+(z-1.78)*.37;cy=.020-(z-1.78)*.20
 if abs(x)>rx*.94 or (abs(x)<.039 and z>1.803):continue
 y=cy+ry*math.sqrt(max(0,1-(x/rx)**2))+.0015
 path('Fine jaw stubble',[(x,y,z),(x+.001,y+.0005,z-.0035)],.00023,hair,[.7,.1])

# Red scarf around neck with lifted wind-swept tail.
loft('Scarf wrapped collar',[(1.65,.102,.088,0,0),(1.68,.112,.101,0,0),(1.705,.106,.094,0,0),(1.73,.099,.084,0,0)],lining,48,2)
for j in range(3):path('Scarf folds',[(-.089,.04,1.674+j*.014),(-.04,.104,1.678+j*.011),(.04,.109,1.669+j*.012),(.092,.04,1.678+j*.014)],.005,lining)
# Ribbon surface with broad flowing gesture.
verts=[]
for j in range(24):
 t=j/23;cx=-.07-.65*t;cy=-.05-.15*t;cz=1.69+.18*math.sin(t*2.8)
 for i in range(8):
  u=i/7-.5
  verts.append((cx,cy+u*.065,cz+u*(.12-.045*t)+.012*math.sin(t*11+u*5)))
mesh('Swept crimson scarf',verts,[(j*8+i,j*8+i+1,(j+1)*8+i+1,(j+1)*8+i) for j in range(23) for i in range(7)],lining,2,.004)

# Signature dueling blade: downward in the right hand, clear of the body.
a=Vector((.42,.145,.985));tip=Vector((.95,.17,.07));axis=(tip-a).normalized();side=Vector((axis.z,0,-axis.x)).normalized()
bar('Sword wrapped grip',a-axis*.065,a+axis*.065,.025,leather)
for j in range(8):ring('Grip brass wire',a-axis*.055+axis*j*.015,.025,.0015,gold,'Z')
base=a+axis*.09
path('Split brass sword guard',[base-side*.105-axis*.017,base-side*.04,base,base+side*.055,base+side*.14+axis*.025],.015,gold,[.3,.8,1,.8,.15])
# Diamond cross section, curved elongated tip.
verts=[]
for p,w in [(base+axis*.025,.044),(base+axis*.12,.048),(tip-axis*.15,.028),(tip,0)]:
 verts.extend([p+side*w,p+Vector((0,.010,0)),p-side*w,p-Vector((0,.010,0))])
faces=[]
for j in range(3):
 for i in range(4):faces.append((j*4+i,j*4+(i+1)%4,(j+1)*4+(i+1)%4,(j+1)*4+i))
blade=mesh('Tempered dueling blade',verts,faces,steel,0)
for p in blade.data.polygons:p.use_smooth=False
path('Silver cutting edge',[base+axis*.035+side*.044,base+axis*.12+side*.048,tip-axis*.15+side*.028,tip],.0027,edge)
path('Temporal blade channel',[base+axis*.10+Vector((0,.012,0)),tip-axis*.13+Vector((0,.012,0))],.0032,glow)
ell('Sword pommel',a-axis*.086,(.032,.027,.032),gold)

# Studio presentation; hero collection remains separate and editable.
scene=bpy.context.scene
studio=bpy.data.collections.new('STUDIO | lighting and plinth');scene.collection.children.link(studio)
def studio_move(o):
 for c in list(o.users_collection):c.objects.unlink(o)
 studio.objects.link(o)
 return o
stage=mat('Stage charcoal',(.028,.036,.045),.22,.48)
bpy.ops.mesh.primitive_cylinder_add(vertices=128,radius=.82,depth=.07,location=(0,0,-.012));o=studio_move(bpy.context.object);o.name='Obsidian dais';o.data.materials.append(stage);be=o.modifiers.new('Rim bevel','BEVEL');be.width=.018;be.segments=4;smooth(o)
for rad in [.72,.77]:studio_move(ring('Dais brass inlay',(0,0,.026),rad,.0015,gold,'Z'))
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.05));studio_move(bpy.context.object).data.materials.append(stage)
scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.07,.09,.11,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.35
for name,pos,power,size,col in [('Warm key',(1.6,3.2,4.5),330,3,(1,.87,.71)),('Cool edge',(-2,-2.1,3.0),600,2,(.24,.70,.80)),('Face fill',(-2,3,2.8),150,2,(.78,.86,1)),('Hair rim',(1,-1.2,4),250,1.5,(1,.64,.38))]:
 bpy.ops.object.light_add(type='AREA',location=pos);o=studio_move(bpy.context.object);o.name=name;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.data.color=col;o.rotation_euler=(Vector((0,0,1.25))-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(2.7,6,3.0));cam=studio_move(bpy.context.object);scene.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=2.75
cam.rotation_euler=(Vector((.03,0,1.08))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.engine='CYCLES';scene.cycles.samples=48;scene.cycles.use_denoising=True
scene.render.resolution_x=1300;scene.render.resolution_y=1600;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
# Organized source opens on the hero; no automatic replacement of the game asset.
bpy.ops.object.select_all(action='DESELECT');head.select_set(True);bpy.context.view_layer.objects.active=head
for area in bpy.context.screen.areas:
 if area.type=='VIEW_3D':
  area.spaces.active.region_3d.view_distance=3.5;area.spaces.active.region_3d.view_location=Vector((0,0,1.1))
scene.render.filepath=os.path.join(OUT,'hero-blender.png')
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'LastSecond.blend'))
for name,pos,target,scale in [('hero-blender',(2.7,6,3.0),(.03,0,1.08),2.75),('portrait-blender',(1.4,5,2.7),(0,0,1.82),.85),('back-blender',(-2.7,-6,3),(0,0,1.08),2.75)]:
 cam.location=pos;cam.data.ortho_scale=scale;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=os.path.join(OUT,name+'.png');bpy.ops.render.render(write_still=True)
print('LAST_SECOND_STUDY_COMPLETE')


