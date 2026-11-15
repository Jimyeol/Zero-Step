"""Build sound-palette auditions: Tools/SoundLab/<palette>/*.wav + Tools/SoundLab/audition.html.

usage: python3 -I Tools/DevScripts/make_audition.py <project_root>
Columns: now (current game files), wood, bubble, piano. Each has the same set of clips, including a
"scene" clip that plays a melody with a bomb, a charge, a combo chord and the clear, to judge overlaps.
"""
import os, sys, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import soundkit as sk

ROOT = sys.argv[1]
OUT = os.path.join(ROOT, 'Tools', 'SoundLab')
SND = os.path.join(ROOT, 'Assets', 'Resources', 'Sounds')
MELODY = ['c', 'd', 'e', 'c', 'e', 'g', 'e', 'g', 'co', 'g', 'e', 'd', 'c']
STEP = 0.17

FX = {
    'wood': dict(bomb=dict(f0=140, f1=50, puff=0.55, puff_lp=0.10), click=dict(lp_coef=0.35, decay=160, body=0.5, body_f=1250),
                 knot=dict(lp_coef=0.3, decay=120, body=0.7, body_f=820)),
    'bubble': dict(bomb=dict(f0=230, f1=70, sweep=40, puff=0.5, puff_lp=0.22), click=dict(lp_coef=0.65, decay=320),
                   knot=dict(lp_coef=0.55, decay=200, body=0.35, body_f=1600)),
    'piano': dict(bomb=dict(f0=120, f1=45, puff=0.4, puff_lp=0.08), click=dict(lp_coef=0.3, decay=220),
                  knot=dict(lp_coef=0.25, decay=150, body=0.3, body_f=600)),
}


def melody_clip(palette):
    parts = []
    for k, name in enumerate(MELODY):
        m = sk.block_midi(palette, name)
        parts.append((k * STEP, sk.note(palette, m), 1.0))
        if k >= 3:   # combo octave layer, growing like the game
            parts.append((k * STEP, sk.note(palette, m + 12), min(0.12 + 0.04 * (k - 3), 0.4)))
    return sk.mix(parts, len(MELODY) * STEP + 0.5)


def combo_chord(palette, name='e'):
    m = sk.block_midi(palette, name)
    return sk.chord(palette, [m, m + 4, m + 7, m + 12], stagger=0.035)


def clear_clip(palette):
    base = sk.block_midi(palette, 'c')
    arp = sk.chord(palette, [base, base + 4, base + 7, base + 12], stagger=0.09)
    final = sk.chord(palette, [base + 12, base + 16, base + 19], stagger=0.0, length=0.8)
    return sk.mix([(0, arp, 0.9), (0.38, final, 0.8)], 1.2)


def fail_clip(palette):
    base = sk.block_midi(palette, 'c')
    return sk.mix([(0, sk.note(palette, base + 4), 0.8), (0.16, sk.note(palette, base - 1), 0.75)], 0.6)


def clips_for(palette):
    fx = FX[palette]
    c = {}
    c['melody'] = melody_clip(palette)
    c['combo'] = combo_chord(palette)
    c['bomb'] = sk.thump(**fx['bomb'])
    c['charge'] = sk.whoosh(0.2, rising=True)
    c['reveal'] = sk.sparkle()
    c['knot'] = sk.click(0.07, **fx['knot'])
    c['blackout'] = sk.whoosh(0.4, rising=False, lo=0.01, hi=0.2)
    c['clear'] = clear_clip(palette)
    c['fail'] = fail_clip(palette)
    c['button'] = sk.click(0.05, **fx['click'])
    return c


def scene(palette, c):
    """Melody with effects on top, as in play: bomb on note 4, charge on note 7, combo chord replaces note 9, clear."""
    parts = []
    for k, name in enumerate(MELODY[:10]):
        t = k * STEP
        if k == 8:
            parts.append((t, c['combo'], 0.9))
            continue
        parts.append((t, sk.note(palette, sk.block_midi(palette, name)), 1.0))
        if k == 3:
            parts.append((t, c['bomb'], 0.8))
        if k == 6:
            parts.append((t, c['charge'], 0.6))
    parts.append((10 * STEP + 0.05, c['clear'], 0.9))
    return sk.mix(parts, 10 * STEP + 1.3)


def fever_clip(palette, seconds=6.0):
    loop = sk.fever_bgm(palette)
    music = loop[:int(seconds * sk.RATE)]
    r = random.Random(5)
    parts = [(0, music, 0.55)]
    t, step = 0.6, 0
    while t < seconds - 0.2:
        parts.append((t, sk.fever_pop(step % 10), 0.35))
        step += 1
        t += r.choice([0.1, 0.1, 0.2, 0.3])
    return sk.mix(parts, seconds)


def now_clips():
    """Current game sounds for reference."""
    def f(name):
        return sk.read(os.path.join(SND, name + '.wav'))
    notes = {n: f('block_' + n) for n in set(MELODY)}
    parts = [(k * STEP, notes[n], 1.0) for k, n in enumerate(MELODY)]
    c = {'melody': sk.mix(parts, len(MELODY) * STEP + 0.5), 'combo': f('sfx_combo_milestone'), 'bomb': f('bomb'),
         'charge': f('sfx_charge'), 'reveal': f('sfx_reveal'), 'knot': f('sfx_fixed_knot'), 'blackout': f('sfx_blackout'),
         'clear': f('clear'), 'fail': f('fail')[:int(0.65 * sk.RATE)], 'button': f('ui_button')}
    sp = [(k * STEP, notes[n], 1.0) for k, n in enumerate(MELODY[:10])]
    sp += [(3 * STEP, c['bomb'], 0.8), (6 * STEP, c['charge'], 0.6), (8 * STEP, c['combo'], 0.75), (10 * STEP + 0.05, c['clear'], 0.9)]
    c['scene'] = sk.mix(sp, 10 * STEP + 2.1)
    music = f('bgm_fever')[:int(6 * sk.RATE)]
    c['fever'] = sk.mix([(0, music, 0.55)], 6.0)
    return c


LABELS = [
    ('scene', '실제 플레이처럼 (멜로디 + 폭발 + 충전 + 콤보 + 클리어)', '겹침이 거슬리는지 여기서 판단해 주세요'),
    ('melody', '칸 멜로디 (콤보가 쌓이며 한 옥타브 위가 겹침)', ''),
    ('combo', '콤보 5·10 (멜로디 음을 화음으로)', ''),
    ('bomb', '십자폭발', '음높이 없는 소리'),
    ('charge', '충전', '음높이 없는 소리'),
    ('reveal', '숨은 칸 켜짐', '음높이 없는 소리'),
    ('knot', '순서 칸 제때 밟음', '음높이 없는 소리'),
    ('blackout', '깜깜 칸', '음높이 없는 소리'),
    ('clear', '클리어', '1.2초 이하'),
    ('fail', '실패', '0.6초 이하'),
    ('button', '버튼', ''),
    ('fever', '피버타임 (배경음악 + 칸 없앨 때 뿅뿅)', ''),
]
COLUMNS = [('now', '지금 게임'), ('wood', 'A. 말랑 나무'), ('bubble', 'B. 몽글 전자음'), ('piano', 'C. 피아노 유지')]


def main():
    made = {}
    for key, _ in COLUMNS:
        if key == 'now':
            c = now_clips()
        else:
            c = clips_for(key)
            c['scene'] = scene(key, c)
            c['fever'] = fever_clip(key)
        for name, sig in c.items():
            sk.write(os.path.join(OUT, key, name + '.wav'), sig, peak=0.75 if name not in ('fever',) else None)
        made[key] = c
        print('done', key)
    rows = []
    for name, title, note in LABELS:
        cells = ''.join(f'<td><audio controls preload="none" src="{key}/{name}.wav"></audio></td>' for key, _ in COLUMNS)
        sub = f'<div class="note">{note}</div>' if note else ''
        rows.append(f'<tr><th>{title}{sub}</th>{cells}</tr>')
    head = ''.join(f'<th>{label}</th>' for _, label in COLUMNS)
    html = f'''<!doctype html>
<html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Zero Step Sound Lab</title>
<style>
body {{ font-family: -apple-system, sans-serif; margin: 16px; background: #f3eee6; color: #2b2a28; }}
table {{ border-collapse: collapse; width: 100%; }}
th, td {{ border-bottom: 1px solid #ddd3c4; padding: 8px; text-align: left; vertical-align: middle; }}
thead th {{ position: sticky; top: 0; background: #e6dfd3; }}
audio {{ width: 190px; height: 32px; }}
.note {{ font-weight: normal; font-size: 12px; color: #6e675e; }}
</style></head><body>
<h1>Zero Step 소리 시안</h1>
<p>같은 줄끼리 비교해 주세요. "실제 플레이처럼" 줄에서 겹침이, 나머지 줄에서 음색이 잘 들려요.</p>
<table><thead><tr><th></th>{head}</tr></thead><tbody>
{''.join(rows)}
</tbody></table></body></html>
'''
    with open(os.path.join(OUT, 'audition.html'), 'w', encoding='utf-8') as f:
        f.write(html)
    print('wrote', os.path.join(OUT, 'audition.html'))


if __name__ == '__main__':
    main()
