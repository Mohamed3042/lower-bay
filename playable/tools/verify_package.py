"""Check that the Unity delivery contains the exact current map, images and scripts."""
import argparse
import hashlib
import json
import pathlib
import tarfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--receipt', required=True)
args = parser.parse_args()
root = pathlib.Path(__file__).resolve().parents[1]
output = pathlib.Path(args.receipt).resolve()
if output.exists():
    parser.error('Choose a new receipt path; existing evidence is preserved.')
package = root.parent / 'export/unitypackage/LowerBay_Playable.unitypackage'
digest = lambda value: hashlib.sha256(value).hexdigest()
with tarfile.open(package, 'r:gz') as archive:
    members = {entry.name: entry for entry in archive.getmembers()}
    paths = {}
    for name, entry in members.items():
        if name.endswith('/pathname'):
            relative = archive.extractfile(entry).read().decode('utf-8').strip()
            if not relative.startswith('Assets/') or '..' in pathlib.PurePosixPath(relative).parts:
                raise AssertionError('Unsafe package asset path: ' + relative)
            asset = name[:-len('pathname')] + 'asset'
            if asset in members:
                paths[relative] = archive.extractfile(members[asset]).read()
    source = (root / 'output/lower-bay.strikemap.json').read_bytes()
    shipped_sources = [data for name, data in paths.items() if name.endswith('/source.strikemap.json')]
    assert len(shipped_sources) == 1 and shipped_sources[0] == source, 'Package source does not match current map.'
    textures = {}
    for entry in json.loads((root / 'generated/ASSET-RECEIPT.json').read_text(encoding='utf-8')):
        included = [data for name, data in paths.items() if name.endswith('/textures/' + entry['file'])]
        assert len(included) == 1 and digest(included[0]) == entry['sha256'], entry['file']
        textures[entry['file']] = entry['sha256']
    scripts = {}
    for path in (root / 'unity/Assets/StrikeMapStudio').rglob('*.cs'):
        if 'Maps' in path.parts:
            continue
        name = path.relative_to(root / 'unity').as_posix()
        assert paths.get(name) == path.read_bytes(), 'Missing or stale script: ' + name
        scripts[name] = digest(path.read_bytes())
    assert 'Assets/LowerBayReview/Resources/LowerBayRoutes.json' in paths, 'Runtime fixture absent from delivery.'
    result = {'status': 'PASS', 'source_sha256': digest(source), 'package_sha256': digest(package.read_bytes()),
              'package_bytes': package.stat().st_size, 'asset_count': len(paths), 'textures': textures, 'scripts': scripts,
              'scope': 'Exact Unity package bytes, embedded map, textures, script sources and route fixture; actual runtime acceptance is in unity-runtime.json.'}
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({key: result[key] for key in ('status', 'package_bytes', 'asset_count', 'source_sha256')}))
