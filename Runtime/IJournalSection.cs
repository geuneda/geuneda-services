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
		/// <see cref="Write"/> 와 같은 순서로 값을 읽어 복원합니다. 부팅 때만 불리므로 할당해도 됩니다
		/// </summary>
		void Read(JournalReader reader);
	}
}
