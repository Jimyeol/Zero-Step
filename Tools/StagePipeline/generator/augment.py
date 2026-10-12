"""Greedy hill-climbing: add restriction instances consistent with a known solution walk."""
import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import copy, random, math
import zs, zs_c, gen

WD = {'Up': (0, 1), 'Down': (0, -1), 'Left': (-1, 0), 'Right': (1, 0)}


def cellmap(s):
    return {(c['x'], c['y']): c for c in s['cells'] if c.get('count', 0) > 0}


def adj_charge(cm, p):
    return any(cm.get((p[0] + dx, p[1] + dy), {}).get('type') == 'Charge' for dx, dy in gen.DIRS)


def proposals(rng, s, walk, kinds, k=24):
    cm = cellmap(s)
    start = (s['startPoint']['x'], s['startPoint']['y'])
    fe = gen.first_entry(walk)
    M = len(walk) - 1
    out = []
    normal = [p for p, c in cm.items() if (c.get('type') or 'Normal') == 'Normal']
    if 'SC' in kinds:
        scc = gen.sc_candidates(set(cm), walk)
        for p in normal:
            if p in scc:
                s2 = copy.deepcopy(s)
                for c in s2['cells']:
                    if (c['x'], c['y']) == p:
                        c['type'] = 'ShortCircuit'; c['direction'] = gen.DNAME[scc[p]]
                out.append(('SC', s2))
    if 'FK' in kinds:
        for p in normal:
            if p != start and p in fe and 2 <= fe[p] < M and not adj_charge(cm, p):
                s2 = copy.deepcopy(s)
                for c in s2['cells']:
                    if (c['x'], c['y']) == p:
                        c['type'] = 'FixedKnot'; c['targetOrder'] = fe[p]; c['properties'] = {'isAbsolute': True}
                out.append(('FK', s2))
    if 'IGN' in kinds and not any(c.get('type') == 'Igniter' for c in cm.values()):
        for _ in range(12):
            ig_c = [p for p in normal if not adj_charge(cm, p) and (p == start or fe.get(p, 999) <= 0.7 * M)]
            if not ig_c: break
            ig = rng.choice(sorted(ig_c))
            t0 = 0 if ig == start else fe[ig]
            hs = [p for p in normal if p != ig and p != start and fe.get(p, -1) > t0 and not adj_charge(cm, p)]
            if not hs: continue
            grp = rng.sample(sorted(hs), min(len(hs), rng.randint(1, 3)))
            s2 = copy.deepcopy(s)
            gname = f'G{rng.randint(10, 99)}'
            for c in s2['cells']:
                q = (c['x'], c['y'])
                if q == ig: c['type'] = 'Igniter'; c['targetID'] = gname
                elif q in grp: c['type'] = 'Hidden'; c['groupID'] = gname
            out.append(('IGN', s2))
    if 'WALL' in kinds:
        unc = gen.all_edges(set(cm)) - gen.crossed_edges(walk) - zs.wall_edges(s)
        for p, c in cm.items():
            if c.get('type') == 'ShortCircuit':
                d = WD[(c.get('direction') or 'Down').capitalize()]
                unc.discard(frozenset((p, (p[0] + d[0], p[1] + d[1]))))
        for e in sorted(unc, key=lambda e: sorted(e)):
            a, b = sorted(e)
            s2 = copy.deepcopy(s)
            s2.setdefault('walls', []).append({'x': a[0], 'y': a[1], 'dir': gen.DNAME[(b[0] - a[0], b[1] - a[1])]})
            out.append(('WALL', s2))
    rng.shuffle(out)
    return out[:k]


def climb(rng, s, walk, kinds, rounds=8, max_states=1_500_000, base=None, want=None, k=24):
    """returns (stage, result) after greedy additions maximising D while keeping states < max_states."""
    base = base or zs_c.solve1(s, limit=max_states)
    cur, cur_r = s, base
    added = []
    for rnd in range(rounds):
        props = proposals(rng, cur, walk, kinds, k=k)
        if want:
            missing = [x for x in want if x not in added]
            if missing:
                pm = [p for p in props if p[0] in missing]
                props = pm or props
        if not props:
            break
        res = zs_c.run([(str(i), p[1], {'limit': max_states}) for i, p in enumerate(props)])
        best = None
        phase1 = bool(want and [x for x in want if x not in added])
        for i, (kind, s2) in enumerate(props):
            r = res[str(i)]
            if not r['complete'] or not r['solvable']:
                continue
            if r['solutions'] >= cur_r['solutions']:
                continue  # not effective
            gain = -math.log10(r['p_random']) - (-math.log10(cur_r['p_random']))
            if phase1:
                if kind not in want or kind in added:
                    continue
                # keep room for further mechanics: smallest effective reduction, earliest wanted kind first
                sc = -10 * want.index(kind) + math.log10(r['solutions'])
            else:
                sc = gain
            if best is None or sc > best[0]:
                best = (sc, kind, s2, r)
        if best is None:
            break
        _, kind, cur, cur_r = best
        added.append(kind)
    return cur, cur_r, added


def all_effective(stages, results):
    """stages: {id: stage}; results: {id: solver result}. returns {id: bool} (every instance matters)"""
    import abl
    ab = abl.run_ablation(stages, results, limit=3_000_000)
    return {sid: all(v is True for l, k, v, st in lst) for sid, lst in ab.items()}


def climb2(rng, s, walk, kinds, want, rounds=12, max_states=1_500_000, k=48, stop_D=None, phase2_rounds=4):
    cur = s
    cur_r = zs_c.solve1(s, limit=max_states)
    added = []
    p2 = 0
    for rnd in range(rounds):
        missing = [x for x in want if x not in added]
        props = proposals(rng, cur, walk, kinds, k=k)
        if missing:
            props = [p for p in props if p[0] in missing] or props
        elif stop_D is not None and -math.log10(cur_r['p_random']) >= stop_D:
            break
        if not props:
            break
        res = zs_c.run([(str(i), p[1], {'limit': max_states}) for i, p in enumerate(props)])
        good = []
        for i, (kind, s2) in enumerate(props):
            r = res[str(i)]
            if not r['complete'] or not r['solvable'] or r['solutions'] >= cur_r['solutions']:
                continue
            if missing:
                if kind not in missing:
                    continue
                key = (want.index(kind), -r['solutions'])
            else:
                key = (0, r['p_random'])
            good.append((key, i, kind, s2, r))
        good.sort(key=lambda g: g[0])
        good = good[:8]
        if not good:
            if missing:
                want = [x for x in want if x != missing[0]]
                continue
            break
        eff = all_effective({str(g[1]): g[3] for g in good}, {str(g[1]): g[4] for g in good})
        pick = next((g for g in good if eff[str(g[1])]), None)
        if pick is None:
            if missing:
                want = [x for x in want if x != missing[0]]
                continue
            break
        _, _, kind, cur, cur_r = pick
        added.append(kind)
        if not missing:
            p2 += 1
            if p2 >= phase2_rounds:
                break
    return cur, cur_r, added
