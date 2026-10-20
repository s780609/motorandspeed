"""用程式合成遊戲音效（WAV，16-bit 單聲道），不使用任何外部素材。

引擎聲是可無縫循環的樣本，遊戲中用 AudioSource.pitch 改變轉速、用音量表現油門。
做法：每次點火產生一個「衰減共振」脈衝（幾個阻尼正弦 + 一小段雜訊），依點火頻率重複排列，
再把頭尾交叉淡化，循環播放時不會有喀聲。
執行：python tools/generate_audio.py
"""
import math
import random
import struct
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "Assets" / "Audio"
SR = 44100
LOOP_FADE = 0.25  # 循環樣本頭尾交叉淡化的長度（秒）


# ---------- 基本工具 ----------

def zeros(seconds):
    return [0.0] * int(round(seconds * SR))


def white(count, rng):
    return [rng.uniform(-1.0, 1.0) for _ in range(count)]


def lowpass(samples, cutoff):
    """一階低通。"""
    a = 1.0 - math.exp(-2.0 * math.pi * cutoff / SR)
    out, y = [], 0.0
    for x in samples:
        y += a * (x - y)
        out.append(y)
    return out


def highpass(samples, cutoff):
    low = lowpass(samples, cutoff)
    return [x - l for x, l in zip(samples, low)]


def bandpass(samples, center, q):
    """RBJ 雙二階帶通（峰值增益 0 dB）。"""
    w0 = 2.0 * math.pi * center / SR
    alpha = math.sin(w0) / (2.0 * q)
    a0 = 1.0 + alpha
    b0, b2 = alpha / a0, -alpha / a0
    a1, a2 = -2.0 * math.cos(w0) / a0, (1.0 - alpha) / a0
    out = []
    x1 = x2 = y1 = y2 = 0.0
    for x in samples:
        y = b0 * x + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1, y2, y1 = x1, x, y1, y
        out.append(y)
    return out


def mix_into(target, source, start, gain=1.0):
    for i, v in enumerate(source):
        j = start + i
        if 0 <= j < len(target):
            target[j] += v * gain


def normalize(samples, peak):
    top = max(abs(v) for v in samples) or 1.0
    return [v * peak / top for v in samples]


def soft_clip(samples, drive):
    norm = math.tanh(drive)
    return [math.tanh(v * drive) / norm for v in samples]


def make_loop(samples, length):
    """把 length 之後多算的部分交叉淡化到開頭，循環接縫處就會連續。"""
    fade = int(LOOP_FADE * SR)
    out = samples[:length]
    for i in range(fade):
        w = i / fade
        out[i] = samples[i] * w + samples[length + i] * (1.0 - w)
    return out


def envelope(count, attack, decay):
    """快速起音、指數衰減的包絡。"""
    a = max(1, int(attack * SR))
    return [(i / a if i < a else 1.0) * math.exp(-i / SR / decay) for i in range(count)]


def tone(freq, seconds, attack, decay, harmonics=((1, 1.0),)):
    count = int(seconds * SR)
    env = envelope(count, attack, decay)
    return [env[i] * sum(amp * math.sin(2 * math.pi * freq * h * i / SR) for h, amp in harmonics) for i in range(count)]


def write_wav(name, samples):
    OUT.mkdir(parents=True, exist_ok=True)
    path = OUT / f"{name}.wav"
    with wave.open(str(path), "wb") as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(SR)
        wav.writeframes(b"".join(struct.pack("<h", int(max(-1.0, min(1.0, v)) * 32767)) for v in samples))
    print(f"{path.relative_to(ROOT)}  {len(samples) / SR:.2f}s")


# ---------- 引擎 ----------

def engine_loop(fire_hz, cycles, modes, noise, noise_band, noise_decay, body, drive, seed, whine=()):
    """
    fire_hz：點火頻率（樣本的基準轉速）；cycles：循環內的點火次數（循環長度 = cycles / fire_hz）
    modes：每次點火的共振 (頻率, 衰減秒數, 振幅)
    noise：點火雜訊量；noise_band：雜訊帶通 (中心頻率, Q)；noise_decay：雜訊衰減秒數
    body：基頻與倍頻的持續正弦 (倍數, 振幅)；drive：飽和失真量（越大越沙啞）
    whine：與轉速無關的持續高頻（變速箱、渦輪）(頻率, 振幅)，頻率會修成循環內整數週期
    """
    rng = random.Random(seed)
    length = int(round(cycles / fire_hz * SR))
    total = length + int(LOOP_FADE * SR)
    out = [0.0] * total
    period = SR / fire_hz

    # 單次點火的脈衝波形
    pulse_len = int(max(decay for _, decay, _ in modes) * 6 * SR)
    pulse = [sum(amp * math.exp(-i / SR / decay) * math.sin(2 * math.pi * freq * i / SR) for freq, decay, amp in modes) for i in range(pulse_len)]
    noise_len = int(noise_decay * 6 * SR)
    noise_env = [math.exp(-i / SR / noise_decay) for i in range(noise_len)]
    noise_src = bandpass(white(total + noise_len, rng), noise_band[0], noise_band[1])
    noise_src = normalize(noise_src, 1.0)

    # 從負的時間開始排脈衝，開頭就有前一次點火的尾巴
    first = -int(math.ceil(pulse_len / period)) - 1
    k = first
    while k * period < total:
        start = int(round(k * period))
        gain = 1.0 + rng.uniform(-0.12, 0.12)
        mix_into(out, pulse, start, gain)
        if noise > 0:
            for i in range(noise_len):
                j = start + i
                if 0 <= j < total:
                    out[j] += noise * gain * noise_env[i] * noise_src[j]
        k += 1

    out = normalize(out, 1.0)
    for i in range(total):
        t = i / SR
        out[i] += sum(amp * math.sin(2 * math.pi * fire_hz * h * t) for h, amp in body)
        for freq, amp in whine:
            f = round(freq * length / SR) * SR / length
            out[i] += amp * math.sin(2 * math.pi * f * t)

    out = soft_clip(normalize(out, 1.0), drive)
    out = highpass(out, 25.0)

    return normalize(make_loop(out, length), 0.85)


def generate_engines():
    # 一般速克達（四行程單缸 125cc）：嗡嗡的鼻音
    write_wav("engine_scooter", engine_loop(
        fire_hz=33.0, cycles=66,
        modes=[(160, 0.012, 1.0), (540, 0.006, 0.6), (1450, 0.0022, 0.25)],
        noise=0.45, noise_band=(1500, 0.8), noise_decay=0.008,
        body=[(1, 0.35), (2, 0.2)], drive=1.8, seed=1))

    # 8+9 改裝車：直通排氣管，更大聲更沙啞
    write_wav("engine_scooter_tuned", engine_loop(
        fire_hz=36.0, cycles=72,
        modes=[(130, 0.016, 1.0), (720, 0.005, 0.85), (2300, 0.002, 0.45)],
        noise=0.9, noise_band=(2200, 0.7), noise_decay=0.01,
        body=[(1, 0.4), (2, 0.25), (3, 0.12)], drive=3.2, seed=2))

    # 汽車（四缸汽油）：低沉平順
    write_wav("engine_car", engine_loop(
        fire_hz=50.0, cycles=100,
        modes=[(95, 0.02, 1.0), (260, 0.01, 0.45), (820, 0.003, 0.12)],
        noise=0.12, noise_band=(900, 0.7), noise_decay=0.01,
        body=[(1, 0.55), (2, 0.25), (4, 0.08)], drive=1.2, seed=3))

    # 大貨車（柴油）：低頻隆隆聲加上柴油敲缸的喀喀聲
    write_wav("engine_truck", engine_loop(
        fire_hz=42.0, cycles=84,
        modes=[(68, 0.028, 1.0), (175, 0.012, 0.6), (3100, 0.0015, 0.45)],
        noise=0.6, noise_band=(2800, 1.2), noise_decay=0.003,
        body=[(1, 0.6), (0.5, 0.25), (2, 0.2)], drive=1.7, seed=4))

    # 公車（柴油）：更低沉，帶著變速箱 / 渦輪的高頻嗚聲
    write_wav("engine_bus", engine_loop(
        fire_hz=36.0, cycles=72,
        modes=[(55, 0.032, 1.0), (150, 0.016, 0.6), (2600, 0.0015, 0.3)],
        noise=0.45, noise_band=(2400, 1.0), noise_decay=0.003,
        body=[(1, 0.65), (0.5, 0.3), (2, 0.15)], drive=1.5, seed=5,
        whine=((1150, 0.07), (1730, 0.035))))


# ---------- 喇叭、碰撞 ----------

def generate_horn():
    # 機車電喇叭：兩個接近的音（420 / 500 Hz）疊在一起的刺耳嗶聲；0.5 秒內都是整數週期，可直接循環
    length = int(0.5 * SR)
    out = []
    for i in range(length):
        t = i / SR
        v = 0.0
        for freq in (420.0, 500.0):
            for h in range(1, 14):
                v += math.sin(2 * math.pi * freq * h * t) / h
        out.append(v)
    out = soft_clip(normalize(out, 1.0), 2.5)
    write_wav("horn", normalize(out, 0.8))


def generate_crash():
    rng = random.Random(7)
    seconds = 0.9
    count = int(seconds * SR)
    out = [0.0] * count
    # 撞擊的悶響：音高往下掉的低頻
    phase = 0.0
    for i in range(count):
        t = i / SR
        freq = 45 + 70 * math.exp(-t / 0.05)
        phase += 2 * math.pi * freq / SR
        out[i] += 1.0 * math.sin(phase) * math.exp(-t / 0.12)
    # 塑膠車殼碎裂的雜訊
    crunch = lowpass(white(count, rng), 3500)
    env = envelope(count, 0.001, 0.07)
    for i in range(count):
        out[i] += 1.4 * crunch[i] * env[i]
    # 金屬零件的不和諧共振
    for freq, decay, amp in ((930, 0.18, 0.25), (1370, 0.13, 0.2), (2210, 0.09, 0.15), (3170, 0.06, 0.1)):
        mix_into(out, tone(freq, seconds, 0.001, decay), 0, amp)
    # 零件散落的喀啦聲
    for _ in range(9):
        start = int(rng.uniform(0.08, 0.6) * SR)
        click = [v * math.exp(-i / SR / 0.006) for i, v in enumerate(highpass(white(int(0.03 * SR), rng), 1500))]
        mix_into(out, click, start, rng.uniform(0.15, 0.35))
    write_wav("crash", normalize(soft_clip(normalize(out, 1.0), 1.5), 0.9))


# ---------- 街道環境音 ----------

def generate_ambience():
    # 不用寬頻雜訊（聽起來像麥克風底噪的沙沙聲），只留很低的隆隆聲，車流改用諧波合成的遠方引擎聲
    rng = random.Random(9)
    seconds = 12.0
    length = int(seconds * SR)
    total = length + int(LOOP_FADE * SR)

    # 遠方車流的低頻隆隆聲：三次低通只剩 100 Hz 以下，帶整數週期的慢速起伏
    rumble = normalize(lowpass(lowpass(lowpass(white(total, rng), 100), 100), 100), 1.0)
    out = []
    for i in range(total):
        slow = 1.0 + 0.25 * math.sin(2 * math.pi * (i / SR) * 2 / seconds)
        out.append(0.35 * rumble[i] * slow)
    out = make_loop(out, length)

    # 遠方經過的車：引擎諧波由小變大再變小，經過時音高往下掉（都卜勒），超過循環長度的部分繞回開頭
    passes = [(1.5, 1.6, 48.0), (4.2, 2.2, 36.0), (6.8, 1.4, 55.0), (9.6, 2.0, 42.0), (11.4, 1.2, 60.0)]
    for center, width, base in passes:
        start = center - width * 1.5
        count = int(width * 3 * SR)
        phase = 0.0
        car = []
        for i in range(count):
            t = start + i / SR
            freq = base * (1.0 + 0.04 * math.tanh((center - t) / (width / 4)))
            phase += 2 * math.pi * freq / SR
            swell = math.exp(-((t - center) / (width / 2)) ** 2)
            car.append(swell * sum(math.sin(h * phase) / h for h in range(1, 7)))
        car = lowpass(lowpass(car, 600), 600)
        offset = int(start * SR)
        for i, v in enumerate(car):
            out[(offset + i) % length] += 0.5 * v

    # 遠方的機車喇叭（很小聲）
    beep = tone(470, 0.12, 0.005, 0.2, ((1, 1.0), (2, 0.4), (3, 0.25)))
    mix_into(out, beep, int(7.3 * SR), 0.03)
    mix_into(out, beep, int(7.48 * SR), 0.03)

    write_wav("ambience_street", normalize(out, 0.5))


# ---------- UI / 比賽提示音 ----------

def generate_ui():
    write_wav("ui_click", normalize(tone(1800, 0.05, 0.001, 0.008, ((1, 1.0), (2.7, 0.3))), 0.6))
    write_wav("countdown_tick", normalize(tone(880, 0.22, 0.003, 0.09, ((1, 1.0), (2, 0.3), (3, 0.1))), 0.8))
    write_wav("countdown_go", normalize(
        [a + b for a, b in zip(tone(1320, 0.7, 0.003, 0.3, ((1, 1.0), (2, 0.3))), tone(1760, 0.7, 0.003, 0.3, ((1, 0.8),)))], 0.85))

    # 送達：上行三音的鐘聲
    chime = zeros(0.8)
    for index, freq in enumerate((1046.5, 1318.5, 1568.0)):
        mix_into(chime, tone(freq, 0.6, 0.002, 0.18, ((1, 1.0), (2.01, 0.25), (3.98, 0.08))), int(index * 0.09 * SR))
    write_wav("checkpoint", normalize(chime, 0.8))

    # 完賽 / 結算：小號角
    fanfare = zeros(1.6)
    notes = [(523.25, 0.0, 0.14), (659.25, 0.15, 0.14), (783.99, 0.3, 0.14), (1046.5, 0.45, 0.5)]
    square = tuple((h, 1.0 / h) for h in range(1, 10, 2))
    for freq, start, decay in notes:
        mix_into(fanfare, tone(freq, 1.1, 0.01, decay, square), int(start * SR))
    write_wav("finish", normalize(soft_clip(normalize(fanfare, 1.0), 1.2), 0.8))


if __name__ == "__main__":
    generate_engines()
    generate_horn()
    generate_crash()
    generate_ambience()
    generate_ui()
