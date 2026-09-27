"""Validate generated Blender/GLB/render receipts without opening any user scene."""
import argparse
import hashlib
import json
import pathlib
import struct

from PIL import Image


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', required=True, help='Completed builder output directory.')
    parser.add_argument('--receipt', required=True, help='New validation JSON path; never overwritten.')
    parser.add_argument('--map', help='Optional preserved source snapshot, otherwise build-report source_map.')
    args = parser.parse_args()
    folder, receipt = pathlib.Path(args.output).resolve(), pathlib.Path(args.receipt).resolve()
    if receipt.exists():
        raise FileExistsError('Validation receipt exists; choose a new path.')
    build = json.loads((folder / 'build-report.json').read_text(encoding='utf-8'))
    source = pathlib.Path(args.map).resolve() if args.map else (folder / build['source_map']).resolve()
    source_hash = sha256(source)
    if source_hash != build['source_sha256']:
        raise AssertionError('Source snapshot SHA-256 differs from the map consumed by Blender.')
    if build['coordinate_error_count']:
        raise AssertionError('Builder reported coordinate errors.')
    for name, expected_size in build['outputs'].items():
        if (folder / name).stat().st_size != expected_size:
            raise AssertionError('Artifact byte count differs: ' + name)
    data = (folder / 'arena.glb').read_bytes()
    magic, version, length = struct.unpack_from('<4sII', data)
    if magic != b'glTF' or version != 2 or length != len(data):
        raise AssertionError('Invalid GLB header or length.')
    json_length, chunk_type = struct.unpack_from('<II', data, 12)
    if chunk_type != 0x4E4F534A:
        raise AssertionError('Missing GLB JSON chunk.')
    gltf = json.loads(data[20:20 + json_length])
    binary_header = 20 + json_length
    binary_length, binary_type = struct.unpack_from('<II', data, binary_header)
    if binary_type != 0x004E4942 or binary_header + 8 + binary_length != len(data):
        raise AssertionError('Invalid GLB binary chunk.')
    for image in gltf.get('images', []):
        if 'uri' in image or 'bufferView' not in image:
            raise AssertionError('GLB image is not embedded.')
        view = gltf['bufferViews'][image['bufferView']]
        if view.get('buffer', 0) != 0 or view.get('byteOffset', 0) + view['byteLength'] > binary_length:
            raise AssertionError('Embedded image buffer is outside the GLB binary chunk.')
    if len(gltf.get('images', [])) != len(build['embedded_textures']):
        raise AssertionError('Exported image count differs from consumed textures.')
    interpolations = sorted({sampler.get('interpolation', 'LINEAR') for animation in gltf.get('animations', []) for sampler in animation['samplers']})
    if build['dynamics'] and (not gltf.get('animations') or interpolations != ['LINEAR']):
        raise AssertionError('Expected train LINEAR animation was not exported.')
    renders = {}
    for render in build['renders']:
        image_path = folder / render['path']
        with Image.open(image_path) as image:
            dimensions = list(image.size)
            image.verify()
        if dimensions != [render['width'], render['height']]:
            raise AssertionError('Rendered dimensions differ: ' + render['path'])
        renders[render['path']] = {'dimensions': dimensions, 'bytes': image_path.stat().st_size, 'sha256': sha256(image_path)}
    result = {'status': 'VERIFIED', 'source_sha256': source_hash, 'blender': build['blender'], 'entities': build['entities'],
              'coordinate_error_count': 0, 'glb_images': len(gltf.get('images', [])), 'glb_animations': len(gltf.get('animations', [])),
              'glb_animation_interpolation': interpolations, 'renders': renders,
              'artifacts': {name: {'bytes': (folder / name).stat().st_size, 'sha256': sha256(folder / name)} for name in build['outputs']},
              'scope': 'Artifact structure, dimensions and source linkage. Human/model visual review and game runtime acceptance are separate.'}
    receipt.parent.mkdir(parents=True, exist_ok=True)
    receipt.write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result))


if __name__ == '__main__':
    main()
