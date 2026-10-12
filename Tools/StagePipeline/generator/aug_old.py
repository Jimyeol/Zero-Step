import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
os.environ['ZS_NPROC'] = '1'
import json, random, math, multiprocessing as mp
import zs, zs_c, augment, pipeline

def climb_hist(rng, s, walk, kinds, want, rounds=8, k=24, stop_D=3.4):
    cur = s
    cur_r = zs_c.solve1(s, limit=1_500_000)
    added = []; hist = []
    for rnd in range(rounds):
        missing = [x for x in want if x not in added]
        if not missing and -math.log10(cur_r['p_random']) >= stop_D:
            break
        props = augment.proposals(rng, cur, walk, kinds, k=k)
        if missing:
            props = [p for p in props if p[0] in missing] or props
        if not props: break
        res = zs_c.run([(str(i), p[1], {'limit': 1_500_000}) for i, p in enumerate(props)])
        good = []
        for i, (kind, s2) in enumerate(props):
            r = res[str(i)]
            if not r['complete'] or not r['solvable'] or r['solutions'] >= cur_r['solutions']: continue
            if missing:
                if kind not in missing: continue
                key = (want.index(kind), -r['solutions'])
            else:
                key = (0, r['p_random'])
            good.append((key, i, kind, s2, r))
        good.sort(key=lambda g: g[0]); good = good[:6]
        if not good:
            if missing:
                want = [x for x in want if x != missing[0]]; continue
            break
        eff = augment.all_effective({str(g[1]): g[3] for g in good}, {str(g[1]): g[4] for g in good})
        pick = next((g for g in good if eff[str(g[1])]), None)
        if pick is None:
            if missing:
                want = [x for x in want if x != missing[0]]; continue
            break
        _, _, kind, cur, cur_r = pick
        added.append(kind)
        hist.append((cur, list(added)))
    return hist

def work(args):
    cid, old, s, strokes, wants, seed = args
    rng = random.Random(seed)
    if len(strokes) != 1: return []
    walk = [tuple(p) for p in strokes[0]]
    out = []
    for want in wants:
        try:
            h = climb_hist(rng, s, walk, {'SC', 'FK', 'IGN', 'WALL'}, list(want))
        except Exception as e:
            continue
        # keep snapshots after all wanted kinds were tried, plus the last two
        for j, (st, added) in enumerate(h):
            if j >= len(h) - 3:
                out.append((f'{cid}+aug{"".join(w[0] for w in want)}{j}', old, st, added))
    return out

if __name__ == '__main__':
    O = json.load(open('old_records.json'))['records']
    V = json.load(open('old_variants.json'))
    jobs = []
    for k, r in enumerate(O):
        rules = set(r['m']['rules'])
        wants = [('WALL', 'SC') if 'ShortCircuit' not in rules else ('WALL', 'FK'), ('IGN', 'FK', 'WALL')]
        jobs.append((r['cid'], r['old'], r['stage'], r['strokes'], wants, k))
    for k, r in enumerate(V):
        if r['notes'][0] == 'charge':
            jobs.append((r['cid'], r['old'], r['stage'], r['strokes'], [('FK', 'SC')], 10000 + k))
    print('jobs', len(jobs), flush=True)
    cands = {}; meta = {}
    with mp.Pool(14) as pool:
        for n, res in enumerate(pool.imap_unordered(work, jobs, chunksize=2)):
            for cid, old, st, added in res:
                cands[cid] = st; meta[cid] = (old, added)
            if n % 200 == 0: print(n, len(cands), flush=True)
    print('candidates', len(cands), flush=True)
    os.environ['ZS_NPROC'] = '14'
    zs_c.os.environ['ZS_NPROC'] = '14'
    recs = pipeline.finalize(cands, strip=True, nproc=14)
    out = []
    for cid, rec in recs.items():
        if not rec['ok']: continue
        old, added = meta[cid]
        out.append({'cid': cid, 'prov': f'old:{old}+mutated', 'old': old, 'notes': ['augmented: +' + '+'.join(added)] + rec['stripped'], 'stage': rec['stage'], 'strokes': rec['strokes'], 'm': rec['metrics']})
    json.dump(out, open('old_aug.json', 'w'))
    print('kept', len(out))
