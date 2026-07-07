using Geuneda.Services;
using Geuneda.Services.Commands;
using NSubstitute;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.Services.Tests
{
	[TestFixture]
	public class CommandServiceTest
	{
		private CommandService<IGameLogicMockup> _commandService;
		private IGameLogicMockup _gameLogicMockup;

		// ReSharper disable once MemberCanBePrivate.Global
		public interface IGameLogicMockup
		{
			void CallMockup(int payload);
		}

		private class CommandMockup : IGameCommand<IGameLogicMockup>
		{
			public int Payload;
			
			public void Execute(IGameLogicMockup gameLogic, IMessageBrokerService messageBroker)
			{
				gameLogic.CallMockup(Payload);
			}
		}

		private class ServerCommandMockup : IGameServerCommand<IGameLogicMockup>
		{
			public int Payload;

			public void ExecuteLogic(IGameLogicMockup gameLogic)
			{
				gameLogic.CallMockup(Payload);
			}
		}

		[SetUp]
		public void Init()
		{
			_gameLogicMockup = Substitute.For<IGameLogicMockup>();
			_commandService = new CommandService<IGameLogicMockup>(_gameLogicMockup, Substitute.For<IMessageBrokerService>());
		}

		[Test]
		public void ExecuteCommand_Successfully()
		{
			var payload = 1;
			var command = new CommandMockup { Payload = payload };
			
			_commandService.ExecuteCommand(command);
			
			_gameLogicMockup.Received().CallMockup(Arg.Is(payload));
		}

		[Test]
		public void ServerCommand_ExecuteLogic_InvokedWithGameLogic()
		{
			var payload = 7;
			IGameServerCommand<IGameLogicMockup> command = new ServerCommandMockup { Payload = payload };

			command.ExecuteLogic(_gameLogicMockup);

			_gameLogicMockup.Received(1).CallMockup(Arg.Is(payload));
		}
	}
}