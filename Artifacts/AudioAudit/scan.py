from pathlib import Path
import re,json,collections
root=Path(__file__).resolve().parents[2]
out=root/'Artifacts/AudioAudit'
assets=root/'Assets'
files=list(assets.rglob('*'))
guidmap={}
for p in files:
 if p.suffix=='.meta':
  m=re.search(r'^guid: (\w+)',p.read_text(errors='ignore'),re.M)
  if m: guidmap[m[1]]=str(p.relative_to(root))[:-5].replace('\\','/')
texts={str(p.relative_to(root)).replace('\\','/'):p.read_text(errors='ignore') for p in files if p.suffix in {'.unity','.prefab','.asset','.mixer','.controller','.anim','.cs'}}
refs=collections.defaultdict(list)
for p,t in texts.items():
 for n,line in enumerate(t.splitlines(),1):
  for g in set(re.findall(r'guid: ([a-f0-9]{32})',line)): refs[g].append(f'{p}:{n}: {line.strip()}')
extensions={'.wav','.ogg','.mp3','.aif','.aiff','.flac','.xm','.mod','.it','.s3m','.aifc','.aac','.m4a'}
clips=[]
reverse={p:g for g,p in guidmap.items()}
for p in files:
 rel=str(p.relative_to(root)).replace('\\','/')
 if p.suffix.lower() in extensions or (p.suffix=='.meta' and 'AudioImporter:' in p.read_text(errors='ignore')):
  if p.suffix=='.meta': rel=rel[:-5]
  if any(c['path']==rel for c in clips): continue
  g=reverse.get(rel,'')
  clips.append({'path':rel,'guid':g,'references':refs[g]})
scenes=re.findall(r'    path: (.*)',(root/'ProjectSettings/EditorBuildSettings.asset').read_text())[:3]
reachable=set(scenes); queue=list(scenes)
while queue:
 p=queue.pop()
 for g in set(re.findall(r'guid: ([a-f0-9]{32})',texts.get(p,''))):
  q=guidmap.get(g)
  if q and q not in reachable: reachable.add(q);queue.append(q)
for c in clips: c['build_scene_dependency']=c['path'] in reachable
(out/'inventory.json').write_text(json.dumps(clips,ensure_ascii=False,indent=2),encoding='utf-8')
lines=['# Audio inventory — 2026-09-12','All AudioImporter assets and audio extensions under Assets. References include inactive/legacy assets; a reference is not proof of playback.','']
for c in clips:
 lines+=['## '+c['path'],f"GUID: {c['guid']}; build-scene dependency: {c['build_scene_dependency']}"]
 lines+=['- '+s for s in c['references']] or ['- NO SERIALIZED REFERENCES']
(out/'inventory.md').write_text('\n'.join(lines),encoding='utf-8')
sources=[]
for p,t in texts.items():
 if p.endswith(('.unity','.prefab')):
  for block in t.split('--- !u!'):
   if block.startswith('82 '): sources.append({'path':p,'block':block})
(out/'audio-sources.json').write_text(json.dumps(sources,indent=2),encoding='utf-8')
print('CLIPS',len(clips),'UNREFERENCED',sum(not c['references'] for c in clips),'SCENE DEPENDENCIES',sum(c['build_scene_dependency'] for c in clips),'SOURCES',len(sources))
print('FORMATS',dict(collections.Counter(Path(c['path']).suffix for c in clips)))
print('REFERENCED CLIPS')
for c in clips:
 if c['references']: print(c['path'],len(c['references']),'scene dependency',c['build_scene_dependency'])
print('PRODUCTION SOURCES',dict(collections.Counter(s['path'] for s in sources if s['path'] in reachable)))
(out/'reachable.json').write_text(json.dumps(sorted(reachable),indent=2))
