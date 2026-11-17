import bpy,math,json
scene=bpy.context.scene
rig=next(o for o in scene.objects if o.type=='ARMATURE' and o.animation_data and o.animation_data.action)
action=rig.animation_data.action
assert action.get('Loop')
period=action['PlaybackFrames'];assert scene.frame_start==1 and scene.frame_end==period
assert abs(action.frame_range[1]-(period+1))<1e-4
assert abs(period/(scene.render.fps/scene.render.fps_base)-action['LoopDuration'])<1e-5

def pose(frame):
 scene.frame_set(frame);bpy.context.view_layer.update()
 return [v for b in rig.pose.bones for row in b.matrix for v in row]
a=pose(1);b=pose(period+1)
assert max(abs(x-y) for x,y in zip(a,b))<1e-5,'Loop closure is discontinuous'
a=pose(2);b=pose(period+2)
assert max(abs(x-y) for x,y in zip(a,b))<1e-4,'Animation curves do not repeat'
print('PASS',json.dumps({'PlaybackFrames':period,'ClosureKeyFrame':period+1,'Duration':action['LoopDuration'],'PoseSeam':True,'CyclesRepeat':True}))

if action.get('SeamBlendSeconds',0)>0:
 def local(frame):
  scene.frame_set(frame)
  return {b.name:(b.location.copy(),b.rotation_quaternion.copy()) for b in rig.pose.bones}
 prev=local(period);zero=local(1);nxt=local(2)
 for name,(p,q) in zero.items():
  a=(prev[name][1].conjugated()@q).normalized();b=(q.conjugated()@nxt[name][1]).normalized()
  error=2*math.acos(min(1,abs(a.dot(b))))
  assert error<.003,(name,error)
  if not action.get('Traversal'):
   assert ((p-prev[name][0])-(nxt[name][0]-p)).length<1e-5,name
 print('PASS matching seam angular and linear velocity')
