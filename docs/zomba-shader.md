# Zomba shader recovery

The beta 0.7.3 implementation was derived from local game data, rather than an invented pulse. No game bytecode or textures are included with this document/source distribution.

Material: WHEEL_Proto09.Proto_09_Mat. Material resource ID (raw UE3 bytes): f4eb4c8de4305b4aad1086363d9d3e64. Pixel shader entry (raw GUID bytes): 47e489f937b1654588bd03224d48e241. Shader type: TBasePassPixelShaderFNoLightMapPolicyNoSkyLightNoPixelVelocity. Recovered from RefShaderCache-PC-D3D-SM5 on 17 November 2026. DXBC disassembly and serialized uniform-expression trees were examined locally.

The uniform expressions provide rotation matrices at t and -2t radians; LUT offsets fract(-.2t) and fract(-.5t). The pixel shader samples Twirl_RGB_T as R/B at the first rotation and G/A at the second. Curve_Rainbow supplies the pulse and hue lookup, evaluated at twice squared distance from the UV centre. Opacity uses edge=max(3*(.22-radius_squared),0), the source amplitude threshold, pulse, and OpacityHardness=15. Painted particles disable HueShift_Sharpness and retain the selected colour. The default particle colour is (1,.1,.1), recovered from DistributionVectorParticleParameter.

rocketleague_wheel_fx.py implements the recovered material calculations in editable Blender nodes. It uses source texture sRGB sampling (SRGB defaults true on the texture), independent packed alpha, source attachment locations and source initial rotation. It omits Unreal fog, selection highlighting, exposure/post-processing and renderer-specific sprite view interpolation. Timing uses seconds from scene FPS/FPS-base; ZombaTimeScale=1 retains source rates.

The shader-cache investigation required decoding each independently compressed object. The existing generic native reader does not currently export this shader cache correctly; research-time decoder changes were not included in the app.
