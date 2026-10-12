using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable CheckNamespace

namespace Geuneda.Services.AssetsImporter
{
	/// <summary>
	/// 로딩 메서드를 보조하는 유틸리티 클래스
	/// </summary>
	public static class AssetLoaderUtils
	{
		/// <summary>
		/// 주어진 <paramref name="tasks"/> 목록을 완료 순서대로 순회할 수 있게 하는 헬퍼 메서드입니다.
		/// 먼저 완료된 작업부터 반환하고, 모든 작업이 완료될 때까지 다음 작업으로 넘어갑니다.
		/// </summary>
		/// <remarks>
		/// .NET 엔지니어의 구현을 바탕으로 합니다 <seealso cref="https://devblogs.microsoft.com/pfxteam/processing-tasks-as-they-complete/"/>
		/// </remarks>
		public static Task<Task<T>>[] Interleaved<T>(IEnumerable<Task<T>> tasks)
		{
			var inputTasks = tasks.ToList();
			var buckets = new TaskCompletionSource<Task<T>>[inputTasks.Count];
			var results = new Task<Task<T>>[buckets.Length];
			var nextTaskIndex = -1;

			for (var i = 0; i < buckets.Length; i++)
			{
				buckets[i] = new TaskCompletionSource<Task<T>>();
				results[i] = buckets[i].Task;
			}

			foreach (var inputTask in inputTasks)
			{
				inputTask.ContinueWith(Continuation, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
			}

			return results;

			// 로컬 함수
			void Continuation(Task<T> completed)
			{
				buckets[Interlocked.Increment(ref nextTaskIndex)].TrySetResult(completed);
			}
		}
	}
}