"""Build extra sound effects from the existing block-note samples so they share the game's timbre and scale.
usage: python3 -I Tools/DevScripts/make_sfx.py <project_root>
Writes 48 kHz stereo 16-bit WAVs into Assets/Resources/Sounds/:
  sfx_combo_milestone.wav  C-E-G-C' arpeggio (combo x5, x10, ...)
  sfx_perfect.wav          C+E+G chord, then high C and G an octave up (one-stroke clear)
  sfx_heart_pop_1..5.wav   one short high note per heart, rising C-D-E-G-C'
  sfx_move_denied.wav      soft low thunk (move refused)
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
