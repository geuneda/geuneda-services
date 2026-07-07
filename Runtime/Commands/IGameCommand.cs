// ReSharper disable once CheckNamespace

namespace Geuneda.Services.Commands
{
	/// <summary>
	/// Tags the interface as a <see cref="IGameCommand{TGameLogic}"/>
	/// </summary>
	public interface IGameCommandBase {}

	/// <summary>
	/// Contract for the command to be executed in the <see cref="ICommandService{TGameLogic}"/>.
	/// Implement this interface if you want logic to be executed on the server
	/// </summary>
	/// <remarks>
	/// Follows the Command pattern <see cref="https://en.wikipedia.org/wiki/Command_pattern"/>
	/// </remarks>
	public interface IGameServerCommand<in TGameLogic> : IGameCommandBase where TGameLogic : class
	{
		/// <summary>
		/// Executes the command logic defined by the implemention of this interface
		/// </summary>
		void ExecuteLogic(TGameLogic gameLogic);
	}

	/// <summary>
	/// Interface representing the command to be executed in the <see cref="ICommandService{TGameLogic}"/>.
	/// Implement this interface with the proper command logic
	/// </summary>
	/// <remarks>
	/// Follows the Command pattern <see cref="https://en.wikipedia.org/wiki/Command_pattern"/>
	/// </remarks>
	public interface IGameCommand<in TGameLogic> : IGameCommandBase where TGameLogic : class
	{
		/// <summary>
		/// Executes the command logic defined by the implemention of this interface
		/// </summary>
		void Execute(TGameLogic gameLogic, IMessageBrokerService messageBroker);
	}
}
