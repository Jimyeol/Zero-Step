"""Walk-first stage generator for ZeroStep (shapes, walks, mechanic placement, count derivation)."""
import random, json, copy
import zs

DIRS = [(1, 0), (-1, 0), (0, 1), (0, -1)]
DNAME = {(0, 1): 'Up', (0, -1): 'Down', (-1, 0): 'Left', (1, 0): 'Right'}
CONFIG = {"mode": "Normal", "difficulty": "Normal", "spotlightRadius": 2.5, "showGridLines": False}
BEAMS = ["#00FBFF", "#38bdf8", "#22d3ee", "#34d399", "#00FFFF"]
TWIN_COLORS = ["#00FBFF", "#f59e0b", "#a78bfa", "#38bdf8", "#22d3ee", "#34d399"]
MAXW, MAXH = 7, 8


# ------------------------------------------------------------------------------- shapes
def connected(cells):
    cells = set(cells)
    if not cells:
        return False
    st = [next(iter(cells))]
    seen = {st[0]}
    while st:
        x, y = st.pop()
        for dx, dy in DIRS:
            q = (x + dx, y + dy)
            if q in cells and q not in seen:
                seen.add(q)
                st.append(q)
    return len(seen) == len(cells)


def normalize(cells):
    mx = min(x for x, y in cells)
    my = min(y for x, y in cells)
    return frozenset((x - mx, y - my) for x, y in cells)


def dims(cells):
    return max(x for x, y in cells) + 1, max(y for x, y in cells) + 1


def is_rect(cells):
    w, h = dims(cells)
    return len(cells) == w * h


def rect(w, h):
    return frozenset((x, y) for x in range(w) for y in range(h))


def from_art(rows):
    """rows top->bottom, '#' = cell. y=0 is the bottom row."""
    h = len(rows)
    return normalize({(x, h - 1 - r) for r, row in enumerate(rows) for x, ch in enumerate(row) if ch == '#'})


ART = {
    'plus5': ["..#..", "..#..", "#####", "..#..", "..#.."],
    'plus_fat': [".###.", "#####", "#####", "#####", ".###."],
    'ring44': ["####", "#..#", "#..#", "####"],
    'ring54': ["#####", "#...#", "#...#", "#####"],
    'ring55': ["#####", "#...#", "#.#.#", "#...#", "#####"],
    'ring64': ["######", "#....#", "#....#", "######"],
    'H': ["#..#", "#..#", "####", "#..#", "#..#"],
    'H5': ["#...#", "#...#", "#####", "#...#", "#...#"],
    'U': ["#..#", "#..#", "#..#", "####"],
    'U5': ["#...#", "#...#", "#...#", "#####", "#####"],
    'T': ["#####", "..#..", "..#..", "..#.."],
    'T2': ["#####", "#####", ".###.", "..#..", "..#.."],
    'L': ["#...", "#...", "#...", "####"],
    'Lfat': ["##...", "##...", "##...", "#####", "#####"],
    'S': [".###", ".#..", ".###", "...#", "###."],
    'Z': ["####", "..#.", ".#..", "####"],
    'stairs4': ["#...", "##..", "###.", "####"],
    'stairs5': ["#....", "##...", "###..", "####.", "#####"],
    'stairs_thin': ["##...", ".##..", "..##.", "...##"],
    'diamond': ["..#..", ".###.", "#####", ".###.", "..#.."],
    'diamond7': ["...#...", "..###..", ".#####.", "#######", ".#####.", "..###..", "...#..."],
    'heart': [".##.##.", "#######", "#######", ".#####.", "..###..", "...#..."],
    'arrow': ["..#..", ".###.", "#####", "..#..", "..#..", "..#.."],
    'X': ["#...#", ".#.#.", "..#..", ".#.#.", "#...#"],
    'X_fat': ["##.##", "#####", ".###.", "#####", "##.##"],
    'E': ["####", "#...", "###.", "#...", "####"],
    'F': ["####", "#...", "###.", "#...", "#..."],
    'C': ["####", "#...", "#...", "#...", "####"],
    'O': [".##.", "#..#", "#..#", "#..#", ".##."],
    'P': ["###.", "#..#", "###.", "#...", "#..."],
    'spiral': ["#####", "....#", "###.#", "#...#", "#####"],
    'comb': ["#.#.#", "#.#.#", "#####"],
    'comb2': ["#####", "#.#.#", "#.#.#", "#####"],
    'key': ["###....", "#.#####", "###..#."],
    'cup': ["#...#", "#...#", "##.##", ".###."],
    'bridge': ["#####", "#...#", "#...#"],
    'zig': ["##...", ".##..", "..##.", "...##", "..##.", ".##.."],
    'tetris_T': ["###", ".#."],
    'butterfly': ["#...#", "##.##", "#####", "##.##", "#...#"],
    'hourglass': ["#####", ".###.", "..#..", ".###.", "#####"],
    'star': ["..#..", ".###.", "#####", ".#.#.", "#...#"],
    'house': ["..#..", ".###.", "#####", "##.##", "##.##"],
    'tree': ["..#..", ".###.", "#####", "..#..", ".###."],
    'crown': ["#.#.#", "#####", "#####"],
    'anchor': ["..#..", ".###.", "..#..", "#.#.#", "#####"],
    'fish': [".##..#", "######", ".##..#"],
    'boat': ["...#...", "..##...", ".###...", "#######", ".#####."],
    'moon': [".###", "##..", "#...", "##..", ".###"],
    'gate': ["#######", "#.....#", "#.###.#", "#.#.#.#"],
    'temple': ["...#...", "..###..", ".#####.", "##.#.##", "#######"],
    'frame_cross': ["#######", "#..#..#", "#######", "#..#..#", "#######"],
    'big_heart': [".##.##.", "#######", "#######", "#######", ".#####.", "..###..", "...#..."],
    'castle': ["#.#.#.#", "#######", "#######", "##...##", "##...##"],
    'rocket': ["...#...", "..###..", "..###..", "..###..", ".#####.", "#######", "##.#.##", "#..#..#"],
}


def art_shapes():
    return {k: from_art(v) for k, v in ART.items()}


def random_poly(rng, n, w=MAXW, h=MAXH):
    cells = {(rng.randrange(w), rng.randrange(h))}
    while len(cells) < n:
        x, y = rng.choice(sorted(cells))
        dx, dy = rng.choice(DIRS)
        q = (x + dx, y + dy)
        if 0 <= q[0] < w and 0 <= q[1] < h:
            cells.add(q)
    return normalize(cells)


def holed_rect(rng, w, h, holes):
    cells = set(rect(w, h))
    tries = 0
    while holes > 0 and tries < 200:
        tries += 1
        c = (rng.randrange(w), rng.randrange(h))
        if c not in cells:
            continue
        cells.discard(c)
        if connected(cells) and dims(normalize(cells)) == (w, h):
            holes -= 1
        else:
            cells.add(c)
    return normalize(cells)


def sym_poly(rng, w, h, density=0.7):
    """mirror-symmetric random shape"""
    half = (w + 1) // 2
    for _ in range(200):
        cells = set()
        for x in range(half):
            for y in range(h):
                if rng.random() < density:
                    cells.add((x, y))
                    cells.add((w - 1 - x, y))
        if len(cells) >= 5 and connected(cells):
            c = normalize(cells)
            if dims(c)[0] <= MAXW and dims(c)[1] <= MAXH:
                return c
    return rect(w, h)


def transform(cells, t):
    sw, sx, sy = t
    out = set()
    for x, y in cells:
        if sw:
            x, y = y, x
        out.add((x * sx, y * sy))
    return normalize(out)


ALL_T = [(sw, sx, sy) for sw in (False, True) for sx in (1, -1) for sy in (1, -1)]


def fits(cells):
    w, h = dims(cells)
    return w <= MAXW and h <= MAXH


# ------------------------------------------------------------------------------- walks
def nbrs(cells, c, walls=frozenset()):
    x, y = c
    out = []
    for dx, dy in DIRS:
        q = (x + dx, y + dy)
        if q in cells and frozenset((c, q)) not in walls:
            out.append(q)
    return out


def random_walk(rng, cells, length, cap=4, start=None, straight=0.0, walls=frozenset(), avoid_back=0.6, unvisited_bias=3.0):
    cells = set(cells)
    for attempt in range(60):
        s = start if start is not None else rng.choice(sorted(cells))
        walk = [s]
        vis = {c: 0 for c in cells}
        vis[s] = 1
        unv = len(cells) - 1
        ok = True
        while unv > 0 or len(walk) - 1 < length:
            cur = walk[-1]
            opts = [q for q in nbrs(cells, cur, walls) if vis[q] < cap]
            if not opts:
                ok = False
                break
            ws = []
            for q in opts:
                wq = 1.0 / (1 + vis[q]) ** 1.5
                if vis[q] == 0:
                    wq *= unvisited_bias
                if len(walk) >= 2 and q == walk[-2]:
                    wq *= (1 - avoid_back)
                if straight and len(walk) >= 2:
                    d1 = (cur[0] - walk[-2][0], cur[1] - walk[-2][1])
                    d2 = (q[0] - cur[0], q[1] - cur[1])
                    if d1 == d2:
                        wq *= (1 + straight)
                ws.append(wq)
            q = rng.choices(opts, ws)[0]
            if vis[q] == 0:
                unv -= 1
            vis[q] += 1
            walk.append(q)
            if len(walk) > length * 3 + 60:
                ok = False
                break
        if ok:
            return walk
    return None


# ------------------------------------------------------------------------------- derivation
def derive_counts(cells, walk, types, extra, rng=None, prefer_hits=0.5, partners=None):
    """Given cell set, a walk (list of cells), types {cell: type}, extra {cell: dict}
    returns counts {cell: count} that make `walk` a winning continuous-drag line, or None.
    For tiles with optional terminal hit events a random choice is made (prefer_hits)."""
    rng = rng or random
    M = len(walk) - 1
    final = walk[-1]
    ev = {c: [] for c in cells}  # (time, kind)  kind: 'L','H','G'
    # TwinLink partners
    if partners is None:
        partners = {}
        grp = {}
        for c in cells:
            if types.get(c) == 'TwinLink':
                grp.setdefault(extra.get(c, {}).get('linkID', 101), []).append(c)
        for g in grp.values():
            for c in g:
                partners[c] = [d for d in g if d != c]
    for i in range(M):
        a, t = walk[i], walk[i + 1]
        prev = walk[i - 1] if i >= 1 else None
        ev[a].append((i, 'L'))
        ta = types.get(a, 'Normal')
        if ta == 'TwinLink':
            for q in partners.get(a, []):
                if q != t and q != prev:
                    ev[q].append((i, 'H'))
        elif ta == 'CrossBlast':
            for dx, dy in DIRS:
                q = (a[0] + dx, a[1] + dy)
                if q in cells and q != t:
                    ev[q].append((i, 'H'))
        elif ta == 'Charge':
            for dx, dy in DIRS:
                q = (a[0] + dx, a[1] + dy)
                if q in cells and q != t and types.get(q, 'Normal') in zs.CHARGEABLE:
                    ev[q].append((i, 'G'))
    appear = {c: [] for c in cells}
    for i, c in enumerate(walk):
        appear[c].append(i)
    counts = {}
    for c in cells:
        e = ev[c]
        decs = [k for k, (t, kind) in enumerate(e) if kind in ('L', 'H')]
        if c == final:
            # all decrements before final entry effective; must be exactly 1 at the end
            dcount = len(decs)
            gains = sum(1 for t, kind in e if kind == 'G')
            init = dcount + 1 - gains
            # running count check: r(t) > 0 throughout
            r = init
            for t, kind in e:
                r += 1 if kind == 'G' else -1
                if r <= 0:
                    return None
            if init < 1:
                return None
            counts[c] = init
            continue
        if not decs:
            return None
        leaves = [k for k in decs if e[k][1] == 'L']
        lastL = leaves[-1] if leaves else -1
        # also must outlive its last appearance (entering requires count>0): appearance time i means
        # the tile is entered by move i-1->i; its leave events come after. Last leave covers it.
        cand = [k for k in decs if k >= lastL] if leaves else decs
        if len(cand) > 1 and rng.random() < prefer_hits:
            j = rng.choice(cand[1:]) if leaves else rng.choice(cand)
        else:
            j = cand[0]
        tj = e[j][0]
        nd = sum(1 for k in decs if k <= j)
        ng = sum(1 for k, (t, kind) in enumerate(e) if kind == 'G' and k < j)
        init = nd - ng
        if init < 1:
            return None
        r = init
        for k, (t, kind) in enumerate(e):
            if k > j:
                break
            r += 1 if kind == 'G' else -1
            if k < j and r <= 0:
                return None
        if r != 0:
            return None
        counts[c] = init
    return counts


def make_stage(cells, start, counts, types=None, extra=None, walls=None, sid=0):
    types = types or {}
    extra = extra or {}
    cl = []
    for (x, y) in sorted(cells, key=lambda p: (p[1], p[0])):
        t = types.get((x, y), 'Normal')
        d = {"x": x, "y": y, "count": counts[(x, y)], "type": t}
        e = extra.get((x, y), {})
        if t == 'ShortCircuit':
            d['direction'] = e['direction']
        elif t == 'CrossBlast':
            d['properties'] = {"beamColor": e.get('beamColor', BEAMS[0])}
        elif t == 'FixedKnot':
            d['targetOrder'] = e['targetOrder']
            d['properties'] = {"isAbsolute": True}
        elif t == 'TwinLink':
            d['linkID'] = e['linkID']
            d['color'] = e.get('color', TWIN_COLORS[0])
        elif t == 'Igniter':
            d['targetID'] = e['targetID']
        elif t == 'Hidden':
            d['groupID'] = e['groupID']
        cl.append(d)
    w, h = dims(cells)
    s = {"stageID": sid, "width": w, "height": h, "startPoint": {"x": start[0], "y": start[1]},
         "config": copy.deepcopy(CONFIG), "cells": cl}
    if walls:
        wl = []
        for e in sorted(walls, key=lambda e: sorted(e)):
            a, b = sorted(e)
            d = (b[0] - a[0], b[1] - a[1])
            wl.append({"x": a[0], "y": a[1], "dir": DNAME[d]})
        s['walls'] = wl
    return s


def stage_walls(s):
    return zs.wall_edges(s)


def stage_cells(s):
    return {(c['x'], c['y']): c for c in s['cells'] if c.get('count', 0) > 0}


# ------------------------------------------------------------------------------- mechanics from walk
def sc_candidates(cells, walk):
    """cells where every leave goes the same direction and never entered from that direction."""
    M = len(walk) - 1
    leaves = {}
    entries = {}
    for i in range(M):
        a, b = walk[i], walk[i + 1]
        leaves.setdefault(a, set()).add((b[0] - a[0], b[1] - a[1]))
        entries.setdefault(b, set()).add((a[0] - b[0], a[1] - b[1]))
    out = {}
    for c in cells:
        L = leaves.get(c, set())
        if len(L) == 1:
            d = next(iter(L))
            if d not in entries.get(c, set()):
                out[c] = d
    return out


def first_entry(walk):
    fe = {}
    for i, c in enumerate(walk):
        if i > 0 and c not in fe:
            fe[c] = i
    return fe


def crossed_edges(walk):
    return {frozenset((walk[i], walk[i + 1])) for i in range(len(walk) - 1)}


def all_edges(cells):
    out = set()
    for (x, y) in cells:
        for dx, dy in ((1, 0), (0, 1)):
            q = (x + dx, y + dy)
            if q in cells:
                out.add(frozenset(((x, y), q)))
    return out
