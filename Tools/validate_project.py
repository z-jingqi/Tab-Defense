#!/usr/bin/env python3
"""Offline structural checks. This does NOT compile C# or substitute for Unity tests."""
from pathlib import Path
import json
import re
import sys
import yaml
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
ASSETS=ROOT/"Assets"
results=[]


def check(name,condition):
    if not condition:raise AssertionError(name)
    results.append(name)


def documents(path):
    text=path.read_text()
    ids=re.findall(r"^--- !u!\d+ &(-?\d+)",text,re.M)
    clean=re.sub(r"^%.*\n","",text,flags=re.M)
    clean=re.sub(r"^--- !u!\d+ &-?\d+","---",clean,flags=re.M)
    parsed=list(yaml.safe_load_all(clean))
    check("Unique local file IDs: "+path.name,len(ids)==len(set(ids)) and len(ids)==len(parsed))
    return {int(identifier):next(iter(doc.values())) for identifier,doc in zip(ids,parsed)}


def walk(value):
    if isinstance(value,dict):
        if "fileID"in value:yield value
        for child in value.values():yield from walk(child)
    elif isinstance(value,list):
        for child in value:yield from walk(child)


def balanced_csharp(text):
    # Delimiter scanner with strings/comments removed, intentionally not advertised as a compiler.
    clean=re.sub(r'@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|//[^\n]*|/\*[\s\S]*?\*/',"",text)
    stack=[];pairs={")":"(","]":"[","}":"{"}
    for char in clean:
        if char in "([{":stack.append(char)
        elif char in pairs:
            if not stack or stack.pop()!=pairs[char]:return False
    return not stack


def main():
    guid_paths={}
    for path in ASSETS.rglob("*"):
        if path.suffix==".meta":continue
        meta=Path(str(path)+".meta")
        check("Asset has meta: "+str(path.relative_to(ASSETS)),meta.exists())
        identifier=yaml.safe_load(meta.read_text())["guid"]
        check("Unique GUID: "+path.name,identifier not in guid_paths)
        guid_paths[identifier]=path
    parsed={}
    for path in ASSETS.rglob("*"):
        if path.suffix in {".unity",".prefab",".asset"}:parsed[path]=documents(path)
    for path,objects in parsed.items():
        for object_id,value in objects.items():
            for reference in walk(value):
                file_id=reference["fileID"]
                if file_id==0:continue
                if "guid"in reference:
                    identifier=reference["guid"]
                    if identifier.startswith("0000000000000000"):continue
                    check("External GUID resolves: "+path.name+" -> "+identifier,identifier in guid_paths)
                    target=guid_paths[identifier]
                    if target in parsed:check("Referenced object exists: "+target.name,file_id in parsed[target])
                    elif target.suffix==".cs":check("MonoScript ID: "+target.name,file_id==11500000)
                    elif target.suffix==".png":check("Sprite ID: "+target.name,file_id==21300000)
                else:check("Local reference resolves: "+path.name+" #"+str(file_id),file_id in objects)
            if "m_Component"in value:
                for item in value["m_Component"]:
                    component=objects[item["component"]["fileID"]]
                    check("Component points to owner: "+path.name,component.get("m_GameObject",{}).get("fileID")==object_id)
    config=parsed[ASSETS/"Data/GameConfig.asset"][11400000]
    check("No wave configuration remains", "waves" not in config)
    spawn=config['spawning']
    check("Valid endless spawning",spawn['spawnInterval']>0 and 0<spawn['maxAlive']<=config['enemyPoolSize'])
    check("Valid bullets",config['bulletSpeed']>0 and config['bulletRadius']>0 and config['bulletPoolSize']>0)
    check("Four unique elements",len(config['elements'])==4 and {e['kind'] for e in config['elements']}=={0,1,2,3})
    for e in config['elements']:
        check("Valid element charges "+str(e['kind']),e['capacity']>0 and e['rechargeSeconds']>0 and e['radius']>0 and 1<=e['lightningTargets']<=8)
    check("Arrow sprite assigned",config['arrowSprite']['fileID']==21300000)
    check("Bounded burst pool",config['pendingBurstLimit']>0 and config['progression']['maxArrows']<=config['bulletPoolSize'])
    check("Four enemy archetypes",len(config['enemies'])==4)
    for path in (ASSETS/"Art").glob("*.png"):
        image=Image.open(path);image.verify()
        check("Valid RGBA sprite: "+path.name,image.mode=="RGBA"and image.size==(128,128))
    for path in ASSETS.rglob("*.cs"):
        check("C# delimiter scan: "+path.name,balanced_csharp(path.read_text()))
    core="\n".join(p.read_text()for p in (ASSETS/"Scripts/Core").glob("*.cs"))
    check("Engine-free core",not re.search(r"\bUnity(Engine|Editor)\b",core))
    asmdefs=[json.loads(path.read_text())for path in ASSETS.rglob("*.asmdef")]
    names={a["name"]for a in asmdefs}
    check("Five distinct assemblies",len(names)==len(asmdefs)==5)
    for assembly in asmdefs:
        check("Assembly references: "+assembly["name"],all(r in names or r=="Unity.ugui"for r in assembly.get("references",[])))
    settings=next(iter(documents(ROOT/"ProjectSettings/ProjectSettings.asset").values()))
    check("Portrait and legacy input configured",settings["defaultScreenOrientation"]==1 and settings["activeInputHandler"]==0)
    check("IL2CPP mobile backends",settings["scriptingBackend"]["iPhone"]==settings["scriptingBackend"]["Android"]==1)
    check("No app internet/storage permission requested",settings["forceInternetPermission"]==settings["forceSDCardPermission"]==0)
    axes=next(iter(documents(ROOT/"ProjectSettings/InputManager.asset").values()))["m_Axes"]
    check("Legacy UI input axes exist",{axis["m_Name"]for axis in axes}>={"Horizontal","Vertical","Submit","Cancel"})
    deps=json.loads((ROOT/"Packages/manifest.json").read_text())["dependencies"]
    check("Minimal Unity-only packages",all(name.startswith("com.unity.")for name in deps) and not any(any(term in name for term in ["ads","purchasing","services","wechat"])for name in deps))
    build=next(iter(documents(ROOT/"ProjectSettings/EditorBuildSettings.asset").values()))
    check("Main scene enabled in build",len(build["m_Scenes"])==1 and build["m_Scenes"][0]["enabled"]==1 and build["m_Scenes"][0]["path"]=="Assets/Scenes/Main.unity")
    summary={"status":"PASS","structural_assertions":len(results),"csharp_files":len(list(ASSETS.rglob('*.cs'))),"mode":"endless vertical shooting","unity_compilation":"NOT RUN - Unity Editor unavailable","unity_tests":"NOT RUN - Unity Editor unavailable","ios_export_and_xcode":"NOT RUN - macOS/Xcode unavailable","device_playtest":"NOT RUN"}
    output=ROOT/"Docs/StaticCheckResults.json";output.write_text(json.dumps(summary,ensure_ascii=False,indent=2)+"\n")
    print(json.dumps(summary,ensure_ascii=False,indent=2))


if __name__=="__main__":
    try:main()
    except Exception as error:
        print("FAIL: "+str(error),file=sys.stderr);raise
