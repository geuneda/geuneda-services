// ReSharper disable once CheckNamespace

namespace Geuneda.Services
{
	/// <summary>
	/// 인터페이스를 <see cref="IGameCommand{TGameLogic}"/>으로 태그합니다
	/// </summary>
	public interface IGameCommandBase {}

	/// <summary>
	/// <see cref="ICommandService{TGameLogic}"/>에서 실행될 커맨드의 계약입니다.
	/// 서버에서 로직을 실행하려면 이 인터페이스를 구현하세요.
	/// </summary>
	/// <remarks>
	/// 커맨드 패턴을 따릅니다 <see cref="https://en.wikipedia.org/wiki/Command_pattern"/>
	/// </remarks>
	public interface IGameServerCommand<in TGameLogic> : IGameCommandBase where TGameLogic : class
	{
		/// <summary>
		/// 이 인터페이스의 구현에 정의된 커맨드 로직을 실행합니다
		/// </summary>
		void ExecuteLogic(TGameLogic gameLogic);
	}

	/// <summary>
	/// <see cref="ICommandService{TGameLogic}"/>에서 실행될 커맨드를 나타내는 인터페이스입니다.
	/// 적절한 커맨드 로직으로 이 인터페이스를 구현하세요.
	/// </summary>
	/// <remarks>
	/// 커맨드 패턴을 따릅니다 <see cref="https://en.wikipedia.org/wiki/Command_pattern"/>
	/// </remarks>
	public interface IGameCommand<in TGameLogic> : IGameCommandBase where TGameLogic : class
	{
		/// <summary>
		/// 이 인터페이스의 구현에 정의된 커맨드 로직을 실행합니다
		/// </summary>
		void Execute(TGameLogic gameLogic, IMessageBrokerService messageBroker);
	}
	
	/// <summary>
	/// <see cref="IGameCommand{TGameLogic}"/>을 실행할 수 있는 서비스입니다.
	/// 게임 로직과 코드의 다른 부분 사이에 원활한 실행 추상화 계층을 생성합니다.
	/// </summary>
	public interface ICommandService<out TGameLogic> where TGameLogic : class
	{
		/// <summary>
		/// 주어진 <paramref name="command"/>를 실행합니다
		/// </summary>
		/// <remarks>
		/// 중요: 로직 실행이 비동기인 경우 <paramref name="command"/>를 클래스 객체로 정의하세요.
		/// 로직 실행이 대기 불필요한 경우 구조체로 정의하세요.
		/// </remarks>
		void ExecuteCommand<TCommand>(TCommand command) where TCommand : IGameCommand<TGameLogic>;
	}
	
	/// <inheritdoc />
	public class CommandService<TGameLogic> : ICommandService<TGameLogic> where TGameLogic : class
	{
		private readonly TGameLogic _gameLogic;
		private readonly IMessageBrokerService _messageBroker;
		
		protected TGameLogic  GameLogic => _gameLogic;
		protected IMessageBrokerService MessageBroker => _messageBroker;

		public CommandService(TGameLogic gameLogic, IMessageBrokerService messageBroker)
		{
			_gameLogic = gameLogic;
			_messageBroker = messageBroker;
		}

		/// <inheritdoc />
		public void ExecuteCommand<TCommand>(TCommand command) where TCommand : IGameCommand<TGameLogic>
		{
			command.Execute(_gameLogic, _messageBroker);
		}
	}
}