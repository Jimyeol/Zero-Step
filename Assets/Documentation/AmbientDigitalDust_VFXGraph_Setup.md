# VFX Graph: 앰비언트 디지털 더스트 (Ambient Dust & Bokeh)

검은 배경 위에서 미세한 금색/은색 입자가 액체처럼 부드럽게 떠다니고, 카메라와의 거리에 따라 Bokeh(심도) 느낌이 나도록 하는 VFX Graph 구성 가이드입니다.

---

## 1. 전체 개요

| 목표 | 방법 |
|------|------|
| 미세한 금/은색 입자 | Color over Lifetime + 랜덤 금/은 톤, 아주 작은 Size |
| 액체처럼 부드러운 움직임 | Curl Noise 또는 Point Cache 기반 속도 |
| 카메라 주변에만 생성 | Spawn: Shape = Cone/Sphere, 카메라 근처 위치 |
| Bokeh/심도 느낌 | 거리 기반 Size·Alpha + (선택) URP DoF |

---

## 2. 그래프 구조 요약

```
[Spawn] → [Initialize] → [Update] → [Output]
   ↓           ↓             ↓
 Set Count  Set Position   Set Velocity (Fluid)
 Set Lifetime  Set Size     (Curl Noise / Point Cache)
              Set Color (Gold/Silver)
```

---

## 3. 단계별 노드 구성

### 3.1 Spawn 컨텍스트

- **Constant Spawn Rate**  
  - Rate: **80 ~ 150** (적당한 밀도, 성능에 따라 조절)
- **Single Burst**는 사용하지 않고, 지속적으로 소량씩 생성해 “공기 중 먼지”처럼 보이게 합니다.

**선택: 카메라 기준 생성**

- **Set Spawn Position**을 두고, **Position (Shape: Cone)** 또는 **Sphere** 사용.
  - **Cone**: 카메라 전방 원뿔 안에서 생성 → 화면에 보이는 영역만 채움.
  - **Sphere**: 카메라 위치 기준 반경 2~5m 구 안에서 생성.
- Cone 사용 시:
  - **Cone** → Radius (base): 3~8, **Arc** 360°, **Height** 5~15.
  - 원하면 **Custom**으로 **Spawn Position**에 `Camera.position + Random inside Cone` 형태로 직접 구성할 수도 있습니다.

---

### 3.2 Initialize 파티클 속성

**Position**

- Spawn에서 준 위치 그대로 사용하거나, **Add Position**으로 카메라 로컬 오프셋만 줍니다.

**Size (Bokeh 느낌의 기초)**

- **Random Uniform**: min **0.02**, max **0.12** (매우 작은 먼지).
- 나중에 **Update**에서 “카메라와의 거리”로 크기를 키우면, 가까운 입자는 더 크게 보여 Bokeh처럼 보입니다.

**Color (금색 / 은색)**

- **Random Uniform** 또는 **Gradient**로 두 가지 톤을 섞습니다.
  - **Color A**: 금색 계열 (R: 0.95, G: 0.85, B: 0.5)
  - **Color B**: 은색/백색 계열 (R: 0.9, G: 0.92, B: 1.0)
- **Random** 0~1로 A/B 블렌드하면 일부는 금, 일부는 은처럼 보입니다.
- **Alpha**: 0.3 ~ 0.8 (전체적으로 부드럽고 반투명).

**Lifetime**

- **Random Uniform**: 4 ~ 10초 (천천히 움직이면서 오래 보이게).

---

### 3.3 Fluid Motion (액체처럼 부드럽게)

**방법 A: Curl Noise (권장)**

- **Update** 컨텍스트에서:
  - **Curl Noise** 노드 추가.
  - **Position** → 파티클 **position** (또는 position * scale로 타일링).
  - **Scale**: 0.5 ~ 2 (값이 작을수록 더 굽이치는 흐름).
  - **Speed**: 0.2 ~ 0.5 (천천히 변하는 흐름).
- **Curl Noise** 출력을 **Velocity**에 더합니다.
  - **Add Velocity** 또는 **Set Velocity**에 기존 velocity + curl 결과를 넣습니다.
- 기존 속도가 0이면 “제자리에서 일렁이는” 느낌, 작은 **Constant**를 더하면 천천히 표류하는 느낌입니다.

**방법 B: Point Cache**

- 유기적인 궤적을 쓰고 싶다면:
  - 외부 도구(예: Houdini, 다른 DCC)에서 유체/연기 시뮬레이션을 베이킹해 **Point Cache** 에셋으로 저장.
  - VFX Graph에서 **Sample Point Cache**로 position/velocity를 읽어 **Set Position** / **Set Velocity**에 연결.
  - “액체처럼 흐른다”는 느낌을 미리 디자인한 경로로 줄 수 있습니다.

**방법 C: 간단한 노이즈 이동**

- **Perlin Noise** 또는 **Voronoi**로 **Offset**을 만들고, **Velocity**에 더해 줍니다.
  - **Time**으로 오프셋을 바꿔 주면 시간에 따라 부드럽게 움직입니다.

실제 연출은 **Curl Noise**만으로도 “디지털 더스트가 액체처럼 일렁이는” 느낌을 내기 충분합니다.

---

### 3.4 Bokeh 효과 (거리 기반)

**의도:**  
가까운 입자는 크고 부드럽게(흐릿하게), 먼 입자는 작고 선명하게 보이게 해 DoF/Bokeh 인상을 냅니다.

**VFX Graph 내에서:**

1. **Camera** 블록으로 카메라 위치/방향을 가져옵니다 (또는 **Global**로 카메라 위치를 넘겨 받습니다).
2. **Distance** 노드: 파티클 **position**과 카메라 **position** 사이 거리.
3. **Normalize (0–1)**  
   - Near: 0, Far: 1 로 매핑 (예: 거리 1~20m를 0~1로).
4. **Lerp**:
   - **Size**: near(0) → 크게 (예: 0.15), far(1) → 작게 (예: 0.02).  
     → 가까운 파티클이 크게 보여 “블러 서클”처럼 보임.
5. **Alpha / Color over Lifetime**  
   - Near일수록 alpha를 약간 낮춰 “흐릿함”을 강조할 수 있습니다 (선택).

**수식 예 (개념):**

- `t = saturate((distance(particlePos, cameraPos) - near) / (far - near))`
- `size = lerp(sizeNear, sizeFar, t)`
- `alpha = lerp(alphaNear, alphaFar, t)` (선택)

**URP Post Processing과 함께:**

- **Depth of Field** (DoF) 활성화:
  - Focus Distance: 플레이어/중심 오브젝트 거리.
  - Aperture: 조리개 열기 (Bokeh 강도).
- VFX 파티클은 실제로 3D 공간에 있으므로, DoF가 자동으로 가까이 있는 파티클은 흐리게, 먼 것은 선명하게 만듭니다.
- VFX에서 “거리 기반 Size”까지 적용하면, 가까운 입자가 더 크게 보여 Bokeh 서클이 더 잘 보입니다.

---

## 4. GPU 파티클 설정

- **Capacity**: 2000 ~ 5000 (화면에 동시에 보이는 더스트 양에 맞춤).
- **Culling**: **Recycled** (삭제된 파티클 슬롯 재사용).
- **Bounds**: 카메라 주변 구/박스로 설정하거나, **Don’t Cull**로 두고 거리 기반으로만 처리해도 됩니다.

---

## 5. 머티리얼 / 렌더링

- **Quad** 또는 **Point** 렌더링.
- **Additive** 또는 **Soft Additive** 블렌딩 → 금/은색이 겹칠수록 밝아져 “빛 먼지” 느낌.
- **Black 배경**이면 검정은 그대로 두고, 입자만 금/은색으로 보이면 됩니다.
- 필요 시 **HDR Color**로 색을 주어 Bloom과 결합하면 더 반짝이게 할 수 있습니다.

---

## 6. 체크리스트

| 항목 | 확인 |
|------|------|
| Spawn을 카메라 전방 Cone/Sphere로 제한 | ☐ |
| Curl Noise(또는 Point Cache)로 Fluid Motion | ☐ |
| 금/은색 Random Color + 부드러운 Alpha | ☐ |
| 거리 기반 Size (Near 크게, Far 작게) | ☐ |
| (선택) URP DoF로 실제 Bokeh | ☐ |
| GPU 파티클 Capacity·Culling 설정 | ☐ |
| Additive 머티리얼 + 검은 배경 | ☐ |

---

## 7. 참고

- **Point Cache**는 “미리 만든 유체 궤적”을 쓰고 싶을 때 활용하고,  
  **Curl Noise**는 별도 에셋 없이 그래프만으로 액체 같은 움직임을 낼 때 유리합니다.
- Bokeh는 “VFX 거리 기반 Size + Alpha”로 1차 연출하고, **URP Depth of Field**로 실제 심도 흐림을 더하면 훨씬 고급스러운 디지털 더스트 연출이 됩니다.
