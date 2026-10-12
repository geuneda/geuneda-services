# 데이터 서비스

[목차로 돌아가기](README.md)

타입별로 데이터를 메모리에 보관하고 `PlayerPrefs` 에 JSON(Newtonsoft.Json)으로 저장/로드합니다. 네임스페이스는 `Geuneda.Services` 입니다.

## 핵심 규칙

- 데이터의 키는 **타입**(`typeof(T)`)입니다. API 에 문자열 키는 없습니다.
- **참조 타입**(`where T : class`)만 지원합니다. 수정한 객체가 그대로 저장되도록 하기 위함입니다.
- 디스크(`PlayerPrefs`) 키는 `typeof(T).Name` 입니다. 서로 다른 네임스페이스/어셈블리에 같은 이름의 타입이 있으면 충돌합니다.
- `GetData<T>()` 는 메모리에 없으면 `KeyNotFoundException` 을 던집니다. `HasData<T>()` 로 먼저 확인하세요.
- `LoadData<T>()` 는 저장된 값이 없으면 `Activator.CreateInstance<T>()` 로 새로 만듭니다. `T` 에 **매개변수 없는 생성자**가 있어야 합니다. 로드한 인스턴스는 메모리에도 등록됩니다.
- `AddOrReplaceData` 는 메모리만 바꿉니다. 디스크에 쓰려면 `SaveData<T>()` / `SaveAllData()` 를 호출하세요.
- 저장할 때마다 `protected virtual void OnDataSaved(string key, object data, Type type)` 가 호출됩니다. 파생 클래스에서 클라우드 동기화 등을 붙일 수 있습니다.

## 사용 예

```csharp
using System;
using Geuneda.Services;

[Serializable]
public class PlayerData
{
    public string Name;
    public int Level;
}

var dataService = new DataService();

// 디스크에서 로드 (없으면 새로 생성) 후 메모리에 등록
PlayerData player = dataService.LoadData<PlayerData>();

// 메모리의 객체를 수정
player.Name = "Hero";
player.Level = 10;

// 한 타입만 저장 / 메모리의 모든 타입 저장
dataService.SaveData<PlayerData>();
dataService.SaveAllData();

// 메모리에만 추가하거나 교체
dataService.AddOrReplaceData(new PlayerData { Name = "Alt", Level = 5 });

// 메모리에서 읽기
if (dataService.HasData<PlayerData>())
{
    PlayerData loaded = dataService.GetData<PlayerData>();
}
```

## IDataProvider 와 IDataService

| 인터페이스 | 메서드 | 용도 |
|------------|--------|------|
| `IDataProvider` | `GetData<T>()`, `HasData<T>()` | 읽기 전용 소비자 |
| `IDataService : IDataProvider` | `AddOrReplaceData<T>`, `LoadData<T>`, `SaveData<T>`, `SaveAllData` 추가 | 읽기/쓰기 |

저장이 필요한 시스템에는 `IDataService` 를, 읽기만 하는 시스템에는 `IDataProvider` 를 넘기세요.

## 예외

| 호출 | 예외 | 조건 |
|------|------|------|
| `GetData<T>()`, `SaveData<T>()` | `KeyNotFoundException` | `T` 가 메모리에 없음 |
| `LoadData<T>()` | `MissingMethodException` | 저장값이 없고 `T` 에 매개변수 없는 생성자가 없음 |
