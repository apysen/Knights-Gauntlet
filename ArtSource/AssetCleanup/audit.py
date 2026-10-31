from pathlib import Path
from collections import Counter, defaultdict
import json, re

PROJECT = Path(__file__).resolve().parents[2]
ASSETS = PROJECT / 'Assets'
OUT = Path(__file__).parent
keep = {p.strip() for p in (OUT / 'dependencies.txt').read_text(encoding='utf-8-sig').splitlines()}
roots = {p.strip() for p in (OUT / 'roots.txt').read_text(encoding='utf-8-sig').splitlines()}
files = [p for p in ASSETS.rglob('*') if p.is_file() and p.suffix != '.meta']
paths = {p.relative_to(PROJECT).as_posix(): p for p in files}

# Asset paths used by editor utilities are also supported workflows.
for p in files:
    if p.suffix != '.cs' or p.name == 'ConceptKnight3DBuilder.cs': continue
    text = p.read_text(encoding='utf-8-sig', errors='replace')
    for literal in re.findall(r'"(Assets/[^"\n]+)"', text):
        for name in paths:
            if name == literal or (literal.endswith('/') and name.startswith(literal)):
                keep.add(name)

# Keep source/build tooling, native plugins, and notices shipped with retained packs.
protected_prefixes = ('Assets/Plugins/', 'Assets/ExternalDependencyManager/', 'Assets/GooglePlayPlugins/', 'Assets/LevelPlay/')
for name in paths:
    if name.startswith(protected_prefixes): keep.add(name)

for name, p in paths.items():
    if re.search(r'license|licence|copyright|notice|readme|changelog|credits', p.name, re.I):
        pack = '/'.join(name.split('/')[:2]) + '/'
        if any(k.startswith(pack) for k in keep): keep.add(name)

unused = sorted(set(paths) - keep)
discarded = {'Assets/Scripts/View/PixelKnight3D.cs'}
keep -= discarded
unused = sorted(set(paths) - keep)
rows = [{'path': name, 'bytes': paths[name].stat().st_size} for name in unused]
removed_guids = {}
for name in unused:
    meta = Path(str(paths[name]) + '.meta')
    if meta.exists():
        match = re.search(r'^guid: ([a-fA-F0-9]{32})', meta.read_text(encoding='utf-8-sig', errors='replace'), re.M)
        if match: removed_guids[match[1]] = name
incoming = []
for name in keep:
    if name not in paths: continue
    for p in (paths[name], Path(str(paths[name])+'.meta')):
        if not p.exists(): continue
        with p.open('rb') as f: header=f.read(256)
        if b'\0' in header: continue
        if p.suffix.lower() in {'.png','.jpg','.ttf','.otf','.wav','.ogg','.fbx','.dll','.aar','.pdf','.docx','.zip'}: continue
        text = p.read_text(encoding='utf-8-sig', errors='replace')
        for guid in set(re.findall(r'guid:\s*([a-fA-F0-9]{32})', text)) & removed_guids.keys():
            incoming.append({'from':p.relative_to(PROJECT).as_posix(),'to':removed_guids[guid]})
(OUT / 'incoming-references.json').write_text(json.dumps(incoming, indent=2), encoding='utf-8')
if incoming: raise RuntimeError(f'{len(incoming)} references from retained assets to deletion candidates; inspect manifest before deletion')
(OUT / 'candidates.json').write_text(json.dumps(rows, indent=2), encoding='utf-8')
(OUT / 'kept.txt').write_text('\n'.join(sorted(keep)), encoding='utf-8')
summary = defaultdict(lambda: [0,0,0,0])
for name,p in paths.items():
    key = '/'.join(name.split('/')[:2])
    group = summary[key]
    group[0] += 1; group[1] += p.stat().st_size
    if name in unused: group[2] += 1; group[3] += p.stat().st_size
for key,(count,size,n,amount) in sorted(summary.items()):
    print(f'{key}: {n}/{count} unused files; {amount/1048576:.1f}/{size/1048576:.1f} MiB')
print(f'TOTAL: {len(rows)}/{len(files)} unused; {sum(r["bytes"] for r in rows)/1048576:.1f} MiB')
