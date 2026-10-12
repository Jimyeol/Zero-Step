import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import json, random, time, math, collections, statistics as st
import recipes, pipeline, canon

ALL = ['rect', 'holed', 'art', 'sym', 'ring', 'poly']
NONRECT = ['holed', 'art', 'sym', 'ring', 'poly', 'art']
def tiers(base, mechs, **kw):
    out = {}
    for name, n, mult in [('s', (5, 12), (1.0, 1.7)), ('m', (9, 20), (1.2, 2.0)), ('l', (14, 30), (1.3, 2.1)), ('x', (20, 36), (1.25, 1.6))]:
        r = {'shapes': kw.get('shapes', ALL), 'n': n, 'mult': mult, 'cap': [2, 3, 3, 4], 'mechs': mechs, 'straight': [0, 1, 3]}
        r.update({k: v for k, v in kw.items() if k != 'shapes'})
        out[f'{base}_{name}'] = r
    return out
RECIPES = {}
RECIPES.update(tiers('normal', {}))
RECIPES.update(tiers('sc', {'SC': (1, 3)}))
RECIPES.update(tiers('cb', {'CB': (1, 3)}))
RECIPES.update(tiers('fk', {'FK': (1, 2)}))
RECIPES.update(tiers('tl', {'TL': (1, 2)}))
RECIPES.update(tiers('ign', {'IGN': (1, 2)}))
RECIPES.update(tiers('wall', {'WALL': (2, 8)}, shapes=NONRECT + ['rect', 'rect']))
RECIPES.update(tiers('wallsc', {'WALL': (3, 10), 'SC': (1, 3)}, shapes=NONRECT + ['rect', 'rect']))
RECIPES.update(tiers('wallfkign', {'WALL': (2, 6), 'FK': (0, 2), 'IGN': (1, 2)}))
RECIPES.update(tiers('charge', {'CHG': (1, 3)}))
RECIPES.update(tiers('chargewall', {'CHG': (1, 2), 'WALL': (2, 6)}))
RECIPES.update(tiers('chargecb', {'CHG': (1, 2), 'CB': (1, 2)}))
RECIPES.update(tiers('chargetl', {'CHG': (1, 2), 'TL': (1, 2)}))
RECIPES.update(tiers('tlignwall', {'TL': (1, 2), 'IGN': (1, 1), 'WALL': (1, 5)}))
RECIPES.update(tiers('fkchgsc', {'FK': (1, 2), 'CHG': (1, 2), 'SC': (1, 2)}))
RECIPES.update(tiers('memory', {'BC': (2, 4), 'BO': (0, 1), 'CHG': (0, 2), 'WALL': (0, 4), 'SC': (0, 2)}))
RECIPES.update(tiers('cbtlwall', {'CB': (1, 2), 'TL': (1, 2), 'WALL': (1, 5)}))
RECIPES.update(tiers('classic2', {'SC': (0, 2), 'CB': (0, 1), 'FK': (0, 1), 'TL': (0, 1), 'IGN': (0, 1)}))
RECIPES.update(tiers('classicbc', {'SC': (0, 2), 'CB': (0, 1), 'FK': (0, 1), 'TL': (0, 1), 'IGN': (0, 1), 'BC': (1, 3), 'BO': (0, 1)}))
RECIPES.update(tiers('mix3', {'SC': (0, 2), 'CB': (0, 1), 'FK': (0, 1), 'TL': (0, 1), 'IGN': (0, 1), 'CHG': (0, 2), 'WALL': (0, 6)}))

def run(name, N, seed, out_path):
    R = RECIPES[name]
    rng = random.Random(seed)
    cands = {}
    tries = 0
    t0 = time.time()
    while len(cands) < N and tries < N * 40:
        tries += 1
        s = recipes.build(rng, R)
        if s is None: continue
        cands[f'{name}:{seed}:{tries}'] = s
    t1 = time.time()
    recs = pipeline.finalize(cands)
    t2 = time.time()
    ok = 0
    whys = collections.Counter()
    with open(out_path, 'a') as f:
        for cid, rec in recs.items():
            if not rec['ok']:
                whys[rec['why'].split(':')[0]] += 1
                continue
            ok += 1
            f.write(json.dumps({'cid': cid, 'recipe': name, 'stage': rec['stage'], 'strokes': rec['strokes'], 'm': rec['metrics'], 'stripped': rec['stripped']}) + '\n')
    return ok, len(cands), tries, round(t1 - t0, 1), round(t2 - t1, 1), whys

if __name__ == '__main__':
    out = sys.argv[1]
    N = int(sys.argv[2]); seed = int(sys.argv[3])
    for name in sys.argv[4:]:
        r = run(name, N, seed, out)
        print(name, r, flush=True)
