using Geuneda.Services.Commands;

// ReSharper disable once CheckNamespace

namespace Geuneda.Services
{
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
