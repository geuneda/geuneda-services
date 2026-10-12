// ReSharper disable once CheckNamespace

namespace Geuneda.Services.Commands
{
	/// <summary>
	/// <see cref="IGameCommand{TGameLogic}"/> 를 실행할 수 있게 하는 서비스입니다.
	/// 게임 로직과 코드의 다른 부분 사이에 매끄러운 실행 추상화 계층을 만들 수 있습니다
	/// </summary>
	public interface ICommandService<out TGameLogic> where TGameLogic : class
	{
		/// <summary>
		/// 주어진 <paramref name="command"/> 를 실행합니다
		/// </summary>
		/// <remarks>
		/// 중요: 로직 실행이 비동기이면 <paramref name="command"/> 를 클래스 객체로 정의하세요.
		/// 로직 실행을 기다릴 필요가 없으면 struct 로 정의하세요.
		/// </remarks>
		void ExecuteCommand<TCommand>(TCommand command) where TCommand : IGameCommand<TGameLogic>;
	}
}
