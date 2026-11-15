"""Build every game sound in one palette so they all belong together (see soundkit.py).

usage: python3 -I Tools/DevScripts/make_sfx.py <project_root> [palette]   (palette: bubble (default) | wood | piano)
Writes 44.1 kHz stereo 16-bit WAVs into Assets/Resources/Sounds/.

Rules: only the melody (block notes) is pitched during play; tile effects are unpitched or picked samples;
celebrations are chords built on C major; the fever pops use A-minor pentatonic like bgm_fever.
Picked samples (samples/fcs, Gamemaster Audio "Fun Casual Sounds", purchased Asset Store pack):
  CrossBlast = cork pop, Charge = sci-fi power-up 02 (first 0.55 s), Blackout = dark bell chime (first 1.1 s).
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import soundkit as sk

ROOT = sys.argv[1]
PAL = sys.argv[2] if len(sys.argv) > 2 else 'bubble'
SND = os.path.join(ROOT, 'Assets', 'Resources', 'Sounds')
FCS = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'samples', 'fcs')


def out(name, x, peak):
    d = sk.write(os.path.join(SND, name + '.wav'), x, peak=peak)
    print(f'  {name:24s} {d:.2f}s')


def trimmed(sample, seconds, fade):
    x = sk.read(os.path.join(FCS, sample + '.wav'))[:int(seconds * sk.RATE)]
    return sk.fade_out(x, fade)


def n(name, octave=0):
    return sk.block_midi(PAL, name) + 12 * octave


def notes(midis, stagger, total, gains=None, length=None):
    parts = [(k * stagger, sk.note(PAL, m, length), (gains[k] if gains else 1.0)) for k, m in enumerate(midis)]
    return sk.mix(parts, total)


print('palette', PAL)
# ---- melody: 13 block notes, and a chord on each (root + fifth + octave: consonant with that note)
for name in sk.NOTE_NAMES:
    m = sk.block_midi(PAL, name)
    out(f'block_{name}', sk.note(PAL, m, 0.3), 0.55)
    out(f'sfx_chord_{name}', sk.chord(PAL, [m, m + 7, m + 12], stagger=0.03, length=0.4), 0.55)

# ---- special tiles (unpitched, or the picked samples)
out('bomb', sk.fade_out(sk.read(os.path.join(FCS, 'cork_pop_open_bottle_boing_01.wav')), 0.03), 0.7)
out('sfx_charge', trimmed('sci-fi_power_up_02', 0.55, 0.15), 0.55)
out('sfx_blackout', trimmed('game_over_dark_bell_chime_01', 1.1, 0.45), 0.6)
out('sfx_reveal', sk.sparkle(0.3, grains=9), 0.45)
out('sfx_fixed_knot', sk.click(0.07, lp_coef=0.55, decay=200, body=0.35, body_f=1600), 0.45)
out('sfx_move_denied', sk.thump(0.12, f0=210, f1=120, sweep=35, body_decay=30, puff=0.25, puff_lp=0.1, puff_decay=40), 0.35)

# ---- progress
out('clear', sk.mix([(0, notes([n('c'), n('e'), n('g'), n('c', 1)], 0.08, 0.9), 0.9),
                     (0.36, sk.chord(PAL, [n('c', 1), n('e', 1), n('g', 1)], length=0.8), 0.8)], 1.2), 0.65)
out('fail', notes([n('e'), n('c'), n('a', -1)], 0.14, 0.6, gains=[0.8, 0.75, 0.7]), 0.5)
out('new_stage', notes([n('g'), n('c', 1)], 0.09, 0.45, gains=[0.6, 0.7]), 0.4)
out('sfx_perfect', sk.mix([(0, sk.chord(PAL, [n('c'), n('e'), n('g')], length=0.5), 0.8),
                           (0.12, notes([n('c', 1), n('e', 1), n('g', 1)], 0.05, 0.45), 0.7)], 0.6), 0.6)
for i, name in enumerate(['c', 'd', 'e', 'g', 'co'], 1):
    out(f'sfx_heart_pop_{i}', sk.note(PAL, n(name, 1), 0.12), 0.4)

# ---- portal and fever
out('sfx_portal_suck', notes([n('co', 1), n('g', 1), n('e', 1), n('c', 1), n('g'), n('e'), n('c')], 0.05, 0.6,
                             gains=[1, 0.95, 0.9, 0.85, 0.8, 0.75, 0.7]), 0.5)
out('sfx_portal_emerge', notes([n('c'), n('g'), n('c', 1)], 0.06, 0.4), 0.5)
out('sfx_fever_start', notes([n('c'), n('e'), n('g'), n('c', 1), n('e', 1), n('g', 1)], 0.045, 0.5), 0.55)
for i in range(10):
    out(f'sfx_fever_pop_{i}', sk.fever_pop(i), 0.4)
out('bgm_fever', sk.fever_bgm(PAL), None)

# ---- UI
out('click', sk.note(PAL, n('c', 1), 0.07), 0.35)
out('ui_button', sk.note(PAL, n('g'), 0.07), 0.35)
out('confirm', notes([n('g'), n('c', 1)], 0.06, 0.25), 0.4)
out('cancel', notes([n('e'), n('c')], 0.06, 0.25), 0.4)
out('snackbar', sk.chord(PAL, [n('e'), n('g')], stagger=0.04, length=0.3), 0.35)
out('hint', sk.mix([(0, notes([n('c'), n('e'), n('g')], 0.06, 0.5), 0.8), (0.05, sk.sparkle(0.35, grains=6, seed=9), 0.5)], 0.55), 0.45)
out('skip', notes([n('c'), n('e'), n('g'), n('c', 1)], 0.035, 0.35), 0.4)
