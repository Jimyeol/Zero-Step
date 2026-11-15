"""Offline compile check for ZeroStep using Unity's bundled Roslyn (no Unity launch, ~20s).
usage: python3 -I compile.py <project_root> [editor|player-android|player-ios|all]
Needs the csproj files and Library/ScriptAssemblies that the Unity editor generates.
"""
import os, re, subprocess, sys, html, glob, tempfile
ROOT = sys.argv[1]
MODE = sys.argv[2] if len(sys.argv) > 2 else 'all'
U = '/Applications/Unity/Hub/Editor/6000.3.5f2/Unity.app/Contents/Resources/Scripting'
DOTNET = f'{U}/NetCoreRuntime/dotnet'
CSC = f'{U}/DotNetSdkRoslyn/csc.dll'
OUT = os.environ.get('ZS_COMPILE_OUT') or os.path.join(tempfile.gettempdir(), 'zerostep_compile_out')
os.makedirs(OUT, exist_ok=True)
SA = os.path.join(ROOT, 'Library/ScriptAssemblies')

def parse(csproj):
    t = open(os.path.join(ROOT, csproj), encoding='utf-8').read()
    comp = [html.unescape(c) for c in re.findall(r'<Compile Include="([^"]+)"', t)]
    is_editor = 'Editor' in csproj
    # scripts created after the csproj was generated
    for f in glob.glob(os.path.join(ROOT, 'Assets/Scripts/**/*.cs'), recursive=True):
        rel = os.path.relpath(f, ROOT)
        if ('/Editor/' in rel) == is_editor and rel not in comp:
            comp.append(rel)
    comp = [c for c in comp if os.path.exists(os.path.join(ROOT, c))]
    refs = [html.unescape(r) for r in re.findall(r'<HintPath>([^<]+)</HintPath>', t)]
    defs = re.search(r'<DefineConstants>([^<]+)</DefineConstants>', t).group(1).split(';')
    lang = re.search(r'<LangVersion>([^<]+)</LangVersion>', t).group(1)
    projrefs = re.findall(r'<ProjectReference Include="([^"]+)\.csproj"', t)
    return comp, refs, defs, lang, projrefs

def run(name, csproj, defs_mut, out_name, override_refs=None):
    comp, refs, defs, lang, projrefs = parse(csproj)
    defs = defs_mut(defs)
    extra = [(override_refs or {}).get(p) or os.path.join(SA, p + '.dll') for p in projrefs]
    refs = [r for r in refs if os.path.basename(r)[:-4] not in projrefs] + extra
    rsp = os.path.join(OUT, out_name + '.rsp')
    out_dll = os.path.join(OUT, out_name + '.dll')
    with open(rsp, 'w') as f:
        f.write('-nologo -noconfig -nostdlib+ -target:library -unsafe -nowarn:0169,0649,0414,0618,0067,0168,0219,0162,0429,0108,0114\n')
        f.write(f'-langversion:{lang}\n-out:"{out_dll}"\n-define:{";".join(d for d in defs if d)}\n')
        for r in refs:
            f.write(f'-r:"{r}"\n')
        for c in comp:
            f.write(f'"{os.path.join(ROOT, c)}"\n')
    p = subprocess.run([DOTNET, CSC, '@' + rsp], capture_output=True, text=True)
    lines = (p.stdout + p.stderr).splitlines()
    errs = [l for l in lines if ' error ' in l]
    warns = [l for l in lines if ' warning CS' in l and '/Assets/Scripts/' in l]
    print(f'== {name}: {"OK" if p.returncode == 0 else "FAIL"}  errors={len(errs)} script-warnings={len(warns)}')
    for l in (errs + warns)[:30]:
        print('   ', l.replace(ROOT + '/', ''))
    return out_dll if p.returncode == 0 else None

EDITOR_DEFS = ('UNITY_EDITOR', 'UNITY_EDITOR_64', 'UNITY_EDITOR_OSX', 'UNITY_EDITOR_WIN', 'UNITY_EDITOR_LINUX', 'UNITY_STANDALONE_OSX',
               'UNITY_STANDALONE', 'UNITY_ANDROID', 'UNITY_IOS', 'UNITY_IPHONE', 'PLATFORM_STANDALONE_OSX', 'PLATFORM_STANDALONE',
               'PLATFORM_ANDROID', 'PLATFORM_IOS')

def player(defs, plat):
    out = [d for d in defs if d not in EDITOR_DEFS]
    return out + (['UNITY_ANDROID', 'PLATFORM_ANDROID'] if plat == 'android' else ['UNITY_IOS', 'UNITY_IPHONE', 'PLATFORM_IOS'])

if MODE in ('editor', 'all'):
    main = run('Assembly-CSharp(editor)', 'Assembly-CSharp.csproj', lambda d: d, 'main_editor')
    if main:
        run('Assembly-CSharp-Editor', 'Assembly-CSharp-Editor.csproj', lambda d: d, 'editor', {'Assembly-CSharp': main})
if MODE in ('player-android', 'all'):
    run('Assembly-CSharp(player-android)', 'Assembly-CSharp.csproj', lambda d: player(d, 'android'), 'main_android')
if MODE in ('player-ios', 'all'):
    run('Assembly-CSharp(player-ios)', 'Assembly-CSharp.csproj', lambda d: player(d, 'ios'), 'main_ios')
