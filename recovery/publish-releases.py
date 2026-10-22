"""Publish preserved porter builds; credentials stay in memory via Git's helper."""
import argparse
import hashlib
import http.client
import json
import subprocess
import urllib.parse
import urllib.request
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
REPO = 'oozycloud/oozys-Fortnite-Porter'
API = 'https://api.github.com/repos/' + REPO
parser = argparse.ArgumentParser()
parser.add_argument('--publish', action='store_true')
parser.add_argument('--draft-only', action='store_true', help='Upload verified assets to a draft without publishing it.')
parser.add_argument('--version', action='append')
args = parser.parse_args()
credential = subprocess.run(['git', '-C', str(ROOT/'work/github-docs'), 'credential', 'fill'],
    input='protocol=https\nhost=github.com\n\n', text=True, capture_output=True, check=True)
values = dict(line.split('=', 1) for line in credential.stdout.splitlines() if '=' in line)
token = values.get('password')
if not token: raise RuntimeError('Git credential helper did not provide GitHub authentication.')
headers = {'Authorization': 'Bearer ' + token, 'User-Agent': 'oozys-porter-release',
           'Accept': 'application/vnd.github+json', 'X-GitHub-Api-Version': '2022-11-28'}

def api(path, method='GET', body=None):
    data = json.dumps(body).encode() if body is not None else None
    request = urllib.request.Request(path if path.startswith('https:') else API + path, data=data, headers=headers, method=method)
    with urllib.request.urlopen(request, timeout=60) as response:
        if path == '': print('GitHub scopes: ' + response.headers.get('X-OAuth-Scopes', 'fine-grained/app token'), flush=True)
        if response.status == 204: return None
        return json.load(response)

def digest(path):
    with path.open('rb') as stream: return 'sha256:' + hashlib.file_digest(stream, 'sha256').hexdigest()

def upload(url, path, name):
    target = urllib.parse.urlsplit(url.split('{')[0] + '?name=' + urllib.parse.quote(name))
    if target.hostname != 'uploads.github.com': raise RuntimeError('Unexpected upload host')
    connection = http.client.HTTPSConnection(target.hostname, timeout=120)
    try:
        connection.putrequest('POST', target.path + '?' + target.query)
        for key, value in headers.items(): connection.putheader(key, value)
        connection.putheader('Content-Type', 'application/octet-stream')
        connection.putheader('Content-Length', str(path.stat().st_size))
        connection.endheaders()
        sent = 0
        with path.open('rb') as stream:
            while chunk := stream.read(1024 * 1024):
                connection.send(chunk)
                sent += len(chunk)
                if sent % (16 * 1024 * 1024) == 0 or sent == path.stat().st_size:
                    print(f'Upload {name}: {sent}/{path.stat().st_size} bytes', flush=True)
        response = connection.getresponse()
        result = json.loads(response.read())
        if response.status != 201: raise RuntimeError(f'Upload failed ({response.status}): {result.get("message", "unknown error")}')
        return result
    finally: connection.close()

metadata = api('')
if not metadata.get('permissions', {}).get('push'): raise RuntimeError('GitHub account cannot publish to this repository.')
releases = api('/releases?per_page=100')
plan = []
old = ROOT/'recovery/previous-installers/oozys-Fortnite-Porter-Installer (beta 0.1.1).exe'
if old.exists(): plan.append(('0.1.1', old, None, None))
for folder in sorted((ROOT/'outputs').glob('oozys-Fortnite-Porter (beta *)')):
    if not folder.is_dir(): continue
    version = folder.name.split('(beta ')[1][:-1]
    installer = folder / f'oozys-Fortnite-Porter-Installer (beta {version}).exe'
    source = ROOT/'outputs'/f'oozys-Fortnite-Porter-Source (beta {version}).zip'
    if installer.exists(): plan.append((version, installer, folder, source if source.exists() else None))
if args.version: plan = [p for p in plan if p[0] in args.version]
print(json.dumps({'Repository': REPO, 'Versions': [p[0] for p in plan],
    'ExistingReleases': [r['tag_name'] for r in releases]}), flush=True)
if not args.publish: raise SystemExit(0)
archive_dir = ROOT/'outputs/github-assets'
archive_dir.mkdir(exist_ok=True)
receipt_path = ROOT/'recovery/github-release-receipts.json'
receipts = json.loads(receipt_path.read_text()) if receipt_path.exists() else {}
head = subprocess.check_output(['git','-C',str(ROOT/'work/github-docs'),'rev-parse','HEAD'],text=True).strip()
for version, installer, folder, source in plan:
    # Refresh between versions so concurrent current-build uploads are reused.
    releases = api('/releases?per_page=100')
    tag = 'beta-' + version
    notes = (ROOT/'docs/releases'/f'{tag}.md').read_text(encoding='utf-8')
    body = notes + '\n\n## Downloads\n\nThe installer includes the runtime. The portable ZIP, when supplied, contains the launcher and app folder; extract them together. Source backups are included where preserved. Older builds are archived as-is; use the newest beta for current fixes.\n'
    installer_name = f'oozys-Fortnite-Porter-Installer.beta.{version}.exe' if tuple(map(int, version.split('.'))) >= (0, 5, 0) else f'oozys-Fortnite-Porter-Installer-beta-{version}.exe'
    assets = [(installer, installer_name)]
    if folder:
        portable = archive_dir/f'oozys-Fortnite-Porter-beta-{version}-portable.zip'
        if not portable.exists():
            with zipfile.ZipFile(portable, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
                for file in sorted(folder.rglob('*')):
                    if file.is_file() and file != installer:
                        archive.write(file, folder.name + '/' + file.relative_to(folder).as_posix())
        with zipfile.ZipFile(portable) as archive:
            if archive.testzip(): raise RuntimeError('Portable archive integrity failure')
        assets.append((portable, portable.name))
    if source: assets.append((source, f'oozys-Fortnite-Porter-beta-{version}-source.zip'))
    checksum = archive_dir/f'SHA256SUMS-beta-{version}.txt'
    hashes = {name: digest(path) for path, name in assets}
    checksum.write_text(''.join(f'{value[7:]}  {name}\n' for name,value in hashes.items()),encoding='utf-8')
    assets.append((checksum, checksum.name)); hashes[checksum.name] = digest(checksum)
    release = next((r for r in releases if r['tag_name'] == tag), None)
    if release is None:
        release = api('/releases', 'POST', dict(tag_name=tag, target_commitish=head, name=f"oozy's Fortnite Porter beta {version}",body=body,draft=True,prerelease=True))
    remote_assets = api(f'/releases/{release["id"]}/assets?per_page=100')
    # An unpublished staging attempt used a URL-oriented alias. Keep only the requested EXE name.
    obsolete_alias = f'oozys-Fortnite-Porter-Installer-beta-{version}.exe'
    if release['draft'] and installer_name != obsolete_alias:
        for old_asset in remote_assets:
            if old_asset['name'] == obsolete_alias:
                api(f'/releases/assets/{old_asset["id"]}', 'DELETE')
        remote_assets = [a for a in remote_assets if a['name'] != obsolete_alias]

    for path, name in assets:
        remote = next((a for a in remote_assets if a['name'] == name), None)
        if remote and release['draft'] and (remote.get('state') != 'uploaded' or remote.get('digest') not in (None, hashes[name]) or remote.get('size') != path.stat().st_size):
            api(f'/releases/assets/{remote["id"]}', 'DELETE')
            remote = None
        if remote is None:
            print('Uploading ' + tag + ' / ' + name, flush=True)
            remote = upload(release['upload_url'], path, name)
        if remote['size'] != path.stat().st_size or remote.get('digest') not in (None, hashes[name]):
            raise RuntimeError('Remote asset does not match local build: ' + name)
    if release['draft'] and not args.draft_only:
        release = api(f'/releases/{release["id"]}', 'PATCH',dict(draft=False,prerelease=True,make_latest='false',target_commitish=head,body=body))
    receipts = json.loads(receipt_path.read_text()) if receipt_path.exists() else {}
    receipts[tag] = dict(URL=release['html_url'], Assets=[dict(Name=n,Size=p.stat().st_size,SHA256=hashes[n]) for p,n in assets])
    receipt_path.write_text(json.dumps(receipts,indent=2),encoding='utf-8')
    print(('Staged draft ' if release['draft'] else 'Published ') + release['html_url'], flush=True)
