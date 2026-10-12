// ReSharper disable once CheckNamespace

namespace Geuneda.Services.Commands
{
	/// <summary>
	/// 인터페이스를 <see cref="IGameCommand{TGameLogic}"/> 로 표시하는 태그 인터페이스
	/// </summary>
	public interface IGameCommandBase {}

	/// <summary>
	/// <see cref="ICommandService{TGameLogic}"/> 에서 실행될 커맨드의 계약입니다.
	/// 로직을 서버에서 실행하려면 이 인터페이스를 구현하세요
	/// </summary>
	/// <remarks>
	/// 커맨드 패턴을 따릅니다 <see cref="https://en.wikipedia.org/wiki/Command_pattern"/>
	/// </remarks>
	public interface IGameServerCommand<in TGameLogic> : IGameCommandBase where TGameLogic : class
	{
		/// <summary>
		/// 이 인터페이스 구현에서 정의한 커맨드 로직을 실행합니다
		/// </summary>
		void ExecuteLogic(TGameLogic gameLogic);
	}

	/// <summary>
	/// <see cref="ICommandService{TGameLogic}"/> 에서 실행될 커맨드를 나타내는 인터페이스입니다.
	/// 알맞은 커맨드 로직으로 이 인터페이스를 구현하세요
	/// </summary>
	/// <remarks>
	/// 커맨드 패턴을 따릅니다 <see cref="https://en.wikipedia.org/wiki/Command_pattern"/>
	/// </remarks>
	public interface IGameCommand<in TGameLogic> : IGameCommandBase where TGameLogic : class
	{
		/// <summary>
		/// 이 인터페이스 구현에서 정의한 커맨드 로직을 실행합니다
		/// </summary>
		void Execute(TGameLogic gameLogic, IMessageBrokerService messageBroker);
	}
}
