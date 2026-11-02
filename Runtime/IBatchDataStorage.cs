// ReSharper disable once CheckNamespace

namespace Geuneda.Services
{
	/// <summary>
	/// 여러 값을 한 번의 커밋으로 묶어 쓸 수 있는 저장소입니다. <see cref="DataService.SaveAllData"/> 가 씁니다.
	/// </summary>
	public interface IBatchDataStorage : IDataStorage
	{
		/// <summary>
		/// 이후의 쓰기를 <see cref="EndBatch"/> 까지 커밋하지 않습니다
		/// </summary>
		void BeginBatch();

		/// <summary>
		/// <see cref="BeginBatch"/> 뒤의 쓰기를 커밋합니다
		/// </summary>
		void EndBatch();
	}
}
