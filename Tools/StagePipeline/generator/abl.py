"""Per-instance mechanic ablation (does every rule-changing mechanic instance matter?)."""
import copy, json
import zs, zs_c

RESTRICT = {'ShortCircuit', 'FixedKnot', 'Igniter', 'Wall'}
EFFECT = {'CrossBlast', 'TwinLink', 'Charge'}


def to_normal(c):
    d = {"x": c['x'], "y": c['y'], "count": c['count'], "type": "Normal"}
    return d


def instances(s):
    """yield (label, kind, ablated_stage)"""
    out = []
    cells = s['cells']
    groups = {}
    for i, c in enumerate(cells):
        if c.get('count', 0) <= 0:
            continue
        t = c.get('type') or 'Normal'
        if t in ('ShortCircuit', 'FixedKnot', 'CrossBlast', 'TwinLink', 'Charge'):
            a = copy.deepcopy(s)
            a['cells'][i] = to_normal(c)
            out.append((f"{t}@{c['x']},{c['y']}", t, a))
        elif t == 'Igniter':
            groups.setdefault(c.get('targetID') or '', []).append(i)
    for i, c in enumerate(cells):
        if (c.get('type') == 'Hidden') and c.get('count', 0) > 0:
            groups.setdefault(c.get('groupID') or 'default', [])
    for g, igs in groups.items():
        a = copy.deepcopy(s)
        idxs = list(igs) + [i for i, c in enumerate(cells) if c.get('type') == 'Hidden' and (c.get('groupID') or 'default') == g]
        for i in idxs:
            a['cells'][i] = to_normal(cells[i])
        out.append((f"Igniter/Hidden:{g}", 'Igniter', a))
    for k, w in enumerate(s.get('walls') or []):
        a = copy.deepcopy(s)
        a['walls'] = [x for j, x in enumerate(s['walls']) if j != k]
        if not a['walls']:
            del a['walls']
        out.append((f"Wall@{w['x']},{w['y']},{w['dir']}", 'Wall', a))
    return out


def verdict(kind, orig, ab, s, a):
    """orig/ab: solver results. returns True (matters), False (no effect) or None (unknown)."""
    if not ab.get('complete'):
        return None
    if not ab['solvable']:
        return True
    if not orig['solvable']:
        return True
    if ab['solutions'] != orig['solutions']:
        return True
    if kind in RESTRICT:
        return False
    # effect type: same count; compare canonical paths cross-validity
    if not zs.replay(a, orig['path'], orig.get('released'))['ok']:
        return True
    if not zs.replay(s, ab['path'], ab.get('released'))['ok']:
        return True
    return False


def run_ablation(stages, orig_results, limit=4_000_000, nproc=None):
    """stages: {sid: stage}, orig_results: {sid: C result}. returns {sid: [(label, kind, verdict, ab_states)]}"""
    items = []
    meta = {}
    for sid, s in stages.items():
        for k, (label, kind, a) in enumerate(instances(s)):
            key = f"{sid}#{k}"
            items.append((key, a, {'limit': limit}))
            meta[key] = (sid, label, kind, a)
    res = zs_c.run(items, nproc=nproc) if items else {}
    out = {sid: [] for sid in stages}
    for key, (sid, label, kind, a) in meta.items():
        r = res[key]
        v = verdict(kind, orig_results[sid], r, stages[sid], a)
        out[sid].append((label, kind, v, r['states']))
    return out
