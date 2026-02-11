# VFX Graph 3D 은하수 배경 구현 가이드

유니티 **VFX Graph**로 Torus 형태의 은하수(수만 개 별)를 만드는 노드 구성 단계입니다. GPU 파티클 기반으로 성능을 유지합니다.

---

## 1. 초기 설정

- **Project** → **Create** → **Visual Effects** → **Visual Effect Graph** 생성.
- 그래프 더블클릭 후 **Initialize** / **Update** / **Output** 컨텍스트 확인.
- **Inspector**에서 **Exposed** 속성은 필요 시 사용(반지름, 회전 속도 등).

---

## 2. Set Position (Shape: Torus) + 중심으로 밀도 증가

### 2-1. Spawn 컨텍스트

- **Spawn** 블록에서 **Constant Spawn Rate** 또는 **Burst** 사용.
  - 예: **Burst** → **Count** 50000~100000 (GPU 부하에 따라 조절).
- **Initialize** 컨텍스트에서:
  - **Set Position (Shape: Torus)** 추가.
  - **Torus** 입력:
    - **Major Radius**: 은하 반지름 (예: 50~200).
    - **Minor Radius**: 은하 두께 (예: 5~30).
  - **Arc** / **Thickness**로 도넛 일부만 쓰면 은하 팔 형태에 가깝게 조정 가능.

### 2-2. 중심부로 밀도 높이기

- **Set Position (Shape: Torus)**만 쓰면 균일 분포입니다.
- **Sample Curve** 또는 **Custom**으로 중심에 가까울수록 파티클이 더 나오게 하려면:
  - **Position (Shape: Torus)** 출력의 **position**을 **Sample Curve**에 연결.
  - Curve는 **X 또는 거리**가 0(중심)일 때 값이 크고, 바깥일수록 작게 설정.
- 또는 **Set Position (Shape: Sphere)**를 **Blend**로 Torus와 섞어, 중심 근처에 추가 스폰:
  - **Lerp**로 Torus position과 Sphere position을 **Random (0~1)**로 블렌드해, 일부 파티클이 중심 쪽에 더 모이게 합니다.

---

## 3. Update에서 중심축 기준 회전

### 3-1. Rotation (각속도)

- **Update** 컨텍스트에 **Rotate (Euler)** 또는 **Rotate (Angle Axis)** 추가.
- **Rotate (Angle Axis)**:
  - **Axis**: (0, 1, 0) 등 은하 회전축 (Y up 기준).
  - **Angle**: `deltaTime * RotationSpeed` 형태.
  - **Rotation Speed**는 **Exposed float**로 두고 0.1~1 정도로 조절.
- **Particle position**을 회전시키려면:
  - **Get Attribute: position** → **Rotate (Angle Axis)** → **Set Attribute: position**.

### 3-2. Vector Field Force (선택)

- **Add Position (Vector Field)**를 쓰면 흐름장으로 회전/와류 느낌을 줄 수 있습니다.
- **Vector Field** 에셋은 **Create** → **Visual Effects** → **Vector Field**로 만들고, 회전하는 벡터장으로 베이크.
- **Update**에서 **Add Position**에 연결해, Torus 축을 감싸 도는 힘을 줍니다.

---

## 4. 반짝임: Size over Lifetime / Color over Lifetime

### 4-1. Size over Lifetime

- **Update** 또는 **Output** 전 **Set Attribute**에서 **size** 제어.
- **Size over Lifetime**:
  - **Curve**: 0~1 구간에서 크기가 작았다 크았다 하도록 (뾰족한 곡선).
  - **Lifetime**은 **Get Attribute: age** / **lifetime**으로 정규화 (0~1).
- **Sample Curve**로 **age/lifetime** → **size multiplier** (0.8~1.2 등) 연결.

### 4-2. Color over Lifetime

- **Set Attribute: color**에서 **Color over Lifetime**.
- **Gradient**: 흰색/연한 노랑 중심에, 약간의 파란/보라 톤을 섞어 별 느낌.
- **Alpha**는 0~1~0처럼 살짝 파동치게 해 반짝임 강조.

### 4-3. 미세 반짝임 (선택)

- **Update**에서 **Sine** 노드: `time * frequency`로 **color alpha** 또는 **size**를 곱해 주면, 시간에 따라 미세하게 깜빡입니다.

---

## 5. GPU 파티클 / 성능

- **Capacity**: 한 번에 나올 수 있는 최대 파티클 수 (예: 100000). 적당히 제한.
- **Output**에서 **Output Particle** 사용 (GPU Instancing).
- **Culling**: **Camera Culling** 등으로 화면 밖은 그리지 않도록 설정.
- **LOD**가 있다면 거리에 따라 파티클 수/크기 줄이기.

---

## 요약 노드 체인

1. **Spawn** → Count/Burst 설정.
2. **Initialize** → **Set Position (Shape: Torus)** (+ 필요 시 Blend로 중심 밀도).
3. **Update** → **Rotate (Angle Axis)**로 position 회전 (+ 선택: Vector Field).
4. **Update** → **Set size** / **Set color** with **Sample Curve** (age/lifetime, 반짝임 곡선).
5. **Output** → **Output Particle** (GPU).

이 순서로 구성하면 Torus 은하수 + 회전 + 반짝임 + GPU 기반 배경을 만들 수 있습니다.
