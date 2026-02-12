# 변경 이력
이 패키지의 모든 주요 변경 사항은 이 파일에 기록됩니다.

이 형식은 [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)를 기반으로 하며,
이 프로젝트는 [유의적 버전](http://semver.org/spec/v2.0.0.html)을 따릅니다.

## [1.0.1] - 2026-01-14

**변경**:
- 의존성 `com.geuneda.dataextensions`를 `com.geuneda.gamedata`로 변경
- 어셈블리 정의에서 `Geuneda.GameData` 참조로 업데이트
- `RngService`에서 결정론적 연산을 위해 `MathfloatP` 사용

## [1.0.0] - 2026-01-11

**신규**:
- AI 코딩 어시스턴트가 이 패키지 라이브러리를 이해하고 작업할 수 있도록 *AGENTS.md* 문서 추가
- 이 패키지의 모든 서비스에 대한 전체 테스트 슈트(단위/통합/성능/스모크 테스트) 추가

**변경**:
- 패키지의 다른 서비스와 일관성을 유지하기 위해 *VersionServices* 네임스페이스를 *GameLovers*에서 *GameLovers.Services*로 변경
- 파생 풀 클래스에서 생명주기 콜백 동작을 커스터마이즈할 수 있도록 *ObjectPoolBase\<T\>*의 *CallOnSpawned*, *CallOnSpawned\<TData\>*, *CallOnDespawned* 메서드를 virtual로 변경

**수정**:
- OSS 표준 모범 사례에 따르도록 *README.md* 파일 수정
- *VersionServices.cs*의 린터 경고 수정 (불필요한 필드 초기화, 미사용 람다 매개변수, 멤버 섀도잉)
- 스폰된 GameObject에 부착된 컴포넌트에서 *IPoolEntitySpawn.OnSpawn()*과 *IPoolEntityDespawn.OnDespawn()*이 호출되지 않던 *GameObjectPool* 문제 수정

## [0.15.1] - 2025-09-24

**신규**:
- 게임별 프로젝트 상속에서 접근할 수 있도록 *CommandService*의 모든 private 필드에 protected 프로퍼티 추가

## [0.15.0] - 2025-01-05

**신규**:
- Unity 코루틴 범위 내에서 지연된 메서드를 안전하게 실행할 수 있도록 *ICoroutineService*에 *StartDelayCall* 메서드 추가
- *IAsyncCoroutine*의 현재 상태를 알 수 있는 기능 추가
- *IObjectPool<T>* 내에서 새 엔티티를 생성하는 데 사용되는 Sample Entity에 접근하고, 오브젝트 풀 해제 시 파괴하는 기능 추가
- *IObjectPool<T>*를 새로운 상태로 리셋할 수 있는 기능 추가

## [0.14.1] - 2024-11-30

**수정**:
- maxInclusive가 true로 설정되었을 때 동일한 min과 max 값을 허용하도록 *RngLogic.Range(float, float, bool)* 메서드 수정

## [0.14.0] - 2024-11-15

**신규**:
- 메시지 발행 중 연쇄 구독이 발생하는 경우 안전하게 메시지를 발행할 수 있도록 *IMessageBrokerService*에 *PublishSafe* 메서드 추가

**변경**:
- 메시지 발행 중 *Subscribe*와 *Unsubscribe* 실행 시 *InvalidOperationException* 발생하도록 변경

**수정**:
- 릴리스 프로젝트 빌드 시 CoroutineTests 실행 문제 수정

## [0.13.1] - 2024-11-04

**수정**:
- 여러 인터페이스를 동시에 바인딩하려 할 때 발생하는 *IInstaller* 문제 수정

## [0.13.0] - 2024-11-04

**변경**:
- 커맨드에서 *MessageBrokerService*를 수신하도록 *CommandService* 변경하여 게임 아키텍처와의 통신을 지원

## [0.12.2] - 2024-11-02

**수정**:
- 엔티티 스폰 시 *IPoolEntityObject<T>.Init()*이 호출되지 않던 문제 수정

## [0.12.1] - 2024-10-25

**수정**:
- *RngService.Range()* 호출 시 발생하던 무한 루프 수정
- 새 엔티티 스폰 시 *GameObjectPool*에서 발생하던 무한 루프 수정

## [0.12.0] - 2024-10-22

**신규**:
- 읽기 전용 데이터 구조를 지원하고 다른 오브젝트에 데이터를 추상적으로 주입할 수 있도록 *PoolService*에 *IRngData* 추가

**변경**:
- *IRngData* 주입 시 박싱/언박싱 성능 문제를 방지하기 위해 *RngData*를 클래스로 변경

## [0.11.0] - 2024-10-19

**신규**:
- 정의된 스폰 데이터로 새 오브젝트를 스폰할 수 있도록 *PoolService*에 *Spawn<T>(T data)* 메서드 추가
- 풀 서비스에서 관리하는 풀 오브젝트를 요청할 수 있도록 *PoolService*에 *GetPool<T>()* 및 *TryGetPool<T>()* 메서드 추가

**변경**:
- 기본 기능이 아니며 이제 *GetPool()*에서 요청한 풀에서 접근할 수 있으므로 *PoolService*에서 *IsSpawned<T>()* 메서드 제거
- *Spawn<T>(T data)*가 데이터 없이도 *OnSpawn()*을 호출하여 *IPoolEntitySpawn*을 구현하는 오브젝트가 전체 동작 생명주기를 가지도록 변경

## [0.10.0] - 2024-10-11

**신규**:
- 참조 타입 커맨드를 위해 struct가 아닌 타입의 커맨드도 실행할 수 있도록 *CommandService* 업데이트
- 정의된 스폰 데이터로 새 오브젝트를 스폰할 수 있도록 풀 오브젝트에 *Spawn<T>(T data)* 메서드 추가

## [0.9.0] - 2024-08-10

**신규**:
- 데이터 서비스 관련 인터페이스와 클래스를 업데이트하여 모듈성을 향상하고 버전 처리를 개선
- Git 커맨드, 버전 관리, 난수 생성을 위한 클래스 추가

**변경**:
- 데이터 서비스 인터페이스를 재구조화하여 기능을 단일 *IDataService* 인터페이스로 통합하고 불필요한 인터페이스 제거
- *DataService* 구현에서 *AddData*를 *AddOrReplaceData*로 변경
- 데이터 처리에서 *isLocal* 상태 제거

## [0.8.1] - 2023-08-27

**신규**:
- Git 명령을 프로세스로 실행하는 GitEditorProcess 클래스 추가하여 유효한 Git 저장소 확인, 현재 브랜치 이름 조회, 커밋 해시 조회, 지정된 커밋에서의 diff 가져오기 기능 지원
- 애플리케이션 버전 관리를 위한 *VersionEditorUtils* 클래스 도입. 빌드 전 내부 버전 설정 및 저장, 디스크에서 버전 데이터 로드, Git 정보와 빌드 설정을 기반으로 한 내부 버전 접미사 생성 포함

**변경**:
- 모듈성과 코드 구성을 개선하기 위해 단일 인스턴스에 여러 타입 인터페이스를 바인딩하는 새 메서드로 *IInstaller* 인터페이스 향상

## [0.8.0] - 2023-08-05

**신규**:
- 프로젝트의 인스턴스 관리를 위한 싱글톤 클래스 *MainInstaller* 도입
- 난수 생성 및 관리를 위한 *RngService* 추가
- 비동기 버전 데이터 로드 및 버전 문자열 비교를 포함한 애플리케이션 버전 관리를 위한 VersionServices 구현

## [0.7.1] - 2023-07-28

**변경**:
- 테스트를 적절한 폴더로 이동하고 패키지 번호 업데이트
- InstallerTest 클래스에서 미사용 네임스페이스 import 제거

**수정**:
- 여러 테스트 파일과 PoolService 클래스의 컴파일 오류 수정

## [0.7.0] - 2023-07-28

**신규**:
- GitHub Actions 워크플로우를 사용한 코드 리뷰 프로세스 도입
- *IInstaller* 인터페이스와 인스턴스 바인딩 및 해결을 위한 Installer 구현 추가
- 네임스페이스 업데이트, 미사용 코드 제거, 테스트 클래스의 메서드 호출 수정

**변경**:
- *CommandService*에서 *ICommandNetworkService* 의존성과 SendCommand 메서드 제거
- 로컬 및 온라인 데이터 저장을 처리하도록 *IDataService* 인터페이스와 *DataService* 클래스 업데이트
- var를 사용한 타입 추론으로 *MessageBrokerService* 클래스 가독성 향상
- 미사용 네트워크 서비스 관련 인터페이스, 클래스 및 메서드 제거
- TickService에서 DeltaTime이 0인지 확인하도록 overFlow 계산 수정

## [0.6.2] - 2020-09-10

**변경**:
- 작업을 더 쉽게 하기 위해 *NetworkService*를 abstract로 변경하고 *INetworkService* 제거
- Readme 문서 개선

## [0.6.1] - 2020-09-09

**신규**:
- *NetworkService*와 *CommandService* 간 연결 추가
- 통합 테스트 추가

## [0.6.0] - 2020-09-09

**신규**:
- *NetworkService* 추가
- Readme 문서 개선

## [0.5.0] - 2020-07-10

**변경**:
- 실행 로직 범위에 맞게 *IDataWriter*와 *FlushData* 메서드를 각각 *IDataSaver*와 *SaveData*로 이름 변경
- *IDataSaver*가 디스크에 데이터를 저장하는 단일 책임을 가지도록 *AddData*를 *IDataService*로 이동

## [0.4.1] - 2020-07-09

**신규**:
- *CommandService* 추가

## [0.4.0] - 2020-07-09

**신규**:
- *DataService* 추가

## [0.3.1] - 2020-02-25

**수정**:
- 오브젝트 풀의 전체 요소 디스폰 수정. 모든 요소를 디스폰하지 못하던 문제 해결
- 코루틴 중지를 방해하고 MissingReferenceException을 발생시키던 문제 수정

## [0.3.0] - 2020-02-09

**변경**:
- *MainInstaller*가 컴파일 타임에 오브젝트 바인딩 관계를 확인하도록 변경
- 게임 오브젝트를 위한 새로운 정적 전역 인스턴시에이터로 *ObjectPools* 헬퍼 클래스 개선
- *PoolService*를 오브젝트 풀의 서비스 컨테이너로만 사용하도록 변경하고 더 이상 새 풀을 생성/초기화하지 않음
- *Pool.Clear* 기능 제거. 대신 *DespawnAll*을 사용하거나 풀을 삭제하세요

**수정**:
- null 코루틴에서 더 이상 실패하지 않도록 *CoroutineService* 수정

## [0.2.0] - 2020-01-19

- *PoolService*와 독립적으로 오브젝트 풀을 사용할 수 있도록 새로운 *ObjectPool*과 *GameObjectPool* 풀 추가. 이를 통해 프로젝트의 서로 다른 오브젝트 컨트롤러에서 같은 타입의 서로 다른 풀을 가질 수 있습니다
- 엔티티가 풀에서 정리될 때 콜백 메서드를 제공하는 새로운 *IPoolEntityClear* 인터페이스 추가
- *ObjectPool*에 대한 새로운 단위 테스트 추가

**변경**:
- *PoolService.Clear()*가 더 이상 액션 매개변수를 받지 않습니다. 엔티티 정리 시 콜백을 받으려면 엔티티에 *IPoolEntityClear* 인터페이스를 구현하세요

## [0.1.1] - 2020-01-06

**신규**:
- 라이선스 추가

## [0.1.0] - 2020-01-06

- 패키지 배포를 위한 최초 제출
