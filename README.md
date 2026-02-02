# ZeroStep

모바일 퍼즐 게임 **ZeroStep** — 손가락으로 타일을 밟아 모든 숫자를 0으로 만드세요.

---

## 게임 소개 (한국어)

**ZeroStep**은 2×2부터 시작해 스테이지가 올라갈수록 그리드가 커지는 터치 기반 퍼즐 게임입니다.  
스테이지 1부터 500까지 있으며, 광고가 포함되어 있습니다.

### 규칙

- **그리드**: 각 타일에는 숫자가 적혀 있고, 한 칸이 **시작 타일**(빨간색)입니다.
- **이동**: **오른쪽** 또는 **아래**로만 이동할 수 있습니다. 대각선 이동은 불가능합니다.
- **밟을 수 있는 타일**: 현재 서 있는 타일의 숫자와 **같은 숫자**가 적힌 타일만 밟을 수 있습니다.
- **숫자 변화**: 타일을 **떠날 때** 그 타일의 숫자가 1 감소하고, **밟을 때** 그 타일의 숫자도 1 감소합니다.
- **클리어**: 모든 타일의 숫자가 0이 되면 스테이지 클리어입니다.

### 예시 (2×2)

```
[시작: 2] [빨간: 2]
[빨간: 2] [초록: 1]
```

**클리어 경로**:  
시작(2) → 오른쪽 빨간(2) → 아래 빨간(2) → 오른쪽 초록(1)  
각 단계에서 떠나는 타일과 밟는 타일이 1씩 줄어들어, 마지막에 모든 타일이 0이 되면 클리어입니다.

---

## Game Overview (English)

**ZeroStep** is a touch-based puzzle game where the grid grows as you advance (starting from 2×2).  
It features 500 stages (1–500) and includes ads.

### Rules

- **Grid**: Each tile has a number. One tile is the **start tile** (red).
- **Movement**: You may only move **right** or **down**. No diagonal moves.
- **Steppable tiles**: You can only step onto a tile whose number **matches** the number on your current tile.
- **Number change**: When you **leave** a tile, its number decreases by 1. When you **step on** a tile, that tile’s number also decreases by 1.
- **Clear**: The stage is cleared when **every** tile’s number becomes 0.

### Example (2×2)

```
[Start: 2] [Red: 2]
[Red: 2]   [Green: 1]
```

**Clear path**:  
Start(2) → right to Red(2) → down to Red(2) → right to Green(1).  
At each step, the tile you leave and the tile you step on both decrease by 1 until all tiles reach 0.

---

## 기술 스택 / Tech

- **Unity** (모바일 타겟 / Mobile)
- 터치 입력 / Touch input
- 스테이지 1–500, 그리드 크기 증가 / Stages 1–500, increasing grid size
