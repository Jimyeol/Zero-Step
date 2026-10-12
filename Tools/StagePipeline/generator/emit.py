"""Write out/Stages, index.json, solutions.json, mapping.csv from assembly.json, with a full C-solver re-check."""
import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import json, csv, math, collections, copy
import zs, zs_c, abl, pipeline, assemble
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'out')
seq = {int(k): v for k, v in json.load(open('assembly.json')).items()}
CFG = {"mode": "Normal", "difficulty": "Normal", "spotlightRadius": 2.5, "showGridLines": False}

def ordered(i, s):
    o = collections.OrderedDict()
    o['stageID'] = i
    o['width'] = s['width']; o['height'] = s['height']
    o['startPoint'] = collections.OrderedDict([('x', s['startPoint']['x']), ('y', s['startPoint']['y'])])
    o['config'] = s.get('config') or copy.deepcopy(CFG)
    o['cells'] = s['cells']
    if s.get('walls'):
        o['walls'] = [collections.OrderedDict([('x', w['x']), ('y', w['y']), ('dir', w['dir'])]) for w in s['walls']]
    return o

stages = {i: json.loads(json.dumps(ordered(i, seq[i]['stage']))) for i in range(1, 1001)}
# ---- full re-check with the C solver
res = zs_c.run([(str(i), s, {'limit': 2_000_000}) for i, s in stages.items()])
bad = [i for i in stages if not (res[str(i)]['complete'] and res[str(i)]['solvable'])]
assert not bad, bad
# solutions: single stroke (no finger lift) ending with the plain clear (total == 1), never via the TwinLink-group clear
nr = zs_c.run([(str(i), s, {'limit': 2_000_000, 'norelease': True, 'plainwin': True}) for i, s in stages.items()])
assert all(nr[str(i)]['solvable'] for i in stages), [i for i in stages if not nr[str(i)]['solvable']]
sols = {}
for i, s in stages.items():
    r = res[str(i)]
    path, rel = r['path'], r['released']
    if str(i) in nr and nr[str(i)].get('solvable'):
        path, rel = nr[str(i)]['path'], nr[str(i)]['released']
    strokes = zs.to_strokes(path, rel)
    rp = zs.replay_strokes(s, strokes)
    assert rp['ok'], (i, rp)
    sols[str(i)] = strokes
ab = abl.run_ablation({str(i): s for i, s in stages.items()}, {str(i): res[str(i)] for i in stages})
abl_fail = {i: [(l, v) for l, k, v, st in ab[str(i)] if v is not True] for i in stages}
abl_fail = {i: v for i, v in abl_fail.items() if v}
bo_items = []
for i, s in stages.items():
    S = zs.Stage(s)
    bts = [k for k in range(S.n) if S.type[k] == 'Blackout']
    if bts:
        bo_items.append((str(i), s, {'limit': 4_000_000, 'bo_deadline': (bts, int(math.floor(0.6 * res[str(i)]['min_len'])))}))
bo = zs_c.run(bo_items)
bo_fail = [int(k) for k, x in bo.items() if not (x['complete'] and x['solutions'] == res[k]['solutions'])]
print('ablation failures', len(abl_fail), list(abl_fail.items())[:10])
print('blackout failures', bo_fail)
# ---- write
os.makedirs(f'{OUT}/Stages', exist_ok=True)
for f in os.listdir(f'{OUT}/Stages'):
    if f.startswith('stage_'): os.remove(f'{OUT}/Stages/{f}')
for i, s in stages.items():
    with open(f'{OUT}/Stages/stage_{i}.json', 'w', encoding='utf-8') as f:
        json.dump(s, f, indent=2, ensure_ascii=False)
with open(f'{OUT}/Stages/index.json', 'w', encoding='utf-8') as f:
    json.dump([f'stage_{i}.json' for i in range(1, 1001)], f, indent=2)
with open(f'{OUT}/solutions.json', 'w', encoding='utf-8') as f:
    f.write('{\n' + ',\n'.join(f'  "{i}": ' + json.dumps(sols[str(i)], separators=(',', ':')) for i in range(1, 1001)) + '\n}\n')
rows = []
for i, s in stages.items():
    r = res[str(i)]; rec = seq[i]
    m = pipeline.metrics(s, r)
    c = (i - 1) // 50 + 1
    rows.append({'stage': i, 'chapter': c, 'theme': assemble.CH[c]['name'], 'provenance': rec['prov'],
                 'notes': '; '.join(rec.get('notes') or []), 'w': m['w'], 'h': m['h'], 'tiles': m['tiles'], 'sum': m['sum'],
                 'p_random': repr(r['p_random']), 'neglog10_p': abs(round(m['D'], 4)), 'crit_steps': round(r['crit_steps'], 4),
                 'forced_ratio': round(r['forced_ratio'], 4), 'solutions': (str(int(r['solutions'])) if r['solutions'] < 1e6 else '1000000+'),
                 'states': r['states'], 'types': '+'.join(m['types']) or 'Normal', 'walls': m['walls'],
                 'rule_mechanics': len(m['rules']), 'outline_rect': int(m['rect']), 'strokes': len(sols[str(i)]),
                 'target_D': round(assemble.target(i), 3)})
with open(f'{OUT}/mapping.csv', 'w', newline='', encoding='utf-8') as f:
    w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
    w.writeheader(); w.writerows(rows)
json.dump({'abl_fail': {str(k): v for k, v in abl_fail.items()}, 'bo_fail': bo_fail, 'rows': rows}, open('emit_check.json', 'w'))
print('written')
