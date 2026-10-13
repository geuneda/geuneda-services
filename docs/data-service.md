# 데이터 서비스

[목차로 돌아가기](README.md)

타입별로 데이터를 메모리에 보관하고 `PlayerPrefs` 에 JSON(Newtonsoft.Json)으로 저장/로드합니다. 네임스페이스는 `Geuneda.Services` 입니다.

## 핵심 규칙

- 데이터의 키는 **타입**(`typeof(T)`)입니다. API 에 문자열 키는 없습니다.
- **참조 타입**(`where T : class`)만 지원합니다. 수정한 객체가 그대로 저장되도록 하기 위함입니다.
- 디스크(`PlayerPrefs`) 키는 `typeof(T).FullName` 입니다(`DataService.GetSaveKey(type)`). 네임스페이스나 감싸는 타입이 다르면 이름이 같아도 충돌하지 않습니다.
- 저장 형식은 `{"version":n,"data":{...}}` 입니다. `version` 은 데이터 타입의 `[DataVersion(n)]` 값이며, 없으면 0 입니다.
- `GetData<T>()` 는 메모리에 없으면 `KeyNotFoundException` 을 던집니다. `HasData<T>()` 로 먼저 확인하세요.
- `LoadData<T>()` 는 저장된 값이 없으면 `Activator.CreateInstance<T>()` 로 새로 만듭니다. `T` 에 **매개변수 없는 생성자**가 있어야 합니다. 로드한 인스턴스는 메모리에도 등록됩니다.
- `LoadData<T>()` 는 저장값을 읽지 못해도 예외를 던지지 않습니다. JSON 이 깨졌거나, 봉투 형식이 아니거나(이전 버전의 평문 JSON 포함), 필요한 마이그레이션이 없거나, `data` 를 `T` 로 바꾸지 못하면 원문을 `{키}.corrupt` 에 남기고 에러 로그와 `OnDataLoadFailed` 를 거쳐 새 인스턴스로 시작합니다. 새 인스턴스를 `SaveData` 하기 전까지 원래 세이브는 그대로입니다.
- 저장된 버전이 현재보다 새로우면(더 새 앱이 저장) 경고 후 알 수 있는 필드만 읽습니다. 원본(모르는 필드와 버전)을 잃지 않도록 그 타입은 `SaveData` / `SaveAllData` 에서 저장하지 않고 경고만 남깁니다. 본문을 읽지 못해 새 인스턴스로 시작한 경우에도 마찬가지입니다. 덮어쓰려면 `AddOrReplaceData` 로 데이터를 직접 교체한 뒤 저장하세요.
- 마이그레이션이 필요 없는 로드는 타입으로 바로 읽으므로 `decimal` 정밀도와 날짜 형태 문자열이 그대로 보존됩니다. 마이그레이션 단계가 받는 토큰도 날짜 형태 문자열은 문자열이고, 실수는 원문 리터럴이 `decimal` 범위와 정밀도 안이면 `decimal`, 아니면 `double` 입니다(토큰마다 따로 정함). 마이그레이션 결과는 문자열로 다시 써서 타입으로 읽으므로 `decimal` / `double` 필드 모두 원문 값 그대로 읽힙니다.
- 봉투 뒤에 다른 내용(쓰레기 문자, 이어 붙인 봉투)이 있으면 읽지 못한 세이브로 처리합니다. 공백은 허용합니다.
- `AddOrReplaceData` 는 메모리만 바꿉니다. 디스크에 쓰려면 `SaveData<T>()` / `SaveAllData()` 를 호출하세요.
- 저장할 때마다 `protected virtual void OnDataSaved(string key, object data, Type type)` 가 호출됩니다. 파생 클래스에서 클라우드 동기화 등을 붙일 수 있습니다.
- 저장값을 읽지 못하면 원문을 `protected virtual void SaveCorruptBackup(string key, string rawData)`(기본: `{키}.corrupt` PlayerPrefs)로 보관하고 `protected virtual void OnDataLoadFailed(string key, string rawData, Exception exception)` 를 호출합니다. 사용자 안내나 원문 업로드를 붙일 수 있습니다. 둘 중 하나가 예외를 던져도 로그만 남기고 새 인스턴스로 시작합니다.

## 스키마 버전과 마이그레이션

데이터 구조를 바꿀 때 타입의 버전을 올리고, 이전 버전 세이브를 다음 버전으로 바꾸는 단계를 등록합니다. `LoadData` 는 저장된 버전부터 현재 버전까지 등록된 단계를 차례로 적용한 뒤 `T` 로 읽습니다. 단계는 `data` 부분(`JToken`)을 받아 다음 버전의 `data` 를 반환합니다.

```csharp
using Geuneda.Services;
using Newtonsoft.Json.Linq;

// v0: { "Name": "..." } → v1: { "DisplayName": "..." } → v2: { "DisplayName": "...", "Level": 1 }
[DataVersion(2)]
public class ProfileData
{
    public string DisplayName;
    public int Level;
}

var dataService = new DataService();
dataService.AddMigration<ProfileData>(0, data => new JObject { ["DisplayName"] = data["Name"] });
dataService.AddMigration<ProfileData>(1, data => { data["Level"] = 1; return data; });

var profile = dataService.LoadData<ProfileData>(); // v0, v1 세이브도 v2 로 읽힘
```

- 마이그레이션은 `LoadData` 전에 등록합니다. 같은 버전을 두 번 등록하면 `ArgumentException`, 버전이 음수이거나 현재 버전 이상이면 `ArgumentOutOfRangeException` 입니다.
- 필요한 단계가 빠졌거나 단계가 예외를 던지면 읽지 못한 세이브로 처리합니다(위 규칙).

## 이전 버전과의 관계

키(`Name` → `FullName`)와 저장 형식(평문 JSON → 봉투)이 바뀌어 이전 버전(2.x)이 저장한 세이브는 읽지 않습니다. 기존 프로젝트는 이전 버전에 고정하고, 새 프로젝트부터 이 형식을 씁니다. 이전 형식의 값이 같은 키에 있으면 읽지 못한 세이브로 처리되어 `.corrupt` 에 남습니다.

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
| `LoadData<T>()` | `MissingMethodException` | 새 인스턴스가 필요한데(저장값이 없거나 읽지 못함) `T` 에 매개변수 없는 생성자가 없음 |
| `AddMigration<T>(fromVersion, migration)` | `ArgumentNullException` / `ArgumentOutOfRangeException` / `ArgumentException` | `migration` 이 null / 버전이 음수이거나 현재 이상 / 이미 등록됨 |
