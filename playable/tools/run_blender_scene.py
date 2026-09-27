"""Build the map in a fresh background Blender, without machine-local helpers."""
import argparse
import pathlib
import shutil
import subprocess
import sys

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--blender', default=shutil.which('blender'))
parser.add_argument('--timeout', type=int, default=1200)
args, remaining = parser.parse_known_args()
if not args.blender:
    parser.error('Pass --blender /path/to/blender or put Blender on PATH.')
script = pathlib.Path(__file__).with_name('build_blender_scene.py').resolve()
command = [args.blender, '--background', '--factory-startup', '--python-exit-code', '1',
           '--python', str(script), '--', *remaining]
result = subprocess.run(command, timeout=args.timeout, check=False)
sys.exit(result.returncode)
