# 버전 서비스

[목차로 돌아가기](README.md)

빌드 버전과 git 메타데이터(브랜치, 커밋, 빌드 번호), 빌드 환경을 런타임에 읽는 정적 클래스 `VersionServices` 입니다. 네임스페이스는 `Geuneda.Services` 입니다.

## 동작 방식

1. **에디터**: `VersionEditorUtils`(`Geuneda.Services.Versioning.Editor`)가 도메인 리로드마다(`[InitializeOnLoadMethod]`) git CLI 로 브랜치와 커밋을 읽어 `version-data.txt` 를 씁니다. 내용이 바뀌지 않았으면 쓰지 않습니다.
   - 쓰는 위치: 프로젝트에 이미 `Resources/` 아래 `version-data.txt` 가 있으면 그 폴더, 없으면 `VersioningEditorSettings` 의 폴더(기본 `Assets/Configs/Resources`). Services Explorer 의 **Versioning** 탭에서 바꿀 수 있으며, 경로에 `Resources` 폴더가 포함되어야 합니다.
   - 빌드 번호는 빌드 타깃 기준입니다. Android 는 `PlayerSettings.Android.bundleVersionCode`, 그 밖에는 `PlayerSettings.iOS.buildNumber` 입니다. 에디터에서 쓸 때는 활성 빌드 타깃을 씁니다.
   - 플레이어를 빌드하면 `VersionBuildProcessor`(`IPreprocessBuildWithReport`)가 빌드 직전에 빌드 대상 플랫폼의 빌드 번호로 다시 씁니다. 빌드가 성공하면 후처리(`IPostprocessBuildWithReport`)에서, 실패하거나 취소되어 후처리가 불리지 않으면 빌드가 끝난 뒤 첫 에디터 업데이트(`BuildPipeline.isBuildingPlayer` 가 거짓일 때)에서 활성 빌드 타깃 값으로 되돌립니다. 배치 모드에서 빌드 후 바로 종료하면 되돌리지 않으며, 다음 에디터 실행의 도메인 리로드에서 다시 씁니다.
   - 빌드 스크립트에서 직접 정하려면 `VersionEditorUtils.SetAndSaveInternalVersion(buildTarget)` 을 호출하세요. 그 뒤 `BuildPipeline.BuildPlayer` 를 부르면 빌드 직전 훅이 다시 씁니다.
   - 환경(dev/qa/prod)은 파일에 쓰지 않습니다. 플레이어를 컴파일할 때의 환경 define 으로 정해지는 `Geuneda.Core.AppEnv` 를 런타임에 읽습니다. define 이 없으면 에디터·개발 빌드는 `dev`, 릴리스 빌드는 `prod` 이고, `GEUNEDA_ENV_QA` 등으로 바꿉니다(core README "빌드 환경").
   - 파일 내용에 커밋 해시가 들어가 커밋마다 바뀌므로, 사용하는 프로젝트에서는 `version-data.txt`(와 `.meta`)를 gitignore 하는 것을 권장합니다. 파일이 없으면 에러 로그 없이 새로 만듭니다(v2.1.2).
2. **런타임**: `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` 훅이 `LoadVersionData()` 를 호출해 `Resources.Load<TextAsset>("version-data")` 를 읽습니다. 씬의 `Awake` 보다 먼저 실행됩니다.
3. **지연 로드 폴백**: 다른 어셈블리의 `SubsystemRegistration` 콜백이 먼저 실행되는 경우를 위해, `VersionInternal` / `Branch` / `Commit` / `BuildNumber` 는 로드를 한 번도 시도하지 않았으면 접근 시 `LoadVersionData()` 를 호출합니다. 시도가 실패해도 다시 시도하지 않습니다(v2.1.4).

기본 흐름에서는 로드 메서드를 직접 호출할 필요가 없습니다.

## 사용 예

```csharp
using Geuneda.Services;

string external = VersionServices.VersionExternal;  // Application.version, 예: "1.0.1"
string internalVersion = VersionServices.VersionInternal; // 예: "1.0.1-42.main.abc12345.prod" (끝은 AppEnv.Name)
string branch = VersionServices.Branch;              // 예: "main"
string commit = VersionServices.Commit;              // 커밋 해시 앞 8자리
string build  = VersionServices.BuildNumber;         // 예: "42"

// 원격 버전이 더 높으면 true
bool outdated = VersionServices.IsOutdatedVersion("1.2.0");
// 두 버전을 직접 비교
bool newer = VersionServices.IsOutdatedVersion("1.2", "1.2.1-rc"); // true
```

## API

| 멤버 | 설명 |
|------|------|
| `VersionDataFilename` | `const string`, 값 `"version-data"` (변경 불가) |
| `VersionExternal` | `Application.version`. 로드 없이 항상 안전 |
| `VersionInternal` | `{Application.version}-{BuildNumber}.{BranchName}.{CommitHash}.{AppEnv.Name}` (`FormatInternalVersion` 결과) |
| `Branch` / `Commit` / `BuildNumber` | `version-data` 의 값 |
| `LoadVersionData()` | 동기 로드. 호출할 때마다 다시 읽음 |
| `LoadVersionDataAsync()` | `Task` 를 반환하는 비동기 로드. `VersionData` 를 크게 확장한 경우에만 의미가 있음 |
| `IsOutdatedVersion(string version)` | `version` 이 `VersionExternal` 보다 높으면 `true` |
| `IsOutdatedVersion(string currentVersion, string otherVersion)` | `otherVersion` 이 `currentVersion` 보다 높으면 `true`. `Major[.Minor[.Patch]]` 를 받고 빠진 부분은 0, 각 부분은 앞쪽 숫자만 비교(`1.2.3-rc` 와 `1.2.3` 은 같음). Major 를 읽을 수 없으면 경고 로그 후 `false` |
| `FormatInternalVersion(VersionData data)` | 내부 버전 문자열 생성 |
| `VersionServices.VersionData` | 중첩 `[Serializable] struct`: `CommitHash`, `BranchName`, `BuildNumber`. 예전 파일의 `BuildType` 값은 읽을 때 무시됩니다 |

## 리소스가 없거나 파싱에 실패할 때

| 멤버 | 동작 |
|------|------|
| `VersionInternal` | `Application.version` 반환 |
| `Branch`, `Commit`, `BuildNumber` | `string.Empty` 반환 |

예외는 던지지 않고 `Geuneda.Services.Version` 로거로 Error 를 남깁니다. 프로퍼티 접근에 따른 지연 로드는 한 번만 시도하므로 에러 로그도 한 번만 남습니다(v2.1.4 이전에는 접근할 때마다 다시 시도해 로그가 반복됐습니다). 나중에 리소스가 생겼다면 `LoadVersionData()` 를 직접 호출해야 반영됩니다.

- `Branch` / `Commit` 은 프로젝트가 git 저장소일 때만 채워집니다. git 저장소가 아니면 `version-data.txt` 는 생성되지만 두 값이 비어 있어 `string.Empty` 를 반환합니다.

## 주의사항

- `IsOutdatedVersion` 은 `Major[.Minor[.Patch]]` 를 받고 빠진 부분은 0 으로 봅니다(새 프로젝트 기본값 `0.1` 도 비교 가능). 각 부분은 앞쪽 숫자만 비교하므로 접미사(`-rc`, `+42`)는 무시되고 `1.2.3-rc` 와 `1.2.3` 은 같습니다. Major 를 읽을 수 없거나(`v1.2.3`, 빈 문자열) 어느 부분이 `int` 범위를 넘으면 예외 없이 경고 로그를 남기고 `false` 를 반환합니다(Unreleased, 이전에는 `IndexOutOfRangeException`/`FormatException`).
