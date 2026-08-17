using Microsoft.Extensions.Logging.Abstractions;
using STYS.Agent.Client.Commands;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Modules.Pavo;
using STYS.Agent.Modules.Pavo.Commands;

namespace STYS.Tests.Agent;

public sealed class PavoCommandHandlerRuntimeParityTests
{
    [Fact]
    public async Task StartPayment_HttpResponseAlindiysa_SequenceBackendCompletiondanBagimsizAdvanceOlur()
    {
        var sequenceService = new FakeSequenceReservationService();
        var client = new FakePavoRestClient
        {
            StartPaymentResponse = new PavoStartPaymentResponse
            {
                HttpSuccess = false,
                HttpResponseReceived = true,
                Message = "DEVICE MESSAGE",
                ErrorCode = 0,
                HasError = false,
                HasAbondon = false,
                Data = new PavoPaymentOperationData
                {
                    IsSuccessful = false,
                    FailMessage = "REJECTED",
                    ResultCode = "55"
                }
            }
        };
        var handler = new PavoStartPaymentCommandHandler(client, sequenceService, NullLogger<PavoStartPaymentCommandHandler>.Instance);

        var result = await handler.HandleAsync(new PavoStartPaymentCommand
        {
            PosCihaziId = 9001,
            SaleReference = "SALE-1",
            Amount = 100m,
            CurrencyCode = "TRY",
            IpAddress = "10.0.0.5",
            HttpPort = 4567,
            TransactionHandle = new PavoTransactionHandle
            {
                SerialNumber = "SN-1",
                Fingerprint = "FP-1",
                TransactionSequence = 7,
                TransactionDate = DateTime.Now
            }
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.HttpResponseReceived);
        Assert.Equal(1, sequenceService.AdvanceCount);
        Assert.Equal("SN-1", sequenceService.LastSerialNumber);
    }

    [Fact]
    public async Task Pairing_BodyReadFailure_HttpResponseReceivedTrueOlur_SequenceAdvanceEdilir()
    {
        var sequenceService = new FakeSequenceReservationService();
        var client = new FakePavoRestClient
        {
            PairingException = new PavoRestClientException("BODY_READ_FAILED", "read failed", httpResponseReceived: true)
        };
        var handler = new PavoPairingCommandHandler(client, sequenceService, NullLogger<PavoPairingCommandHandler>.Instance);

        var result = await handler.HandleAsync(new PavoPairingCommand
        {
            PosCihaziId = 9002,
            IpAddress = "10.0.0.5",
            HttpPort = 4567,
            TransactionHandle = new PavoTransactionHandle
            {
                SerialNumber = "SN-2",
                Fingerprint = "FP-2",
                TransactionSequence = 8,
                TransactionDate = DateTime.Now
            }
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.HttpResponseReceived);
        Assert.Equal(1, sequenceService.AdvanceCount);
        Assert.Equal("SN-2", sequenceService.LastSerialNumber);
    }

    private sealed class FakeSequenceReservationService : IPavoCommandSequenceReservationService
    {
        public int AdvanceCount { get; private set; }
        public string? LastSerialNumber { get; private set; }

        public Task AdvanceAsync(int centralPosCihaziId, string? serialNumber, CancellationToken cancellationToken)
        {
            _ = centralPosCihaziId;
            LastSerialNumber = serialNumber;
            _ = cancellationToken;
            AdvanceCount++;
            return Task.CompletedTask;
        }

        public Task<PavoTransactionHandle> ReserveAsync(int centralPosCihaziId, string? serialNumber, DateTime? transactionDate, CancellationToken cancellationToken) =>
            Task.FromResult(new PavoTransactionHandle
            {
                SerialNumber = "SN",
                Fingerprint = "FP",
                TransactionSequence = 1,
                TransactionDate = transactionDate ?? DateTime.Now
            });

        public Task<PavoTransactionHandle> ReserveForPairingAsync(int centralPosCihaziId, string? serialNumber, DateTime? transactionDate, CancellationToken cancellationToken) =>
            ReserveAsync(centralPosCihaziId, serialNumber, transactionDate, cancellationToken);
    }

    private sealed class FakePavoRestClient : IPavoRestClient
    {
        public PavoPairingResponse? PairingResponse { get; set; }
        public PavoStartPaymentResponse? StartPaymentResponse { get; set; }
        public PavoRestClientException? PairingException { get; set; }
        public PavoRestClientException? StartPaymentException { get; set; }

        public Task<PavoPairingResponse> PairingAsync(PavoPairingRequest request, CancellationToken cancellationToken) =>
            PairingException is not null
                ? Task.FromException<PavoPairingResponse>(PairingException)
                : Task.FromResult(PairingResponse ?? new PavoPairingResponse { HttpSuccess = true, HttpResponseReceived = true });

        public Task<PavoStartPaymentResponse> StartPaymentAsync(PavoStartPaymentRequest request, CancellationToken cancellationToken) =>
            StartPaymentException is not null
                ? Task.FromException<PavoStartPaymentResponse>(StartPaymentException)
                : Task.FromResult(StartPaymentResponse ?? new PavoStartPaymentResponse { HttpSuccess = true, HttpResponseReceived = true, Data = new PavoPaymentOperationData { IsSuccessful = true } });

        public Task<PavoPerformEodResponse> PerformEodAsync(PavoPerformEodRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PavoRebootDeviceResponse> RebootDeviceAsync(PavoRebootDeviceRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PavoEnterPinModeResponse> EnterPinModeAsync(PavoEnterPinModeRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PavoExitPinModeResponse> ExitPinModeAsync(PavoExitPinModeRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PavoPingResponse> PingAsync(PavoPingRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PavoGetDeviceInfoResponse> GetDeviceInfoAsync(PavoGetDeviceInfoRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PavoGetPaymentResultResponse> GetPaymentResultAsync(PavoGetPaymentResultRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
