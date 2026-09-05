import bpy, math, os
from mathutils import Vector
ROOT = r'D:/Work/UnityProjects/Still'
OUT = os.path.join(ROOT, 'Assets/_Project/Resources/Characters')
os.makedirs(OUT, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

def mat(name, color, metallic=0.0, emission=False):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (*color, 1)
    p.inputs['Metallic'].default_value = metallic
    p.inputs['Roughness'].default_value = 0.36
    if emission:
        p.inputs['Emission Color'].default_value = (*color, 1)
        p.inputs['Emission Strength'].default_value = 2.0
    return m
armor = mat('Armor', (0.13, 0.18, 0.23), .65)
edge = mat('Edge', (.57, .65, .67), .6)
cloth = mat('Cloth', (.035, .055, .075))
cyan = mat('Core', (.12, .75, 1), emission=True)
red = mat('Core_Red', (1, .13, .08), emission=True)
gold = mat('Core_Gold', (1, .57, .08), emission=True)

# Blender +Y is the character's front; FBX exports to Unity +Z / +Y up.
def box(name, pos, dims, material, bevel=.025):
    bpy.ops.mesh.primitive_cube_add(size=1, location=pos)
    o = bpy.context.object; o.name = name; o.dimensions = dims
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.data.materials.append(material)
    if bevel:
        mod=o.modifiers.new('Forged edges', 'BEVEL'); mod.width=bevel; mod.segments=1
        bpy.context.view_layer.objects.active=o
        bpy.ops.object.modifier_apply(modifier=mod.name)
        o.modifiers.new('Weighted normals', 'WEIGHTED_NORMAL')
    return o

def segment(name, a, b, width, material):
    o=box(name, (Vector(a)+Vector(b))/2, (width, width, (Vector(b)-Vector(a)).length), material)
    o.rotation_euler=(Vector(b)-Vector(a)).to_track_quat('Z','Y').to_euler()
    return o

def make(kind, accent):
    before=set(bpy.data.objects)
    heavy=kind=='Sentinel'; ranger=kind=='Archer'
    w=.8 if heavy else .57
    box('Torso', (0,0,1.12), (w,.38,.53), armor, .06)
    box('Breastplate', (0,.22,1.15), (w*.85,.10,.34), edge, .04)
    box('Core', (0,.29,1.2), (.13,.04,.25), accent, .012)
    box('Belt', (0,0,.81), (w*.9,.42,.12), cloth)
    box('Buckle', (0,.235,.82), (.16,.06,.10), accent)
    for side in [-1,1]:
        x=side*.17
        box('Leg_L' if side<0 else 'Leg_R', (x,0,.43), (.23,.29,.66), armor)
        box('Knee', (x,.18,.46), (.24,.12,.16), edge)
        box('Boot', (x,.09,.1), (.25,.43,.18), cloth)
        sx=side*(w/2+.12)
        box('Shoulder', (sx,0,1.35), (.36 if heavy else .25,.42,.24), edge)
        box('Arm_L' if side<0 else 'Arm_R', (sx,0,1.02), (.2,.26,.45), armor)
        box('Hand', (sx,.025,.77), (.2,.24,.18), cloth)
    box('Neck', (0,0,1.43), (.2,.2,.16), cloth)
    head=box('Helm', (0,0,1.66), (.43 if heavy else .37,.36,.37), armor, .07)
    box('Visor', (0,.186,1.67), (.30,.03,.07), accent, .008)
    box('Crown', (0,-.01,1.87), (.13,.33,.08), edge)
    if heavy:
        for s in [-1,1]:
            segment('Crest', (s*.17,0,1.76),(s*.32,0,2.02),.09,edge)
        segment('Cleaver grip', (.56,.05,.64),(.56,.05,1.03),.09,cloth)
        box('Cleaver', (.56,.12,1.35), (.20,.10,.61), edge)
        box('Cleaver edge', (.68,.12,1.35), (.035,.12,.61), accent, .005)
    elif ranger:
        box('Hood back', (0,-.16,1.59), (.47,.16,.55), cloth, .045)
        box('Quiver', (-.15,-.29,1.19), (.18,.18,.67), cloth)
        for x in [-.20,-.12]: segment('Arrow', (x,-.3,1.26),(x,-.3,1.77),.025,edge)
        for s in [-1,1]:
            segment('Bow limb', (.50,.03,1.05),(.50,.24,1.05+s*.49),.065,edge)
            segment('Bow tip', (.50,.24,1.05+s*.49),(.50,.36,1.05+s*.57),.045,accent)
        segment('Bow string', (.50,.36,.48),(.50,.36,1.62),.012,cloth)
    else:
        # Split coat forms a recognizable rear silhouette without hiding the legs.
        for s in [-1,1]:
            o=box('Coat tail', (s*.17,-.22,.73), (.30,.10,.64), cloth)
            o.rotation_euler.x=-.16
        box('Spine light', (0,-.23,1.15), (.08,.04,.42), accent)
    objects=list(set(bpy.data.objects)-before)
    # Four material groups keep each character to four renderers in Unity.
    grouped=[]
    groups={material: [o for o in objects if o.data.materials[0] == material] for material in {o.data.materials[0] for o in objects}}
    for material, group in groups.items():
        bpy.ops.object.select_all(action='DESELECT')
        for o in group: o.select_set(True)
        bpy.context.view_layer.objects.active=group[0]
        bpy.ops.object.convert(target='MESH')
        bpy.ops.object.join()
        merged=bpy.context.object; merged.name=material.name
        bpy.context.scene.cursor.location=(0,0,0)
        bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
        grouped.append(merged)
    objects=grouped
    head=objects[0]
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects: o.select_set(True)
    bpy.context.view_layer.objects.active=head
    if kind != 'Wanderer' or not os.path.exists(os.path.join(ROOT,'ArtSource/Characters/Keeper/Keeper.blend')):
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,kind+'.fbx'), use_selection=True,
            object_types={'MESH'}, axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
            bake_anim=False, use_mesh_modifiers=True, add_leaf_bones=False)
    return objects

for i,(kind,accent) in enumerate([('Wanderer',cyan),('Sentinel',red),('Archer',gold)]):
    objects=make(kind,accent)
    for o in objects: o.location.x+=(i-1)*2.2

floor=mat('Stage',(.028,.042,.062), .15)
box('Stage', (0,0,-.15),(8,5,.2),floor)
world=bpy.context.scene.world or bpy.data.worlds.new('World')
bpy.context.scene.world=world; world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.055,.08,.12,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.45
for name,loc,power,size in [('Key', (1,4,6),1800,5),('Rim',(-3,-3,4),2000,4),('Fill',(4,1,3),700,3)]:
    bpy.ops.object.light_add(type='AREA',location=loc)
    o=bpy.context.object; o.name=name; o.data.energy=power; o.data.shape='DISK'; o.data.size=size
    o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(4.1,8,5.5))
cam=bpy.context.object; cam.rotation_euler=(Vector((0,0,1))-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.type='ORTHO'; cam.data.ortho_scale=8.6
scene=bpy.context.scene; scene.camera=cam
scene.render.engine='CYCLES'; scene.cycles.samples=32
scene.render.resolution_x=1500; scene.render.resolution_y=950; scene.render.resolution_percentage=100
scene.render.filepath=os.path.join(ROOT,'ArtSource/Characters/lineup.png')
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT,'ArtSource/Characters/StillCharacters.blend'))
bpy.ops.render.render(write_still=True)
print('STILL_CHARACTER_EXPORT_OK')
