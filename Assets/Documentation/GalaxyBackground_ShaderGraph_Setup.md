# Shader Graph 우주 성운(Nebula) 스카이박스 가이드 (URP)

유니티 **Shader Graph**(URP)로 움직이는 성운 스카이박스와 별 반짝임을 만드는 노드 연결 방법입니다.

---

## 1. 프로젝트 설정

- **Create** → **Shader Graph** → **URP** → **Unlit** (또는 **Lit** 필요 시) 선택.
- **Graph** 이름 예: `NebulaSkybox`.
- **Graph Settings**에서 **Surface Type**: Transparent, **Blend**: Alpha.

---

## 2. Voronoi + Gradient Noise로 성운 질감

### 2-1. UV

- **UV** 노드 (Object 또는 World 기반 선택).
- **Tiling and Offset**에 연결해 나중에 **Time**과 조합.

### 2-2. Voronoi Noise

- **Voronoi** 추가.
- **UV** → Voronoi **UV** 입력.
- **Scale**: 2~10 (성운 덩어리 크기).
- **Angle Offset**: **Time**을 곱해 넣으면 형태가 천천히 변하게 할 수 있음 (선택).

### 2-3. Gradient Noise

- **Gradient Noise** 추가.
- 같은 **UV**(또는 Tiling 적용된 UV) 연결.
- **Scale**: 5~15.

### 2-4. 섞기

- **Lerp** 또는 **Multiply / Add**로 Voronoi 출력과 Gradient Noise 출력을 섞습니다.
- 예: **Lerp(A, B, 0.5)** → 가스 형태의 부드러운 패턴.
- 이 결과를 **Base Color**나 **Alpha**에 연결해 성운 가시성으로 사용.

---

## 3. Time + Tiling and Offset로 흐르는 애니메이션

- **Time** 노드 → **Multiply** (속도 계수) → **Tiling and Offset**의 **Offset** 입력에 연결.
- **Tiling and Offset**의 **UV** 입력에는 **UV** 노드.
- **Tiling**은 (1,1) 또는 (2,2) 등으로 조절.
- 이 **Tiling and Offset** 출력을 Voronoi / Gradient Noise의 **UV**로 사용하면, 배경이 서서히 흐르는 듯 움직입니다.

---

## 4. 별 반짝임: Simple Noise + Step

- **Simple Noise** 추가.
- **Scale**을 **매우 크게** (50~200): 작은 점들이 많이 생기도록.
- **Time**을 **Offset**이나 **UV**에 더해 반짝임처럼 보이게 할 수 있음.
- **Step** 노드:
  - **In**: Simple Noise 출력.
  - **Edge**: 0.9~0.98 (높을수록 별 개수 적고 밝은 점만 남음).
- **Step** 출력은 0 또는 1 → **Multiply**로 **Base Color**나 **Emission**에 곱해, 작은 흰/노랑 점으로 별을 표현.

---

## 5. Deep Blue + Purple 시네마틱 톤

- **Lerp** 또는 **Gradient**로 두 색 혼합:
  - **Color 1**: Deep Blue (0.1, 0.05, 0.3).
  - **Color 2**: Purple (0.3, 0.1, 0.4).
- 위에서 만든 **성운 패턴**(Voronoi+Gradient 혼합)을 **Lerp**의 **T**로 사용하면, 패턴에 따라 Blue/Purple이 섞입니다.
- **Emission**에 같은 색을 넣고 강도를 올리면 시네마틱한 발광 느낌.
- **별(Step)** 출력을 **(1,1,1)** 또는 연한 노랑과 **Multiply**한 뒤, **Base Color** 또는 **Emission**에 **Add**로 더해 줍니다.

---

## 6. 최종 연결 요약

1. **UV** → **Tiling and Offset** (Offset에 **Time * speed**).
2. **Tiling and Offset** → **Voronoi** / **Gradient Noise**의 UV.
3. **Voronoi** ↔ **Gradient Noise** → **Lerp** → 성운 색(Blue/Purple)과 혼합.
4. **Simple Noise** (큰 Scale) → **Step(0.95)** → 별 마스크.
5. **별 마스크 * 흰색** + **성운 색** → **Base Color** / **Emission**.
6. **Alpha**는 성운 패턴 또는 고정 1 (스카이박스용).

이렇게 구성하면 움직이는 성운 + 반짝이는 별 + Deep Blue/Purple 톤의 스카이박스 쉐이더를 만들 수 있습니다.
