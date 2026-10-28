// ReSharper disable once CheckNamespace

namespace Geuneda.Services
{
	/// <summary>
	/// <see cref="DataService"/> 가 직렬화한 문자열을 읽고 쓰는 영구 저장소입니다.
	/// 기본 제공 저장소(<see cref="PlayerPrefsDataStorage"/>, <see cref="FileDataStorage"/>, <see cref="BrowserLocalDataStorage"/>) 외의
	/// 곳에 저장하려면 이 인터페이스를 구현합니다.
	/// </summary>
	public interface IDataStorage
	{
		/// <summary>
		/// <paramref name="key"/> 에 저장된 값이 있는지 확인합니다
		/// </summary>
		bool HasKey(string key);

		/// <summary>
		/// <paramref name="key"/> 에 저장된 값을 읽습니다. 저장된 값이 없으면 <c>null</c> 입니다
		/// </summary>
		string Read(string key);

		/// <summary>
		/// <paramref name="key"/> 에 <paramref name="value"/> 를 저장합니다. 이전 값은 덮어씁니다.
		/// 호출이 돌아오면 값이 영구 저장된 상태여야 합니다
		/// </summary>
		void Write(string key, string value);

		/// <summary>
		/// <paramref name="key"/> 에 저장된 값을 지웁니다. 저장된 값이 없으면 아무것도 하지 않습니다
		/// </summary>
		void Delete(string key);
	}
}
