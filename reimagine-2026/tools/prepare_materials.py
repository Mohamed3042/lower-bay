"""Extract only the licensed 4K PBR maps needed by the Unity scene."""
import hashlib
import json
from pathlib import Path
import zipfile
from PIL import Image

ROOT=Path(__file__).resolve().parents[2]
receipts=[]
for asset in ('Tiles008','Concrete048','Concrete033'):
    source=ROOT/'reimagine-2026/local'/(asset+'_4K-JPG.zip')
    folder=ROOT/'playable/unity/Assets/LowerBay2026/Surfaces'/asset
    folder.mkdir(parents=True,exist_ok=True)
    row={'id':asset,'source':'https://ambientcg.com/view?id='+asset,
         'download':'https://ambientcg.com/get?file='+source.name,
         'license':'CC0-1.0','licenseUrl':'https://docs.ambientcg.com/license/',
         'archiveSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'maps':[]}
    with zipfile.ZipFile(source) as archive:
        for role in ('Color','NormalGL','Roughness','AmbientOcclusion'):
            name=asset+'_4K-JPG_'+role+'.jpg'
            target=folder/(role+'.jpg')
            target.write_bytes(archive.read(name))
            with Image.open(target) as image:
                assert image.size==(4096,4096), (name,image.size)
            row['maps'].append({'role':role,'file':target.relative_to(ROOT).as_posix(),
                               'width':4096,'height':4096,'sha256':hashlib.sha256(target.read_bytes()).hexdigest()})
        preview=ROOT/'reimagine-2026/materials'/(asset+'-preview.png')
        preview.parent.mkdir(parents=True,exist_ok=True)
        preview.write_bytes(archive.read(asset+'.png'))
    receipts.append(row)
(ROOT/'reimagine-2026/evidence/materials-provenance.json').write_text(json.dumps(receipts,indent=2),encoding='utf-8')
print(json.dumps({'status':'PASS','materials':len(receipts),'native4KMaps':sum(len(r['maps']) for r in receipts)}))
