"""Assemble the 1000-stage sequence from candidate pools (old, old variants, new, intros, finale)."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import json, glob, math, random, collections, statistics as st
import canon

RULE_FROM = {'ShortCircuit': 21, 'CrossBlast': 51, 'FixedKnot': 101, 'TwinLink': 151, 'Igniter': 201,
             'Wall': 401, 'Charge': 551}
INFO_FROM = {'BlindCurtain': 251, 'Blackout': 301}
INTRO = {'ShortCircuit': 21, 'CrossBlast': 51, 'FixedKnot': 101, 'TwinLink': 151, 'Igniter': 201,
         'BlindCurtain': 251, 'Blackout': 301, 'Wall': 401, 'Charge': 551}
INTRO_AT = {v: k for k, v in INTRO.items()}
ALLR = set(RULE_FROM)

CH = {
    1: dict(name='First Steps + ShortCircuit', core={'ShortCircuit'}, kind='single'),
    2: dict(name='CrossBlast', core={'CrossBlast'}, kind='single'),
    3: dict(name='FixedKnot', core={'FixedKnot'}, kind='single'),
    4: dict(name='TwinLink', core={'TwinLink'}, kind='single'),
    5: dict(name='Igniter & Hidden', core={'Igniter'}, kind='single'),
    6: dict(name='BlindCurtain', core=set(), info={'BlindCurtain'}, kind='info'),
    7: dict(name='Blackout', core=set(), info={'Blackout'}, kind='info'),
    8: dict(name='Remix: classic mechanics combined', core={'ShortCircuit', 'CrossBlast', 'FixedKnot', 'TwinLink', 'Igniter'}, kind='remix'),
    9: dict(name='Wall', core={'Wall'}, kind='single'),
    10: dict(name='Wall mazes + ShortCircuit', core={'Wall', 'ShortCircuit'}, kind='combo'),
    11: dict(name='Locked doors: Wall + FixedKnot + Igniter', core={'Wall', 'FixedKnot', 'Igniter'}, kind='combo'),
    12: dict(name='Charge', core={'Charge'}, kind='single', multi=True),
    13: dict(name='Power grid: Charge + CrossBlast', core={'Charge', 'CrossBlast'}, kind='combo'),
    14: dict(name='Charge + TwinLink', core={'Charge', 'TwinLink'}, kind='combo'),
    15: dict(name='Circuits: TwinLink + Igniter + Wall', core={'TwinLink', 'Igniter', 'Wall'}, kind='combo'),
    16: dict(name='Timing: FixedKnot + Charge + ShortCircuit', core={'FixedKnot', 'Charge', 'ShortCircuit'}, kind='combo'),
    17: dict(name='Memory: BlindCurtain/Blackout + Charge/Wall', core={'Charge', 'Wall', 'ShortCircuit'}, info={'BlindCurtain', 'Blackout'}, kind='memory'),
    18: dict(name='CrossBlast + TwinLink + Wall', core={'CrossBlast', 'TwinLink', 'Wall'}, kind='combo'),
    19: dict(name='Everything', core=ALLR, kind='mix'),
    20: dict(name='Grand finale', core=ALLR, kind='mix'),
}
M0, MSTEP = 0.45, 0.14
SEGS = [6, 7, 5, 8, 6, 7, 7]  # positions 4..49


def chapter_median(c):
    return M0 + MSTEP * (c - 1)


def offsets():
    off = {1: -0.5, 2: -0.42, 3: -0.34}
    p = 4
    breath = set()
    for k, L in enumerate(SEGS):
        drift = -0.2 + 0.4 * k / (len(SEGS) - 1)
        for j in range(L):
            off[p] = drift - 0.35 + 0.75 * j / (L - 1)
            if j == 0 and k > 0:
                breath.add(p)
            p += 1
    off[50] = 1.25
    med = st.median(off.values())
    return {p: v - med for p, v in off.items()}, breath


OFF, BREATH = offsets()


def target(idx):
    c = (idx - 1) // 50 + 1
    p = (idx - 1) % 50 + 1
    if c == 1:
        if p <= 5:
            return 0.0
        if p <= 20:
            return 0.12 + 0.03 * (p - 6) + (0.12 if p % 4 == 0 else 0)
        if p <= 31:
            return 0.35 + 0.04 * (p - 21)
        return max(0.35, chapter_median(1) + OFF[p] * 0.8)
    scale = min(1.0, 0.6 + 0.08 * c)
    return max(0.35, chapter_median(c) + OFF[p] * scale)


def load_all():
    recs = []
    O = json.load(open('old_records.json'))
    for r in O['records']:
        r['src'] = 'old'
        recs.append(r)
    for fn, src in (('old_info_variants.json', 'oldvar'), ('old_variants.json', 'oldvar'), ('old_aug.json', 'oldvar')):
        if not os.path.exists(fn):
            continue
        for r in json.load(open(fn)):
            r['src'] = src
            recs.append(r)
    for fn in sorted(glob.glob('pools/*.jsonl')):
        for l in open(fn):
            try:
                r = json.loads(l)
            except Exception:
                continue
            r['src'] = 'new'
            r['prov'] = 'new'
            r['old'] = None
            r.setdefault('notes', [])
            recs.append(r)
    intros = json.load(open('intros.json'))
    fin = json.load(open('finale.json'))
    for r in recs:
        prep(r)
    for k, r in intros.items():
        r['src'] = 'intro'; r['old'] = None
        prep(r)
    prep(fin)
    fin['src'] = 'finale'; fin['old'] = None
    return recs, intros, fin, O['excluded']


def prep(r):
    m = r['m']
    r['rules'] = set(m['rules'])
    r['info'] = set(t for t in m['types'] if t in INFO_FROM)
    r['key'] = repr(canon.canon(r['stage'], use_start=False))
    r['outline'] = repr(canon.outline(r['stage']))
    r['first'] = max([RULE_FROM[t] for t in r['rules']] + [INFO_FROM[t] for t in r['info']] + [1])


def cellmaps(r):
    if '_maps' not in r:
        cells = [c for c in r['stage']['cells'] if c.get('count', 0) > 0]
        maps = []
        for t in canon.T8:
            pts = [canon.tf((c['x'], c['y']), t) for c in cells]
            mx = min(p[0] for p in pts); my = min(p[1] for p in pts)
            maps.append({(p[0] - mx, p[1] - my): (c['count'], c.get('type') or 'Normal') for c, p in zip(cells, pts)})
        r['_maps'] = maps
    return r['_maps']


def near_dup(a, b, k=2):
    """same outline and at most k cells differ in count/type (any rotation/mirror)"""
    if a['outline'] != b['outline']:
        return False
    da = cellmaps(a)[0]
    for db in cellmaps(b):
        if set(db) == set(da) and sum(1 for p in da if da[p] != db[p]) <= k:
            return True
    return False


def eligible(r, idx):
    if r['first'] > idx:
        return False
    m = r['m']
    if m['states'] >= 2_000_000 or m['w'] > 7 or m['h'] > 8 or m['maxc'] > 6 or m['sum'] > 48:
        return False
    if idx > 20 and m['p_random'] >= 0.999999:
        return False
    if idx > 30 and m['p_random'] >= 0.5:
        return False
    return True


def theme_cost(r, c, focus_mech, slot_easy):
    spec = CH[c]
    rules, info = r['rules'], r['info']
    core = spec['core']
    k = len(rules & core)
    cost = 0.0
    if focus_mech:
        need = focus_mech
        ok = (need in rules) if need in RULE_FROM else (need in info)
        if not ok:
            return None
    kind = spec['kind']
    if kind == 'single':
        if k == 0:
            cost += 0.45
        if spec.get('multi') and len(rules) < 2 and not slot_easy and not focus_mech:
            cost += 0.6
    elif kind == 'info':
        if not (info & spec['info']):
            cost += 0.6
    elif kind == 'remix':
        cost += {0: 1.0, 1: 0.5}.get(len(rules), 0.0)
    elif kind == 'combo':
        if k == 0:
            return None
        if len(rules) < 2 and not slot_easy:
            return None
        need = min(2, len(core))
        cost += 0.8 * (need - min(k, need))
        if len(rules) < 2:
            cost += 0.4
    elif kind == 'memory':
        if not info:
            cost += 0.9
        if 'Charge' in rules or 'Wall' in rules:
            cost -= 0.15
        if len(rules) < 2 and not slot_easy:
            return None
        if not (rules & core):
            cost += 0.4
    elif kind == 'mix':
        if len(rules) < 2 and not slot_easy:
            return None
        cost += {2: 0.35}.get(len(rules), 0.0) if len(rules) >= 2 else 0.8
        if info:
            cost -= 0.1
        if 'Charge' in rules:
            cost -= 0.2
    return cost


def run(seed=1, out='assembly.json'):
    rng = random.Random(seed)
    recs, intros, fin, excluded = load_all()
    # dedupe by key (prefer old > oldvar > new)
    pri = {'old': 0, 'oldvar': 1, 'new': 2}
    best = {}
    for r in recs:
        k = r['key']
        if k not in best or (pri[r['src']], r['cid']) < (pri[best[k]['src']], best[k]['cid']):
            best[k] = r
    recs = list(best.values())
    if os.path.exists('plain_bad.json'):
        bad = set(json.load(open('plain_bad.json')))
        recs = [r for r in recs if r['cid'] not in bad]
    print('unique candidates', len(recs), collections.Counter(r['src'] for r in recs))
    used_keys = set()
    used_old = set()
    outline_use = collections.Counter()
    seq = {}
    reserved = {INTRO[k]: intros[k] for k in INTRO}
    reserved[1000] = fin
    by_outline = collections.defaultdict(list)
    for idx, r in reserved.items():
        used_keys.add(r['key'])
        outline_use[r['outline']] += 1
        by_outline[r['outline']].append(r)
    for c in range(1, 21):
        lo, hi = (c - 1) * 50 + 1, c * 50
        chosen = {}
        combo_use = collections.Counter()
        for idx in range(lo, hi + 1):
            if idx in reserved:
                chosen[idx] = reserved[idx]
        intro_here = [i for i in reserved if lo <= i <= hi and i in INTRO_AT]
        focus = {}
        for i in intro_here:
            for j in range(i + 1, i + 11):
                focus[j] = INTRO_AT[i]
        # slot order: boss, focus, then by target desc
        slots = [i for i in range(lo, hi + 1) if i not in chosen]
        slots.sort(key=lambda i: (i != hi, i not in focus, -target(i)))
        pool = [r for r in recs if r['key'] not in used_keys and r['first'] <= hi]
        for idx in slots:
            t = target(idx)
            p = (idx - 1) % 50 + 1
            easy = p <= 3 or idx in [lo + q - 1 for q in BREATH]
            scored = []
            for r in pool:
                if r['key'] in used_keys or (r['old'] is not None and r['old'] in used_old):
                    continue
                if not eligible(r, idx):
                    continue
                tc = theme_cost(r, c, focus.get(idx), easy)
                if tc is None:
                    continue
                m = r['m']
                D = m['D']
                if idx == hi:
                    tb = chapter_median(c) + 1.2 + 0.03 * c
                    cost = 2.0 * abs(D - tb)   # boss: clearly the hardest of the chapter
                    if m['states'] > 1_000_000:
                        cost += 1.0
                else:
                    cost = 2.5 * abs(D - t) + (1.5 * (abs(D - t) - 0.5) if abs(D - t) > 0.5 else 0)
                cost += tc
                if c <= 8 and r['src'] == 'new':
                    cost += 0.5
                if c >= 9 and r['src'] == 'new':
                    cost += 0.25
                if r['src'] == 'oldvar':
                    cost += 0.1
                if idx > 100 and m['rect']:
                    cost += 0.35
                cost += 0.2 * min(outline_use[r['outline']], 5)
                cost += 0.12 * combo_use[tuple(sorted(r['rules'] | r['info']))]
                if m['states'] > 1_000_000:
                    cost += 0.6
                if c == 1 and p <= 20:
                    cost += 0.08 * max(0, m['tiles'] - (3 + p // 2))
                if c == 1 and p == 7 and m['crit_steps'] < 2:
                    cost += 3
                if c == 1 and p <= 5:
                    cost += 0.3 * m['tiles'] + (0 if r['cid'] != 'old:1' or p == 1 else 5) - (5 if (p == 1 and r['cid'] == 'old:1') else 0)
                cost += rng.random() * 0.05
                scored.append((cost, r))
            scored.sort(key=lambda x: x[0])
            best_r = None
            for cost, r in scored:
                if any(near_dup(r, q) for q in by_outline[r['outline']]):
                    continue
                best_r = r
                break
            if best_r is None:
                raise RuntimeError(f'no candidate for stage {idx}')
            by_outline[best_r['outline']].append(best_r)
            chosen[idx] = best_r
            used_keys.add(best_r['key'])
            if best_r['old'] is not None:
                used_old.add(best_r['old'])
            outline_use[best_r['outline']] += 1
            combo_use[tuple(sorted(best_r['rules'] | best_r['info']))] += 1
        # reorder: rank-match within groups (focus block, rest) except fixed (reserved, boss)
        fixed = set(i for i in chosen if i in reserved) | {hi}
        if c == 1:
            fixed |= set(range(1, 9))
        groups = [sorted(focus)] if focus else []
        rest = [i for i in range(lo, hi + 1) if i not in fixed and i not in focus]
        cuts = sorted(intro_here)
        for a, b in zip([lo - 1] + cuts, cuts + [hi + 1]):
            groups.append([i for i in rest if a < i < b])
        for g in groups:
            g = [i for i in g if i not in fixed]
            stages_g = sorted((chosen[i] for i in g), key=lambda r: r['m']['D'])
            pos_g = sorted(g, key=lambda i: target(i))
            for i, r in zip(pos_g, stages_g):
                chosen[i] = r
        # constraints fix-up: no more than 3 consecutive same outline
        for _ in range(3):
            for i in range(lo + 3, hi + 1):
                o = chosen[i]['outline']
                if all(chosen[i - k]['outline'] == o for k in (1, 2, 3)) and i not in fixed:
                    for j in range(lo, hi + 1):
                        if j in fixed or j in focus or chosen[j]['outline'] == o:
                            continue
                        if abs(chosen[j]['m']['D'] - chosen[i]['m']['D']) < 0.4:
                            chosen[i], chosen[j] = chosen[j], chosen[i]
                            break
        # boss must be the hardest stage of the chapter
        top = max(range(lo, hi), key=lambda i: chosen[i]['m']['D'])
        if chosen[top]['m']['D'] > chosen[hi]['m']['D'] and hi not in reserved and top not in focus and top not in reserved:
            chosen[top], chosen[hi] = chosen[hi], chosen[top]
        seq.update(chosen)
        Ds = [chosen[i]['m']['D'] for i in range(lo, hi + 1)]
        print(f"ch{c:2d} median D {st.median(Ds):.2f} (target {chapter_median(c):.2f}) range {min(Ds):.2f}-{max(Ds):.2f} boss {Ds[-1]:.2f} src {dict(collections.Counter(chosen[i]['src'] for i in range(lo, hi + 1)))}")
    for idx, r in seq.items():
        if idx in reserved:
            continue
        assert eligible(r, idx), (idx, r['cid'])
    return seq, excluded


if __name__ == '__main__':
    seq, excluded = run()
    json.dump({str(i): {k: v for k, v in r.items() if k not in ('rules', 'info', '_maps')} for i, r in seq.items()}, open('assembly.json', 'w'))
