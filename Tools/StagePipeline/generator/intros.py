import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import json, random, math, collections
import recipes, pipeline, zs
rng = random.Random(5)
SPEC = {
    'ShortCircuit': {'SC': (1, 1)}, 'CrossBlast': {'CB': (1, 1)}, 'FixedKnot': {'FK': (1, 1)},
    'TwinLink': {'TL': (1, 1)}, 'Igniter': {'IGN': (1, 1)}, 'BlindCurtain': {'BC': (1, 1)},
    'Blackout': {'BO': (1, 1)}, 'Wall': {'WALL': (1, 2)}, 'Charge': {'CHG': (1, 1)},
}
best = {}
for mech, mechs in SPEC.items():
    R = {'shapes': ['tiny'], 'n': (4, 9), 'mult': (1.0, 1.6), 'cap': [2, 3], 'mechs': mechs, 'straight': [0], 'hidden_max': 2, 'tl_size': [2], 'prefer_hits': 0.7}
    cands = {}
    for t in range(4000):
        s = recipes.build(rng, R)
        if s: cands[f'{mech}:{t}'] = s
    recs = pipeline.finalize(cands, strip=False)
    scored = []
    for cid, r in recs.items():
        if not r['ok']: continue
        m = r['metrics']; s = r['stage']
        ts = collections.Counter(c['type'] for c in s['cells'])
        if mech in ('BlindCurtain', 'Blackout'):
            if ts.get(mech, 0) != 1 or m['rules']: continue
        elif mech == 'Wall':
            if m['rules'] != ['Wall']: continue
        else:
            key = 'Igniter' if mech == 'Igniter' else mech
            if m['rules'] != [key]: continue
        sol = m['solutions']
        # score: small, a real (but gentle) decision, few solutions
        sc = 0
        sc += abs(m['sum'] - 8) * 0.3
        sc += 0 if 0.25 <= m['p_random'] <= 0.85 else 2
        sc += 0 if sol <= 2 else 1.5
        sc += 0 if m['tiles'] >= 6 else 1
        sc += 0 if m['crit_steps'] >= 1 else 1
        if mech == 'Blackout':
            path = zs.from_strokes(r['strokes'])[0]
            bo = [i for i, p in enumerate(path) if any(c['x'] == p[0] and c['y'] == p[1] and c['type'] == 'Blackout' for c in s['cells'])]
            sc += 0 if bo and bo[0] <= 0.4 * (len(path) - 1) else 1
        scored.append((sc, rng.random(), cid, r))
    scored.sort(key=lambda x: x[:2])
    print(mech, 'ok', len(scored))
    for sc, _, cid, r in scored[:3]:
        print('  ', round(sc, 2), r['metrics']['w'], r['metrics']['h'], 'sum', r['metrics']['sum'], 'p', round(r['metrics']['p_random'], 3), 'sol', r['metrics']['solutions'], 'crit', round(r['metrics']['crit_steps'], 2))
    if scored:
        r = scored[0][3]
        best[mech] = {'cid': scored[0][2], 'prov': 'new', 'stage': r['stage'], 'strokes': r['strokes'], 'm': r['metrics'], 'notes': [f'{mech} intro']}
json.dump(best, open('intros.json', 'w'))
