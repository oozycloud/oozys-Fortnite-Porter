import * as THREE from 'three';

// The same recovered Zomba equations used by the Blender exporter, evaluated on the GPU.
const fragment=`
 uniform sampler2D patternMap;uniform sampler2D rampMap;uniform float shaderTime;uniform bool painted;uniform vec3 paintColour;varying vec2 wheelUv;
 vec2 rotateUv(vec2 p,float a){float c=cos(a),s=sin(a);return vec2(c*p.x-s*p.y,s*p.x+c*p.y)+.5;}
 void main(){
  vec2 p=wheelUv-vec2(.5);float radius=dot(p,p),r2=min(radius,1.0)*2.0;
  vec4 a=texture2D(patternMap,rotateUv(p,shaderTime));vec4 b=texture2D(patternMap,rotateUv(p,-2.0*shaderTime));
  vec3 pulse=texture2D(rampMap,vec2(r2+fract(-.2*shaderTime),r2)).rgb;
  vec3 hue=texture2D(rampMap,vec2(r2+fract(-.5*shaderTime),r2)).rgb;
  float edge=max((.22-radius)*3.0,0.0),sum=a.r+b.g;
  float amp=clamp((sum*a.b*b.a-.1)*10.0,0.0,1.0);
  float patternLight=clamp(sum*(pulse.r+pulse.g+pulse.b)*.8,0.0,1.0);
  float flash=amp*pulse.r,boost=flash*6.0;
  float strength=(patternLight+boost+.3)*pow(edge,.2)*2.0+boost;
  float alpha=clamp((flash+edge)*edge*15.0,0.0,1.0);
  vec3 base=vec3(1.0,.1,.1),rgb=paintColour;
  if(!painted){float angle=hue.b*r2*6.28318530718,c=cos(angle),s=sin(angle);float mean=(base.r+base.g+base.b)/3.0;rgb=c*base+(1.0-c)*vec3(mean)+s*vec3(base.b-base.g,base.r-base.b,base.g-base.r)/sqrt(3.0);}
  gl_FragColor=vec4(rgb*strength,alpha);
  #include <tonemapping_fragment>
  #include <colorspace_fragment>
 }
`;
const vertex=`varying vec2 wheelUv;void main(){wheelUv=uv;gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.0);}`;

export async function createWheelEffects(root){
 const replacements=new Map(),textures=new Set();let meshes=0;
 const sources=new Set();root.traverse(o=>{if(o.isMesh)for(const material of Array.isArray(o.material)?o.material:[o.material])if(material?.userData?.StudioWheelShader?.kind==='zomba')sources.add(material);});
 try{
  for(const original of sources){const source=original.userData.StudioWheelShader;
   const [pattern,ramp]=await Promise.all([new THREE.TextureLoader().loadAsync(source.pattern),new THREE.TextureLoader().loadAsync(source.ramp)]);
   for(const texture of [pattern,ramp]){texture.colorSpace=THREE.SRGBColorSpace;texture.wrapS=texture.wrapT=THREE.RepeatWrapping;texture.flipY=true;textures.add(texture);}
   const material=new THREE.ShaderMaterial({name:original.name,uniforms:{patternMap:{value:pattern},rampMap:{value:ramp},shaderTime:{value:0},painted:{value:Array.isArray(source.paint)},paintColour:{value:new THREE.Vector3(...(source.paint||[1,.1,.1]))}},vertexShader:vertex,fragmentShader:fragment,transparent:true,depthWrite:false,side:THREE.DoubleSide,toneMapped:true});
   material.userData={...original.userData};replacements.set(original,{material,timeScale:Number.isFinite(source.timeScale)?source.timeScale:1});
  }
  root.traverse(o=>{if(!o.isMesh)return;const replace=material=>{const effect=replacements.get(material);if(effect){meshes++;return effect.material;}return material;};o.material=Array.isArray(o.material)?o.material.map(replace):replace(o.material);});
  for(const original of replacements.keys())original.dispose();
  return {count:meshes,update(seconds){for(const {material,timeScale}of replacements.values())material.uniforms.shaderTime.value=seconds*timeScale;},dispose(){for(const texture of textures)texture.dispose();for(const {material}of replacements.values())material.dispose();}};
 }catch(error){for(const texture of textures)texture.dispose();for(const {material}of replacements.values())material.dispose();throw error;}
}
