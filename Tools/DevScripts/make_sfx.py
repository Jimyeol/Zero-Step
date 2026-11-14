"""Build extra sound effects from the existing block-note samples so they share the game's timbre and scale.
usage: python3 -I Tools/DevScripts/make_sfx.py <project_root>
Writes 48 kHz stereo 16-bit WAVs into Assets/Resources/Sounds/:
  sfx_combo_milestone.wav  C-E-G-C' arpeggio (combo x5, x10, ...)
  sfx_perfect.wav          C+E+G chord, then high C and G an octave up (one-stroke clear)
  sfx_heart_pop_1..5.wav   one short high note per heart, rising C-D-E-G-C'
  sfx_move_denied.wav      soft low thunk (move refused)
  sfx_twin.wav             C+G an octave up together (TwinLink partners drop)
  sfx_charge.wav           quick E-G rise an octave up (Charge adds +1)
  sfx_reveal.wav           G-C'-E' sparkle two octaves up (Igniter opens Hidden tiles)
  sfx_fixed_knot.wav       C+G low click (order tile entered on time)
  sfx_blackout.wav         C'-G-E-C falling an octave down (numbers turn into ?)
  sfx_fever_start.wav      C-E-G-C'-E'-G' rising run (fever board starts)
  sfx_portal_suck.wav      high-to-low spiral run (Zero is sucked into the portal)
  sfx_portal_emerge.wav    C-G-C' pop (phase-2 board appears)
"""
import os, sys, wave, struct, math

ROOT = sys.argv[1]
SND = os.path.join(ROOT, 'Assets', 'Resources', 'Sounds')
RATE = 48000

def read(name):
    with wave.open(os.path.join(SND, name)) as w:
        ch, width, rate, n = w.getnchannels(), w.getsampwidth(), w.getframerate(), w.getnframes()
        raw = w.readframes(n)
    assert rate == RATE, (name, rate)
    out = []
    step = width * ch
    for i in range(n):
        frame = raw[i * step:(i + 1) * step]
        vals = []
        for c in range(ch):
            b = frame[c * width:(c + 1) * width]
            if width == 3:
                v = int.from_bytes(b, 'little', signed=True) / 8388608.0
            elif width == 2:
                v = int.from_bytes(b, 'little', signed=True) / 32768.0
            else:
                raise ValueError(width)
            vals.append(v)
        out.append(sum(vals) / len(vals))   # mono mix
    return out

def resample(x, factor):
    """factor > 1 raises the pitch (and shortens), linear interpolation."""
    n = int(len(x) / factor)
    out = []
    for i in range(n):
        p = i * factor
        j = int(p)
        f = p - j
        a = x[j]
        b = x[j + 1] if j + 1 < len(x) else 0.0
        out.append(a + (b - a) * f)
    return out

def fade(x, fade_in=0.002, fade_out=0.03, length=None):
    if length is not None:
        x = x[:int(length * RATE)]
    x = list(x)
    fi, fo = int(fade_in * RATE), int(fade_out * RATE)
    for i in range(min(fi, len(x))):
        x[i] *= i / max(1, fi)
    for i in range(min(fo, len(x))):
        x[len(x) - 1 - i] *= i / max(1, fo)
    return x

def mix(parts, total):
    buf = [0.0] * int(total * RATE)
    for start, sig, gain in parts:
        o = int(start * RATE)
        for i, v in enumerate(sig):
            if o + i < len(buf):
                buf[o + i] += v * gain
    return buf

def normalize(x, peak=0.5):
    m = max(1e-9, max(abs(v) for v in x))
    return [v * peak / m for v in x]

def write(name, x):
    path = os.path.join(SND, name)
    with wave.open(path, 'wb') as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(RATE)
        frames = bytearray()
        for v in x:
            s = max(-32767, min(32767, int(v * 32767)))
            frames += struct.pack('<hh', s, s)
        w.writeframes(bytes(frames))
    print('wrote', name, f'{len(x) / RATE:.3f}s')

note = {k: read(f'block_{k}.wav') for k in ['c', 'd', 'e', 'g', 'co']}

# combo milestone: quick rising arpeggio
arp = mix([(0.000, note['c'], 1.0), (0.055, note['e'], 1.0), (0.110, note['g'], 1.0), (0.165, note['co'], 1.1)], 0.32)
write('sfx_combo_milestone.wav', normalize(fade(arp, fade_out=0.05), 0.55))

# perfect: chord, then a bright octave-up answer
hi_c = resample(note['co'], 2.0)
hi_g = resample(note['g'], 4.0)
perfect = mix([(0.0, note['c'], 0.8), (0.0, note['e'], 0.8), (0.0, note['g'], 0.8),
               (0.10, note['co'], 1.0), (0.10, hi_c, 0.6), (0.19, hi_g, 0.5)], 0.42)
write('sfx_perfect.wav', normalize(fade(perfect, fade_out=0.08), 0.6))

# heart pops: one octave up, very short, rising per heart
for i, k in enumerate(['c', 'd', 'e', 'g', 'co'], 1):
    pop = fade(resample(note[k], 2.0), fade_out=0.025, length=0.06)
    write(f'sfx_heart_pop_{i}.wav', normalize(pop, 0.42))

# move denied: low, short, soft
low = fade(resample(note['c'], 0.5), fade_out=0.06, length=0.11)
write('sfx_move_denied.wav', normalize(low, 0.35))

# --- D-2: special tiles, fever, portal
up = {k: resample(note[k], 2.0) for k in note}
up2 = {k: resample(note[k], 4.0) for k in note}
down = {k: resample(note[k], 0.5) for k in note}

twin = mix([(0.0, up['c'], 0.9), (0.0, up['g'], 0.8)], 0.13)
write('sfx_twin.wav', normalize(fade(twin, fade_out=0.04), 0.4))

charge = mix([(0.0, up['e'], 0.8), (0.045, up['g'], 1.0)], 0.15)
write('sfx_charge.wav', normalize(fade(charge, fade_out=0.04), 0.4))

reveal = mix([(0.0, up['g'], 0.8), (0.05, up['co'], 0.9), (0.10, up2['e'], 0.8)], 0.26)
write('sfx_reveal.wav', normalize(fade(reveal, fade_out=0.06), 0.42))

knot = mix([(0.0, note['c'], 1.0), (0.0, note['g'], 0.7)], 0.12)
write('sfx_fixed_knot.wav', normalize(fade(knot, fade_out=0.05), 0.42))

blackout = mix([(0.0, down['co'], 1.0), (0.07, down['g'], 1.0), (0.14, down['e'], 1.0), (0.21, down['c'], 1.1)], 0.42)
write('sfx_blackout.wav', normalize(fade(blackout, fade_out=0.1), 0.5))

run = [note['c'], note['e'], note['g'], note['co'], up['e'], up['g']]
fever = mix([(0.045 * i, s, 0.9 + 0.04 * i) for i, s in enumerate(run)], 0.42)
write('sfx_fever_start.wav', normalize(fade(fever, fade_out=0.08), 0.55))

spiral = [up2['c'], up['g'], up['e'], up['c'], note['g'], note['e'], note['c'], down['g']]
suck = mix([(0.055 * i, s, 1.0 - 0.06 * i) for i, s in enumerate(spiral)], 0.6)
write('sfx_portal_suck.wav', normalize(fade(suck, fade_out=0.14), 0.5))

emerge = mix([(0.0, note['c'], 0.8), (0.06, note['g'], 0.9), (0.12, note['co'], 1.0), (0.12, up['co'], 0.5)], 0.32)
write('sfx_portal_emerge.wav', normalize(fade(emerge, fade_out=0.07), 0.5))
