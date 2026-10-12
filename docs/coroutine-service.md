# 코루틴 서비스

[목차로 돌아가기](README.md)

MonoBehaviour 가 없는 순수 C# 클래스에서 Unity 코루틴을 실행합니다. 네임스페이스는 `Geuneda.Services` 입니다.

## 핵심 규칙

- 생성자에서 `DontDestroyOnLoad` GameObject 를 만들고 `CoroutineServiceMonoBehaviour` 를 붙입니다. 플레이 모드(런타임)에서 생성하세요. 싱글톤은 강제하지 않습니다.
- `StartCoroutine` 은 일반 Unity `Coroutine` 을 반환합니다(완료 콜백 없음).
- `StartAsyncCoroutine` 은 `IAsyncCoroutine` 을 반환합니다. `IsRunning`, `IsCompleted`, `StartTime`, `OnComplete(Action)`, `StopCoroutine(bool triggerOnComplete = false)` 를 제공합니다.
- `OnComplete(...)` 는 콜백을 **교체**합니다(누적되지 않음). 마지막으로 설정한 콜백 하나만 호출됩니다.
- `StartDelayCall(action, delay)` 의 인자 순서는 **action 이 먼저, delay 가 마지막**입니다. 지연 호출할 `action` 은 내부적으로 `OnComplete` 로 등록되므로, 반환된 핸들에 같은 형태의 `OnComplete` 를 다시 설정하면 원래 `action` 이 교체됩니다.
- `IAsyncCoroutine.StopCoroutine(triggerOnComplete)`: `true` 면 완료 콜백을 호출하고 `false`(기본값) 면 호출하지 않습니다. 두 경우 모두 이후 `IsCompleted == true`, `IsRunning == false` 가 되며, 이미 완료된 코루틴에 호출하면 아무 일도 하지 않습니다.
- 서비스의 `StopAllCoroutines()` / `StopCoroutine(Coroutine)` 은 Unity 코루틴만 멈춥니다. 이때 `IAsyncCoroutine` 핸들은 완료 처리되지 않고 콜백도 호출되지 않습니다. 핸들 상태가 필요하면 `IAsyncCoroutine.StopCoroutine(...)` 을 쓰세요.
- `Dispose()` 는 모든 코루틴을 멈추고 호스트 GameObject 를 파괴합니다.

## 사용 예

```csharp
using System.Collections;
using Geuneda.Services;
using UnityEngine;

public class Loader
{
    private readonly ICoroutineService _coroutines;

    public Loader(ICoroutineService coroutines)
    {
        _coroutines = coroutines;
    }

    public void Run()
    {
        // 일반 코루틴
        Coroutine handle = _coroutines.StartCoroutine(MyRoutine());
        _coroutines.StopCoroutine(handle);

        // 완료 콜백과 상태를 가진 코루틴
        IAsyncCoroutine asyncHandle = _coroutines.StartAsyncCoroutine(MyRoutine());
        asyncHandle.OnComplete(() => Debug.Log("Done"));

        if (asyncHandle.IsRunning)
        {
            asyncHandle.StopCoroutine(triggerOnComplete: false); // 콜백 없이 중지
        }

        // 완료 시 데이터를 넘겨주는 코루틴
        IAsyncCoroutine<int> typedHandle = _coroutines.StartAsyncCoroutine(MyRoutine(), 42);
        typedHandle.OnComplete(result => Debug.Log($"Finished with {result}"));

        // 지연 호출
        _coroutines.StartDelayCall(() => Debug.Log("2초 뒤"), 2f);
        _coroutines.StartDelayCall(msg => Debug.Log(msg), "Hello", 1f);
    }

    private IEnumerator MyRoutine()
    {
        yield return new WaitForSeconds(1f);
    }
}
```

`IAsyncCoroutine<T>` 는 `T Data { get; set; }` 와 `OnComplete(Action<T>)` 를 추가로 가집니다. `OnComplete(Action)` 과 `OnComplete(Action<T>)` 를 둘 다 설정하면 완료 시 둘 다 호출됩니다.
