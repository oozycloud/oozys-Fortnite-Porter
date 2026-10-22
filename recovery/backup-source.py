"""Create a reproducible source backup without game data or generated payloads."""
from pathlib import Path
import json
import re
import shutil
import zipfile

root = Path(__file__).resolve().parent.parent
version = re.search(r'const string Version = "([^"]+)"', (root / 'work/FortniteObj/PorterRelease/ProductInfo.cs').read_text()).group(1)
source = root / 'outputs' / f'oozys-Fortnite-Porter-Source ({version}).zip'
items = []
for base in ['work/FortniteObj', 'work/Installer', 'work/AssetChecks', 'work/FormatChecks', 'work/GameChecks',
             'recovery/dependencies', 'recovery/licenses', 'recovery/parser-patches', 'recovery/unity-runtime', 'docs']:
    for path in (root / base).rglob('*'):
        if not path.is_file() or '__pycache__' in path.parts or path.name == 'FortniteInstaller.payload.zip':
            continue
        if base.startswith('work/') and any(part in ('bin', 'obj') for part in path.relative_to(root / base).parts):
            continue
        items.append(path)
for name in ['README.md', 'CHANGELOG.md', 'recovery/package-current.py', 'recovery/test-package.ps1',
             'recovery/unity-requirements.txt', 'recovery/original/launcher.exe', 'recovery/SOURCE-RECOVERY.md',
             'recovery/verify-unity.py', 'recovery/backup-source.py', 'recovery/build-website-catalog.py', 'recovery/publish-releases.py']:
    items.append(root / name)
source.parent.mkdir(exist_ok=True)
with zipfile.ZipFile(source, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
    for path in sorted(items):
        archive.write(path, path.relative_to(root))
with zipfile.ZipFile(source) as archive:
    assert archive.testzip() is None
backup = Path.home() / 'Desktop' / 'oozys Fortnite Porter Source Backup'
backup.mkdir(exist_ok=True)
shutil.copy2(source, backup / source.name)
print(json.dumps({'Source': str(source), 'Files': len(items), 'Bytes': source.stat().st_size}))
