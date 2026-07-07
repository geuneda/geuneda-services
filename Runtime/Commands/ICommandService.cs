// ReSharper disable once CheckNamespace

namespace Geuneda.Services.Commands
{
	/// <summary>
	/// This service provides the possibility to execute a <see cref="IGameCommand{TGameLogic}"/>.
	/// It allows to create a seamless abstraction layer of execution between the game logic and any other part of the code 
	/// </summary>
	public interface ICommandService<out TGameLogic> where TGameLogic : class
	{
		/// <summary>
		/// Executes the given <paramref name="command"/>
		/// </summary>
		/// <remarks>
		/// IMPORTANT: Defines the <paramref name="command"/> as a class object if logic execution is asynchronous.
		/// Define as a struct if logic execution is non waitable.
		/// </remarks>
		void ExecuteCommand<TCommand>(TCommand command) where TCommand : IGameCommand<TGameLogic>;
	}
}
