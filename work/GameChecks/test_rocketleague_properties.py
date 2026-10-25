import unittest,sys,struct,tempfile,json
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'FortniteObj'))
from rocketleague_properties import Package,PropertyError
from rocketleague_animation import attach_animation

class Properties(unittest.TestCase):
 def setUp(self):
  self.p=Package.__new__(Package);self.p.names={0:'None',1:'Value',2:'IntProperty',3:'Nested',4:'StructProperty',5:'Example'};self.p.exports={};self.p.imports={};self.p.objects={}
 def name(self,i):return struct.pack('<ii',i,0)
 def integer(self,value):return self.name(1)+self.name(2)+struct.pack('<iii',4,0,value)
 def test_object_header_wins_over_nested_struct(self):
  nested=self.integer(1)+self.integer(2)+self.name(0)
  raw=b'HEAD'+self.name(3)+self.name(4)+struct.pack('<ii',len(nested),0)+self.name(5)+nested+self.name(0)
  props,offset,end=self.p.object_properties(raw)
  self.assertEqual(offset,4);self.assertIn('Nested',props);self.assertEqual(end,len(raw))
 def test_truncated_tag_is_rejected(self):
  with self.assertRaises(PropertyError):self.p.properties(self.integer(3)[:-1])
 def test_child_false_and_zero_override_defaults(self):
  self.p.objects={1:{'Properties':{'Enabled':True,'Scale':2}},2:{'Archetype':1,'Properties':{'Enabled':False,'Scale':0}}}
  self.assertEqual(self.p.inherited(2),{'Enabled':False,'Scale':0})
 def test_archetype_cycle_is_rejected(self):
  self.p.objects={1:{'Archetype':2,'Properties':{}},2:{'Archetype':1,'Properties':{}}}
  with self.assertRaises(PropertyError):self.p.inherited(1)
 def test_lookup_table_is_float_data(self):
  self.assertEqual(self.p.value('ArrayProperty',None,struct.pack('<iff',2,.25,1.5),'LookupTable',0),[.25,1.5])
 def test_animation_preserves_rate_delay_and_bone_mapping(self):
  with tempfile.TemporaryDirectory() as folder:
   root=Path(folder);model=root/'mesh.gltf';model.write_text(json.dumps({'asset':{'version':'2.0'},'nodes':[{'name':'root'}]}))
   animation=root/'test.md5anim';animation.write_text('numFrames 2\nframeRate 30\nhierarchy { "root" -1 63 0 }\nframe 0 { 0 0 0 0 0 0 }\nframe 1 { 100 200 300 0 0 0 }')
   result=attach_animation(model,animation,root/'animated.gltf',.5,.4)
   self.assertAlmostEqual(result['Duration'],.4+1/15);self.assertEqual(result['Tracks'],1)
   doc=json.loads((root/'animated.gltf').read_text());self.assertEqual(len(doc['animations'][0]['channels']),2)
   blob=(root/'animated.anim.bin').read_bytes();self.assertEqual(struct.unpack_from('<fff',blob,20),(1,3,-2))

if __name__=='__main__':unittest.main()
