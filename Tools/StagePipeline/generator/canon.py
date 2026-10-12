"""Canonical forms for duplicate detection (D4 symmetry)."""
import zs
DIRV = {'UP': (0, 1), 'DOWN': (0, -1), 'RIGHT': (1, 0), 'LEFT': (-1, 0)}
T8 = [(sw, sx, sy) for sw in (False, True) for sx in (1, -1) for sy in (1, -1)]
def tf(p, t):
    sw, sx, sy = t; x, y = p
    if sw: x, y = y, x
    return (x * sx, y * sy)
def canon(s, use_start=True, use_counts=True, use_types=True, use_walls=True):
    cells = [c for c in s['cells'] if c.get('count', 0) > 0]
    sp = (s['startPoint']['x'], s['startPoint']['y'])
    walls = zs.wall_edges(s) if use_walls else set()
    best = None
    for t in T8:
        pts = [tf((c['x'], c['y']), t) for c in cells]
        mx = min(p[0] for p in pts); my = min(p[1] for p in pts)
        items = []
        for c, p in zip(cells, pts):
            q = (p[0] - mx, p[1] - my)
            ty = (c.get('type') or 'Normal') if use_types else ''
            extra = ''
            if use_types and ty == 'ShortCircuit':
                extra = tf(DIRV.get((c.get('direction') or 'Down').upper(), (0, -1)), t)
            if use_types and ty == 'FixedKnot':
                extra = c.get('targetOrder')
            items.append([q, c['count'] if use_counts else 0, ty, extra, c])
        items.sort(key=lambda z: z[0])
        gm = {}; out = []
        for q, cnt, ty, extra, c in items:
            if use_types and ty == 'TwinLink': extra = ('L', gm.setdefault(('L', c.get('linkID')), len(gm)))
            if use_types and ty == 'Hidden': extra = ('G', gm.setdefault(('G', c.get('groupID')), len(gm)))
            if use_types and ty == 'Igniter': extra = ('G', gm.setdefault(('G', c.get('targetID')), len(gm)))
            out.append((q, cnt, ty, str(extra)))
        wl = []
        for e in walls:
            a, b = [tf(p, t) for p in e]
            a = (a[0] - mx, a[1] - my); b = (b[0] - mx, b[1] - my)
            wl.append(tuple(sorted((a, b))))
        stp = tf(sp, t); stp = (stp[0] - mx, stp[1] - my) if use_start else None
        key = (tuple(out), tuple(sorted(wl)), stp)
        if best is None or key < best: best = key
    return best
def outline(s):
    cells = [(c['x'], c['y']) for c in s['cells'] if c.get('count', 0) > 0]
    best = None
    for t in T8:
        pts = [tf(p, t) for p in cells]
        mx = min(p[0] for p in pts); my = min(p[1] for p in pts)
        k = tuple(sorted((p[0] - mx, p[1] - my) for p in pts))
        if best is None or k < best: best = k
    return best
