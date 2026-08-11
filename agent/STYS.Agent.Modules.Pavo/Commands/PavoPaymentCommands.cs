using STYS.Agent.Client.Commands;
using STYS.Agent.Contracts.Dtos;

namespace STYS.Agent.Modules.Pavo.Commands;

public sealed class PavoStartPaymentCommand : PavoStartPaymentRequest, IAgentCommand
{
    public string CommandType => "PavoStartPayment";
}

public sealed class PavoGetPaymentResultCommand : PavoGetPaymentResultRequest, IAgentCommand
{
    public string CommandType => "PavoGetPaymentResult";
}
