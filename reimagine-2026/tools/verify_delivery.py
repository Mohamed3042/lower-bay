"""Verify exact 2026 package assets and native-player evidence without Unity."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import struct
import tarfile

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--package', type=Path, required=True)
parser.add_argument('--runtime', type=Path, required=True)
parser.add_argument('--receipt', type=Path, required=True)
args = parser.parse_args()
if args.receipt.exists():
    parser.error('Choose a new receipt path; existing proof is preserved.')


def sha(data):
    return hashlib.sha256(data).hexdigest()


expected = {}
unity = ROOT / 'playable/unity'
for base in [unity / 'Assets/LowerBay2026', unity / 'Assets/StrikeMapStudio']:
    for path in base.rglob('*'):
        if not path.is_file() or 'Maps' in path.parts:
            continue
        if path.suffix in {'.png', '.jpg', '.fbx', '.cs', '.shader', '.asmdef'}:
            expected[path.relative_to(unity).as_posix()] = sha(path.read_bytes())

packaged, sources, scenes, rows = {}, [], [], {}
# Unity places pathname records after some large assets. Stream once rather than
# repeatedly rewinding and inflating a several-hundred-megabyte gzip archive.
with tarfile.open(args.package, 'r|gz') as archive:
    for entry in archive:
        assert entry.isfile() or entry.isdir(), 'Unexpected package link or special file'
        if not entry.isfile():
            continue
        parts = PurePosixPath(entry.name).parts
        assert len(parts) == 2, 'Unexpected archive structure'
        row = rows.setdefault(parts[0], {})
        if parts[1] == 'pathname':
            row['path'] = archive.extractfile(entry).read().decode('utf-8').strip()
        elif parts[1] == 'asset':
            data = archive.extractfile(entry).read()
            row['sha256'] = sha(data)
            row['localPath'] = b'C:\\Users' in data or b'C:/Users' in data

for row in rows.values():
    path = row['path']
    assert path.startswith('Assets/') and '..' not in PurePosixPath(path).parts, path
    if 'sha256' not in row:
        continue
    assert path not in packaged, 'Duplicate package path: ' + path
    packaged[path] = row['sha256']
    if path.endswith('/source.strikemap.json'):
        sources.append(row['sha256'])
    if path.endswith('.unity'):
        scenes.append(path)
    if path.endswith('.fbx'):
        assert not row['localPath'], 'Local FBX dependency: ' + path

for path, digest in expected.items():
    assert packaged.get(path) == digest, 'Missing or stale asset: ' + path
source_sha = sha((ROOT / 'playable/output/lower-bay.strikemap.json').read_bytes())
assert sources == [source_sha] and len(scenes) == 1, 'Package source or scene contract changed'
assert 'Assets/LowerBayReview/Resources/LowerBayRoutes.json' in packaged

runtime = json.loads((args.runtime / 'runtime.json').read_text(encoding='utf-8-sig'))
assert runtime['status'] == 'PASS' and runtime['sourceSha256'] == source_sha
assert len(runtime['routes']) == 45 and all(r['passed'] for r in runtime['routes'])
assert all(runtime[key] for key in ['carry', 'trappedRiderReset', 'hazardReset', 'reset', 'paused', 'dropCannotWalkBack', 'upperFloorClosed'])
assert runtime['native4KTextures'] == 24 and runtime['propInstances'] == 9 and runtime['lodLevels'] == 18
assert runtime['reflectionProbeCount'] == 7 and runtime['bakedProbeCount'] >= 180
captures = {}
assert len(runtime['screenshots']) == 13
for name in runtime['screenshots']:
    data = (args.runtime / name).read_bytes()
    assert data[:8] == b'\x89PNG\r\n\x1a\n' and struct.unpack_from('>II', data, 16) == (3840, 2160), name
    captures[name] = sha(data)

result = dict(status='PASS', sourceSha256=source_sha, packageSha256=sha(args.package.read_bytes()),
              packageBytes=args.package.stat().st_size, packagedAssets=len(packaged),
              matchedPreparedAssets=len(expected), scene=scenes[0], native4KMaps=24,
              runtimeReceiptSha256=sha((args.runtime/'runtime.json').read_bytes()),
              assets=expected, captures=captures,
              scope='Exact package/source/asset bytes and native runtime contracts. Visual acceptance is recorded separately.')
args.receipt.parent.mkdir(parents=True, exist_ok=True)
args.receipt.write_text(json.dumps(result, indent=2)+'\n', encoding='utf-8')
print(json.dumps({k: result[k] for k in ['status', 'packagedAssets', 'matchedPreparedAssets', 'packageBytes', 'packageSha256']}))
