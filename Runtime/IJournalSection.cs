// ReSharper disable once CheckNamespace

namespace Geuneda.Services
{
	/// <summary>
	/// <see cref="DataJournal"/> 에 들어가는 값 묶음 하나입니다. 자주 바뀌는 값만 담습니다.
	/// </summary>
	public interface IJournalSection
	{
		/// <summary>
		/// 값을 씁니다. 저널을 쓸 때마다 불리므로 할당하지 않아야 합니다
		/// </summary>
		void Write(JournalWriter writer);

		/// <summary>
		/// <see cref="Write"/> 와 같은 순서로 값을 임시로 읽습니다. 실제 데이터를 바꾸지 않습니다.
		/// 부팅 때만 불리므로 할당해도 됩니다
		/// </summary>
		/// <param name="frameSequence">이 값이 든 프레임 번호. JSON 이 더 새로우면 <see cref="Apply"/> 에서 무시하는 데 씁니다</param>
		void Read(JournalReader reader, long frameSequence);

		/// <summary>
		/// 모든 섹션을 읽는 데 성공한 뒤에만 불립니다. <see cref="Read"/> 로 읽은 값을 실제 데이터에 넣습니다
		/// </summary>
		void Apply();
	}
}
