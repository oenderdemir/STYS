namespace STYS.Agent.Modules.Pavo.Commands;

public sealed class PavoConnectionTestCommand : STYS.Agent.Client.Commands.IAgentCommand
{
    public string CommandType => "PavoConnectionTest";
}
