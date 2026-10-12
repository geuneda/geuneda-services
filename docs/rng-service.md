# RNG 서비스

[목차로 돌아가기](README.md)

시드와 호출 횟수만으로 상태를 재현할 수 있는 결정론적 난수 생성기입니다. .NET `System.Random` 과 같은 Knuth 감산 생성기 알고리즘을 씁니다. 네임스페이스는 `Geuneda.Services` 입니다.

## 핵심 규칙

- 상태는 `RngData`(`Seed`, `Count`, `int[] State`)에 들어 있습니다. `RngService` 는 넘겨받은 `RngData` 객체를 **직접 수정**하므로, 이 객체를 저장하면 현재 상태가 그대로 저장됩니다.
- `RngService.CreateRngData(seed)` 로 초기 상태를 만듭니다.
- float 계열 API 는 `com.geuneda.gamedata` 의 `floatP`(네임스페이스 `Geuneda.DataExtensions`)를 씁니다.
- `Range(int, int)` 는 기본적으로 max **미포함**, `Range(floatP, floatP)` 는 기본적으로 max **포함**입니다. 세 번째 인자 `maxInclusive` 로 바꿀 수 있습니다.
- `Peek`, `Peekfloat`, `PeekRange` 는 상태와 `Counter` 를 바꾸지 않습니다. `Next`, `Nextfloat`, `Range` 는 상태를 진행하고 `Counter` 를 1 올립니다.
- `Restore(count)` 는 시드에서 상태를 다시 만든 뒤 `count` 번 진행합니다. 과거와 미래 어느 쪽으로도 갈 수 있지만 비용은 `count` 에 비례합니다.

## 사용 예

```csharp
using Geuneda.DataExtensions;
using Geuneda.Services;

RngData rngData = RngService.CreateRngData(seed: 12345);
var rng = new RngService(rngData);

int value          = rng.Next;                    // 음이 아닌 int
floatP valueFloat  = rng.Nextfloat;
int dice           = rng.Range(1, 7);             // 1~6 (max 미포함)
floatP ratio       = rng.Range(0f, 1f);           // 0~1 (max 포함)
bool critical      = rng.Range(0, 100) < 5;       // 5% 판정

// 상태를 바꾸지 않고 미리 보기
int peeked      = rng.Peek;
int peekedRange = rng.PeekRange(1, 100);

// 저장/복원
int saved = rng.Counter;
rng.Range(0, 10);
rng.Restore(saved); // saved 시점으로 되돌림

IRngData state = rng.Data;
```

## 정적 헬퍼

`RngService` 는 상태 배열을 직접 다루는 정적 메서드도 제공합니다.

| 메서드 | 설명 |
|--------|------|
| `CreateRngData(int seed)` | 새 `RngData` 생성 (`Count = 0`) |
| `GenerateRngState(int seed)` | 시드로 초기 상태 배열(길이 56) 생성 |
| `CopyRngState(int[] state)` | 상태 배열 복사. 길이가 56이 아니면 `IndexOutOfRangeException` |
| `Restore(int count, int seed)` | 시드에서 `count` 번 진행한 상태 배열 반환 |
| `Range(int, int, int[] state, bool maxInclusive)` / `Range(floatP, floatP, int[] state, bool maxInclusive)` | 주어진 상태 배열을 진행시키며 범위 난수 반환 |

## 주의사항

- v2.1.1 에서 생성기 분포 편향 버그가 수정되어 같은 시드라도 이전 버전과 시퀀스가 다릅니다. 이전 버전에서 저장한 리플레이/재현 데이터는 호환되지 않습니다. 자세한 내용은 [CHANGELOG](../CHANGELOG.md) 를 참고하세요.

## 예외

| 호출 | 예외 | 조건 |
|------|------|------|
| `Range(min, max)` / `PeekRange(min, max)` | `IndexOutOfRangeException` | `min > max`, 또는 `maxInclusive == false` 인데 `min == max` |
