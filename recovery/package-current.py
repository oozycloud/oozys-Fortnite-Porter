from pathlib import Path
import shutil,ctypes,struct,json,zipfile
root=Path(__file__).resolve().parent.parent
version='beta 0.7.2';stage=root/'work/publish-beta0.7.2';app=stage/'app';launcher=f'oozys-Fortnite-Porter ({version}).exe'
shutil.copy2(root/'recovery/original/launcher.exe',stage/launcher)
k=ctypes.WinDLL('kernel32',use_last_error=True)
k.LoadLibraryExW.argtypes=[ctypes.c_wchar_p,ctypes.c_void_p,ctypes.c_uint];k.LoadLibraryExW.restype=ctypes.c_void_p
k.FindResourceW.argtypes=[ctypes.c_void_p,ctypes.c_void_p,ctypes.c_void_p];k.FindResourceW.restype=ctypes.c_void_p
k.SizeofResource.argtypes=[ctypes.c_void_p,ctypes.c_void_p];k.SizeofResource.restype=ctypes.c_uint
k.LoadResource.argtypes=[ctypes.c_void_p,ctypes.c_void_p];k.LoadResource.restype=ctypes.c_void_p
k.LockResource.argtypes=[ctypes.c_void_p];k.LockResource.restype=ctypes.c_void_p
k.FreeLibrary.argtypes=[ctypes.c_void_p]
m=k.LoadLibraryExW(str(stage/launcher),None,2);res=k.FindResourceW(m,1,16);size=k.SizeofResource(m,res)
assert size>0
blob=bytearray(ctypes.string_at(k.LockResource(k.LoadResource(m,res)),size));k.FreeLibrary(m)
blob=blob.replace('0.1.1'.encode('utf-16le'),'0.7.2'.encode('utf-16le'))
offset=blob.index(struct.pack('<I',0xFEEF04BD));struct.pack_into('<IIII',blob,offset+8,7,131072,7,131072)
k.BeginUpdateResourceW.argtypes=[ctypes.c_wchar_p,ctypes.c_bool];k.BeginUpdateResourceW.restype=ctypes.c_void_p
k.UpdateResourceW.argtypes=[ctypes.c_void_p,ctypes.c_void_p,ctypes.c_void_p,ctypes.c_ushort,ctypes.c_void_p,ctypes.c_uint];k.UpdateResourceW.restype=ctypes.c_bool
k.EndUpdateResourceW.argtypes=[ctypes.c_void_p,ctypes.c_bool];k.EndUpdateResourceW.restype=ctypes.c_bool
handle=k.BeginUpdateResourceW(str(stage/launcher),False);assert handle
buf=ctypes.create_string_buffer(bytes(blob));assert k.UpdateResourceW(handle,16,1,1033,buf,len(blob));assert k.EndUpdateResourceW(handle,False)
shutil.copytree(root/'recovery/licenses',app/'licenses',dirs_exist_ok=True)
shutil.copy2(root/'docs/releases/beta-0.7.2.md',app/'RELEASE-NOTES.md')
with zipfile.ZipFile(app/'PorterEmoteTools.zip','w',zipfile.ZIP_DEFLATED) as z:
 for source,target in [('FortniteObj.blender_emote_tools.py','__init__.py'),('FortniteObj.blender_animation.py','blender_animation.py')]:z.write(root/'work/FortniteObj'/source,'porter_emote_tools/'+target)
files=sorted({p.relative_to(stage).as_posix() for p in stage.rglob('*') if p.is_file()}|{'app/installation.json'})
(app/'installation.json').write_text(json.dumps(dict(ProductId='oozycloud.fortnite-porter',Version=version,Launcher=launcher,Files=files),indent=2),encoding='utf-8')
with zipfile.ZipFile(root/'work/Installer/FortniteInstaller.payload.zip','w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
 for f in files:z.write(stage/f,f)
print('Packaged',len(files),'managed files')
