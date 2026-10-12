import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import json, random, collections, time
import recipes, pipeline, gen, zs, show
FIN = {
 'zero': [".#####.", "##...##", "#...###", "#..##.#", "#.##..#", "###...#", "##...##", ".#####."],
 'rocket': gen.ART['rocket'],
 'star8': ["...#...", "..###..", "#######", ".#####.", "..###..", ".##.##.", "##...##", "#.....#"],
 'crown8': ["#..#..#", "##.#.##", "#######", "#######", ".#####.", ".#...#.", ".#####.", "..###.."],
}
for k, v in FIN.items():
    c = gen.from_art(v); assert gen.connected(c) and gen.fits(c), k
    recipes.SHAPES['FIN_' + k] = c
names = ['FIN_' + k for k in FIN]
seed = int(sys.argv[1]); N = int(sys.argv[2])
rng = random.Random(seed)
NEED = {'ShortCircuit', 'CrossBlast', 'FixedKnot', 'TwinLink', 'Igniter', 'Charge', 'Wall'}
found = []
t0 = time.time()
for batch in range(int(sys.argv[3])):
    cands = {}
    tries = 0
    while len(cands) < N and tries < N * 60:
        tries += 1
        name = rng.choice(names)
        cells = set(gen.transform(recipes.SHAPES[name], rng.choice([(False, 1, 1), (False, -1, 1)])))
        recipes.SHAPE_NAMES = [name]
        R = {'shapes': ['art'], 'n': (20, 45), 'mult': (1.0, 1.25), 'cap': [2, 3], 'straight': [0, 1, 3],
             'mechs': {'SC': (2, 3), 'CB': (1, 2), 'FK': (1, 2), 'TL': (1, 1), 'IGN': (1, 1), 'CHG': (1, 2), 'WALL': (3, 8), 'BC': (2, 3), 'BO': (1, 1)},
             'tl_size': [2], 'hidden_max': 3, 'maxlen': 47, 'prefer_hits': 0.6}
        s = recipes.build(rng, R)
        if s: cands[f'fin:{seed}:{batch}:{tries}'] = s
    recs = pipeline.finalize(cands)
    for cid, r in recs.items():
        if not r['ok']: continue
        m = r['metrics']
        if not NEED <= set(m['rules']): continue
        if not {'BlindCurtain', 'Blackout'} <= set(m['types']): continue
        if m['p_random'] >= 1e-4: continue
        found.append({'cid': cid, 'prov': 'new', 'stage': r['stage'], 'strokes': r['strokes'], 'm': m, 'notes': ['finale']})
    print('batch', batch, 'cands', len(cands), 'found', len(found), round(time.time() - t0), flush=True)
json.dump(found, open(f'finale_{seed}.json', 'w'))
for f in sorted(found, key=lambda f: -f['m']['D'])[:3]:
    print(f['m']); print(show.render(f['stage']))
