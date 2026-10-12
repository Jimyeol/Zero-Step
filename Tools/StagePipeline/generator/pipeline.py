"""Candidate finalisation: solve, strip ineffective mechanics, Blackout timing, no-release path, metrics."""
import copy, math, json
import zs, zs_c, abl, canon

RULE_TYPES = ['ShortCircuit', 'CrossBlast', 'FixedKnot', 'TwinLink', 'Igniter', 'Charge']
LIMIT = 2_000_000
ABL_LIMIT = 4_000_000


def rule_set(s):
    ts = {c.get('type') or 'Normal' for c in s['cells'] if c.get('count', 0) > 0}
    out = {t for t in ts if t in RULE_TYPES}
    if 'Hidden' in ts:
        out.add('Igniter')
    if s.get('walls'):
        out.add('Wall')
    return out


def all_types(s):
    ts = {c.get('type') or 'Normal' for c in s['cells'] if c.get('count', 0) > 0} - {'Normal'}
    if s.get('walls'):
        ts.add('Wall')
    return ts


def is_rect(s):
    cells = [(c['x'], c['y']) for c in s['cells'] if c.get('count', 0) > 0]
    xs = [x for x, y in cells]; ys = [y for x, y in cells]
    return len(cells) == (max(xs) - min(xs) + 1) * (max(ys) - min(ys) + 1)


def strip_instance(s, label, kind):
    s = copy.deepcopy(s)
    if kind == 'Wall':
        spec = label.split('@')[1].split(',')
        x, y, d = int(spec[0]), int(spec[1]), spec[2]
        s['walls'] = [w for w in s['walls'] if not (w['x'] == x and w['y'] == y and w['dir'] == d)]
        if not s['walls']:
            del s['walls']
    elif kind == 'Igniter':
        g = label.split(':', 1)[1]
        for j, c in enumerate(s['cells']):
            if (c.get('type') == 'Igniter' and (c.get('targetID') or '') == g) or (c.get('type') == 'Hidden' and (c.get('groupID') or 'default') == g):
                s['cells'][j] = abl.to_normal(c)
    else:
        xy = label.split('@')[1].split(',')
        x, y = int(xy[0]), int(xy[1])
        for j, c in enumerate(s['cells']):
            if c['x'] == x and c['y'] == y:
                s['cells'][j] = abl.to_normal(c)
    # TwinLink groups of size 1 have no effect: they will be stripped next round by ablation anyway
    return s


def bo_tiles(s):
    S = zs.Stage(s)
    return [i for i in range(S.n) if S.type[i] == 'Blackout']


def finalize(cands, nproc=None, strip=True, max_rounds=4, limit=LIMIT, log=None, relocate_bo=True):
    """cands: {cid: stage}. returns {cid: rec} where rec has keys ok, why, stage, res, stripped"""
    out = {}
    cur = {cid: copy.deepcopy(s) for cid, s in cands.items()}
    stripped = {cid: [] for cid in cur}
    res = zs_c.run([(cid, s, {'limit': limit}) for cid, s in cur.items()], nproc=nproc)
    for cid in list(cur):
        r = res[cid]
        if not r['complete'] or not r['solvable'] or r.get('overflow'):
            out[cid] = {'ok': False, 'why': 'incomplete' if not r['complete'] else 'unsolvable', 'states': r['states']}
            del cur[cid]
    for rnd in range(max_rounds):
        if not cur:
            break
        ab = abl.run_ablation(cur, {cid: res[cid] for cid in cur}, limit=ABL_LIMIT, nproc=nproc)
        changed = {}
        for cid, lst in ab.items():
            bad = [(l, k) for l, k, v, st in lst if v is False]
            unk = [(l, k) for l, k, v, st in lst if v is None]
            if unk:
                out[cid] = {'ok': False, 'why': 'ablation-unknown'}
                del cur[cid]
                continue
            if bad:
                if not strip:
                    out[cid] = {'ok': False, 'why': 'ineffective:' + ','.join(l for l, k in bad)}
                    del cur[cid]
                    continue
                s = cur[cid]
                for l, k in bad:
                    s = strip_instance(s, l, k)
                    stripped[cid].append(l)
                cur[cid] = s
                changed[cid] = s
        if not changed:
            break
        r2 = zs_c.run([(cid, s, {'limit': limit}) for cid, s in changed.items()], nproc=nproc)
        for cid in changed:
            r = r2[cid]
            if not r['complete'] or not r['solvable']:
                out[cid] = {'ok': False, 'why': 'after-strip:' + ('incomplete' if not r['complete'] else 'unsolvable')}
                del cur[cid]
            else:
                res[cid] = r
    else:
        # still changing after max rounds -> reject the ones that changed in last round
        pass
    # final ablation confirmation is done by caller's validation; here check blackout + norelease path
    bo_items = []
    nr_items = []
    for cid, s in cur.items():
        S = zs.Stage(s)
        bts = [i for i in range(S.n) if S.type[i] == 'Blackout']
        if bts:
            K = int(math.floor(0.6 * res[cid]['min_len']))
            bo_items.append((cid, s, {'limit': limit * 2, 'bo_deadline': (bts, K)}))
        if any(t == 'TwinLink' for t in S.type) and any(res[cid].get('released') or []):
            nr_items.append((cid, s, {'limit': limit, 'norelease': True}))
    bo_res = zs_c.run(bo_items, nproc=nproc) if bo_items else {}
    if relocate_bo:
        # Blackout triggered too late in some solution: try moving it to other early tiles
        late = [cid for cid, _, _ in bo_items if not (bo_res[cid]['complete'] and bo_res[cid].get('solutions') == res[cid]['solutions'])]
        tries = []
        for cid in late:
            s = cur[cid]
            path, _ = zs.from_strokes(zs.to_strokes(res[cid]['path'], res[cid]['released']))
            fe = {}
            for k, p in enumerate(path):
                if k > 0 and tuple(p) not in fe:
                    fe[tuple(p)] = k
            sp = (s['startPoint']['x'], s['startPoint']['y'])
            K = int(math.floor(0.6 * res[cid]['min_len']))
            opts = sorted([j for j, c in enumerate(s['cells']) if (c.get('type') or 'Normal') in ('Normal', 'Blackout') and c['count'] > 0
                           and (c['x'], c['y']) != sp and fe.get((c['x'], c['y']), 999) <= K], key=lambda j: fe[(s['cells'][j]['x'], s['cells'][j]['y'])])
            for j in opts[:6]:
                s2 = copy.deepcopy(s)
                for c in s2['cells']:
                    if c.get('type') == 'Blackout':
                        c['type'] = 'Normal'
                s2['cells'][j]['type'] = 'Blackout'
                S2 = zs.Stage(s2)
                t = S2.idx[(s2['cells'][j]['x'], s2['cells'][j]['y'])]
                tries.append((f'{cid}@bo{j}', s2, {'limit': limit * 2, 'bo_deadline': ([t], K)}))
        tr = zs_c.run(tries, nproc=nproc) if tries else {}
        for key, s2, _ in tries:
            cid = key.split('@bo')[0]
            x = tr[key]
            if cid in late and x['complete'] and x.get('solutions') == res[cid]['solutions']:
                cur[cid] = s2
                bo_res[cid] = x
                late.remove(cid)
    nr_res = zs_c.run(nr_items, nproc=nproc) if nr_items else {}
    for cid, s in cur.items():
        r = res[cid]
        rec = {'ok': True, 'why': '', 'stage': s, 'res': r, 'stripped': stripped[cid]}
        if cid in bo_res:
            b = bo_res[cid]
            if not b['complete'] or b.get('solutions') != r['solutions']:
                rec['ok'] = False
                rec['why'] = 'blackout-late'
        strokes_path, strokes_rel = r['path'], r['released']
        if cid in nr_res and nr_res[cid].get('solvable'):
            strokes_path, strokes_rel = nr_res[cid]['path'], nr_res[cid]['released']
        rec['strokes'] = zs.to_strokes(strokes_path, strokes_rel)
        rp = zs.replay_strokes(s, rec['strokes'])
        if not rp['ok']:
            rec['ok'] = False
            rec['why'] = 'replay:' + rp['reason']
        rec['metrics'] = metrics(s, r)
        out[cid] = rec
    return out


def metrics(s, r):
    cells = [c for c in s['cells'] if c.get('count', 0) > 0]
    xs = [c['x'] for c in cells]; ys = [c['y'] for c in cells]
    p = r['p_random']
    return {'w': max(xs) - min(xs) + 1, 'h': max(ys) - min(ys) + 1, 'tiles': len(cells),
            'sum': sum(c['count'] for c in cells), 'maxc': max(c['count'] for c in cells),
            'p_random': p, 'D': -math.log10(p) if p > 0 else 99.0, 'crit_steps': r['crit_steps'],
            'forced_ratio': r['forced_ratio'], 'solutions': r['solutions'], 'states': r['states'],
            'min_len': r['min_len'], 'max_len': r['max_len'],
            'rules': sorted(rule_set(s)), 'types': sorted(all_types(s)), 'walls': len(s.get('walls') or []),
            'rect': is_rect(s)}
