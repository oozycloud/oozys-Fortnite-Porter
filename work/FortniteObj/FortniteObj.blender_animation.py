"""Retarget source local poses without replacing the character's bone lengths."""
import bpy, math, re
from array import array
from mathutils import Matrix, Quaternion, Vector

BASIS = Matrix.Diagonal((1., -1., 1., 1.))
DISPLAY = Matrix.Rotation(-math.pi / 2, 4, 'Z')


def local_matrix(p, q, scale, root=False):
    local = Matrix.LocRotScale(Vector(p) / 100, Quaternion((q[3], q[0], q[1], q[2])), Vector(scale))
    return BASIS @ local @ BASIS @ DISPLAY if root else DISPLAY.inverted() @ BASIS @ local @ BASIS @ DISPLAY


def retarget_basis(target_rest, source_rest, animated, mode, root=False, accessory=False):
    tp, tq, ts = target_rest.decompose()
    sp, sq, ss = source_rest.decompose()
    ap, aq, ass = animated.decompose()
    # UE translation retargeting keeps the animation's local rotation. Applying
    # a source-rest delta to a different mesh rest pose adds an unwanted pose
    # offset (particularly at shoulders, wrists and fingers).
    if accessory:
        delta_q = sq.inverted() @ aq
    elif mode == 'AnimationRelative':
        delta_q = tq.inverted() @ aq @ sq.inverted() @ tq
    else:
        delta_q = tq.inverted() @ aq
    if root or mode == 'Animation':
        position = ap
    elif mode == 'AnimationScaled':
        position = ap * (tp.length / sp.length if sp.length > 1e-6 else 1.0)
    elif mode == 'AnimationRelative':
        position = tp + ap - sp
    elif mode == 'OrientAndScale' and sp.length > 1e-6 and tp.length > 1e-6:
        position = sp.rotation_difference(tp) @ ap * (tp.length / sp.length)
    else:
        position = tp
    translation = tq.inverted() @ (position - tp)
    translation = Vector(tuple(translation[i] / ts[i] if abs(ts[i]) > 1e-8 else 0 for i in range(3)))
    reference_scale = ss if accessory or mode == 'AnimationRelative' else ts
    scale = Vector(tuple(ass[i] / reference_scale[i] if abs(reference_scale[i]) > 1e-8 else 1 for i in range(3)))
    return translation, delta_q.normalized(), scale


def apply_animation(rig, data):
    rig.animation_data_create()
    actions = []
    scene_fps = data['Clips'][0]['Fps']
    for clip in data['Clips']:
        bones = clip.get('Bones', data['Bones'])
        source = {b['Name'].casefold(): b for b in bones}
        tracks = {t['Bone'].casefold(): t['Frames'] for t in clip['Tracks']}
        targets = []
        for pb in rig.pose.bones:
            name = pb.bone.get('SourceName', pb.name).casefold()
            src = source.get(name)
            values = tracks.get(name)
            if src is None or not values:
                continue
            if len(values) != clip['Frames']:
                raise ValueError('Animation track frame count does not match the clip')
            parent_rest = pb.parent.bone.matrix_local if pb.parent else Matrix.Identity(4)
            target_rest = parent_rest.inverted() @ pb.bone.matrix_local
            root = src['Parent'] < 0
            source_rest = local_matrix(src['Translation'], src['Rotation'], src['Scale'], root)
            mode = src.get('TranslationMode') or ('Animation' if root else 'AnimationScaled' if name == 'pelvis' else 'Skeleton')
            main = name in {'root','pelvis','head','neck_01','neck_02'} or name.startswith(('spine_', 'clavicle_', 'upperarm_', 'lowerarm_', 'hand_', 'thigh_', 'calf_', 'foot_', 'ball_', 'thumb_', 'index_', 'middle_', 'ring_', 'pinky_', 'ik_'))
            targets.append((pb, src, values, target_rest, source_rest, root, mode, not main))
        if not targets:
            raise ValueError('No animation tracks match this rig')
        action = bpy.data.actions.new(clip['Name'])
        action.use_fake_user = True
        rig.animation_data.action = action
        for pb in rig.pose.bones:
            pb.matrix_basis = Matrix.Identity(4)
        times = [1 + (clip['FrameTimes'][frame] if 'FrameTimes' in clip else frame/clip['Fps']) * scene_fps for frame in range(clip['Frames'])]
        for target_index, (pb, src, values, target_rest, source_rest, root, mode, accessory) in enumerate(targets):
            channels = [[] for _ in range(10)]
            previous = None
            for frame in range(clip['Frames']):
                v = values[frame]
                if len(v) != 10 or not all(math.isfinite(x) for x in v):
                    raise ValueError('Invalid animation transform for ' + pb.name)
                animated = local_matrix(v[:3], v[3:7], v[7:10], root)
                p, q, scale = retarget_basis(target_rest, source_rest, animated, mode, root, accessory)
                if previous is not None and previous.dot(q) < 0:
                    q.negate()
                previous = q.copy()
                for channel, value in zip(channels, (*p, *q, *scale)):
                    channel.append(value)
            pb.rotation_mode = 'QUATERNION'
            # Seed one key per property to let Blender create the correct action
            # slot/group, then fill each curve in bulk. Per-frame key insertion
            # repeatedly sorts growing curves and stalls long 240 FPS emotes.
            for path in ('location', 'rotation_quaternion', 'scale'):
                pb.keyframe_insert(data_path=path, frame=times[0], group=pb.name)
            bag = next(bag for layer in action.layers for strip in layer.strips for bag in strip.channelbags if bag.slot_handle == rig.animation_data.action_slot.handle)
            channel_index = 0
            for path, count in (('location',3), ('rotation_quaternion',4), ('scale',3)):
                data_path = pb.path_from_id(path)
                for component in range(count):
                    curve = bag.fcurves.find(data_path, index=component)
                    values = channels[channel_index];channel_index += 1
                    pairs = [(times[0],values[0]), (times[-1],values[-1])] if all(v == values[0] for v in values) else zip(times,values)
                    coordinates = array('f', (v for pair in pairs for v in pair))
                    curve.keyframe_points.add(len(coordinates)//2-1)
                    curve.keyframe_points.foreach_set('co',coordinates)
                    for key in curve.keyframe_points:key.interpolation='LINEAR'
                    curve.update()
            if target_index % 20 == 0 or target_index == len(targets)-1:
                print(f"PORTER: Attaching {clip['Name']}: {target_index+1}/{len(targets)} bones",flush=True)
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in bag.fcurves:
                        for key in curve.keyframe_points:
                            key.interpolation = 'LINEAR'
        actions.append({'Name': action.name, 'Frames': clip['Frames'], 'MatchedBones': len(targets), 'Fps': scene_fps, 'Duration': clip.get('Duration', (clip['Frames']-1)/clip['Fps']), 'Retargeting': 'Authored local rotations; target bone lengths; source translation modes; accessory rest deltas'})
    if not actions:
        raise ValueError('No animation actions were created')
    # Montages can list a brief introduction before the actual dance. Retain
    # every action, but open the scene on the playable main loop.
    first = next((action for action in actions if not re.search(r'_(intro|outro|breakout|start|end)(?:_|$)',action['Name'],re.I)),actions[0])
    for item in actions:item['Active']=item is first
    action = bpy.data.actions[first['Name']]
    rig.animation_data.action = action
    if action.slots:
        rig.animation_data.action_slot = action.slots[0]
    scene = bpy.context.scene
    scene.render.fps = round(first['Fps'])
    scene.render.fps_base = round(first['Fps']) / first['Fps']
    scene.frame_start = 1
    scene.frame_end = math.ceil(first['Duration']*scene_fps - 1e-3)+1
    scene.frame_set(1)
    rig['Rig type'] = 'Retargeted emote actions on source deformation bones'
    return actions

