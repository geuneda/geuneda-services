# RNG 서비스

[목차로 돌아가기](README.md)

시드, 스트림, 쓴 출력 수 세 값만으로 상태를 재현하는 결정론적 난수 서비스입니다. core 의 `Pcg32`(PCG32 XSH-RR)를 씁니다. 네임스페이스는 `Geuneda.Services` 입니다.

## 핵심 규칙

- 상태는 `RngData`(`Seed`, `Stream`, `Count`, 모두 `ulong`)에 들어 있습니다. `RngService` 는 넘겨받은 `RngData` 객체의 `Count` 를 **직접 수정**하므로, 이 객체를 저장하면 현재 상태가 그대로 저장됩니다.
- `RngData` 의 필드를 바꾸면(예: 세이브를 같은 객체에 불러오기) 서비스는 다음 호출부터 바뀐 상태를 씁니다.
- `RngService.CreateRngData(seed, stream)` 로 초기 상태를 만듭니다. `stream` 을 빼면 0 입니다.
- `Count` 는 호출 수가 아니라 쓴 32비트 출력 수입니다. 정수 범위는 편향을 없애려고 드물게 출력을 하나 더 쓰고, `NextUInt`·`NextFloat`·floatP 범위는 정확히 하나를 씁니다. 범위 함수는 성공하면 언제나 출력을 하나 이상 씁니다(`Range(7, 7, true)` 도 한 단계 진행).
- `Range(int, int)` 는 기본적으로 max **미포함**, `Range(floatP, floatP)` 는 기본적으로 max **포함**입니다. 세 번째 인자 `maxInclusive` 로 바꿀 수 있고, 포함이면 max 도 나옵니다.
- 정수 범위는 Lemire 방식이라 음수 범위를 포함해 편향이 없습니다.
- floatP 범위는 `min * (1 - t) + max * t` 를 [min, max] 로 자른 값입니다. max 미포함인데 반올림으로 max 가 나오면 max 바로 아래 값을 돌려줍니다.
- float 계열 API 는 `com.geuneda.gamedata` 의 `floatP`(네임스페이스 `Geuneda.DataExtensions`)를 씁니다. `NextFloat()` 은 [0, 1) 의 2^-24 간격 값입니다.
- `PeekRange` 는 상태와 `Count` 를 바꾸지 않습니다. 이어서 같은 인자로 부른 `Range` 가 같은 값을 돌려줍니다.
- `Restore(count)` 는 과거와 미래 어느 쪽이든 O(log n) 으로 옮깁니다.
- 스레드 안전하지 않습니다.

## 사용 예

```csharp
using Geuneda.DataExtensions;
using Geuneda.Services;

RngData rngData = RngService.CreateRngData(seed: 12345);
var rng = new RngService(rngData);

uint raw           = rng.NextUInt();              // 32비트
floatP unit        = rng.NextFloat();             // [0, 1)
int dice           = rng.Range(1, 7);             // 1~6 (max 미포함)
int diceInclusive  = rng.Range(1, 6, true);       // 1~6 (max 포함)
floatP ratio       = rng.Range(0f, 1f);           // 0~1 (max 포함)
bool critical      = rng.Range(0, 100) < 5;       // 5% 판정

// 상태를 바꾸지 않고 미리 보기
int peekedRange = rng.PeekRange(1, 100);

// 저장/복원
ulong saved = rngData.Count;
rng.Range(0, 10);
rng.Restore(saved); // saved 시점으로 되돌림

IRngData state = rng.Data;
```

## 스트림

시드가 같아도 스트림 번호가 다르면 다른 수열입니다(번호를 SplitMix64 로 섞으므로 0, 1, 2 처럼 이웃한 번호도 관련 없음. 스트림 공간이 2^63 이라 임의의 ulong 번호 한 쌍은 겹칠 수 있으니 작은 번호를 매겨 씀). 드랍·전투·가챠처럼 쓰임새마다 스트림을 나누면 한쪽에 뽑기를 추가해도 다른 쪽 수열이 밀리지 않습니다. 서비스 하나가 스트림 하나이므로, 스트림마다 `RngData` 와 `RngService` 를 만들어 보관합니다.

```csharp
var dropRng   = new RngService(RngService.CreateRngData(matchSeed, stream: 1));
var combatRng = new RngService(RngService.CreateRngData(matchSeed, stream: 2));
```

`MainInstaller` 는 인터페이스 하나에 객체 하나를 바인딩하므로, 여러 스트림은 게임 쪽 컨테이너에 두거나 기본 스트림 하나만 `IRngService` 로 바인딩합니다.

## 서버에서 재현

정수 결과(`NextUInt`, `Range(int, int)`)는 core 의 `Runtime/Random/Pcg32.cs` 만 링크하면 서버(.NET)에서 같은 값을 얻습니다. `RngService` 와 같게 하려면 `new Pcg32(Seed, Stream)` 를 만들고 `Advance(Count)` 한 뒤, `Range(min, max)` 는 `NextInt(min, max)`, `Range(min, max, true)` 는 `NextIntInclusive(min, max)` 로 부릅니다.

`floatP` 결과는 floatP 의 곱셈이 IEEE 반올림이 아니라 잘라내기라서 `float`/`double` 로 계산하면 다를 수 있습니다. 서버에서 재현하려면 gamedata 의 `floatP` 도 함께 링크합니다.

## 예외

| 호출 | 예외 | 조건 |
|------|------|------|
| `new RngService(null)` | `ArgumentNullException` | |
| `Range(int min, int max, ...)` / `PeekRange` | `ArgumentOutOfRangeException` | `min > max`, 또는 `maxInclusive == false` 인데 `min == max`. 상태는 바뀌지 않음 |
| `Range(floatP min, floatP max, ...)` / `PeekRange` | `ArgumentOutOfRangeException` | 위 조건, 또는 경계가 NaN·무한대. 상태는 바뀌지 않음 |

## 주의사항

- v4.0.0 에서 Knuth 감산 생성기를 PCG32 로 바꿨습니다. 같은 시드라도 이전 버전과 수열이 다르고, `RngData` 의 형식도 다릅니다. 이전 버전에서 저장한 상태나 리플레이 데이터는 호환되지 않습니다. 자세한 내용은 [MIGRATION.md](../MIGRATION.md) 를 참고하세요.
