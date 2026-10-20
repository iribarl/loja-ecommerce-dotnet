using System.Net;
using System.Text;
using Loja.Shipping.Contracts;
using Loja.Shipping.Infrastructure;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Loja.Shipping.Tests;

/// <summary>
/// The ViaCEP client against simulated answers: tests never call the real service
/// (a test that depends on the internet fails for reasons unrelated to the code).
/// </summary>
public sealed class ViaCepAddressLookupTests : IDisposable
{
    private const string PaulistaJson = """
        {"cep":"01310-100","logradouro":"Avenida Paulista","complemento":"de 612 a 1510 - lado par",
         "bairro":"Bela Vista","localidade":"São Paulo","uf":"SP","ibge":"3550308"}
        """;

    private readonly ServiceProvider _services = new ServiceCollection().AddHybridCache().Services.BuildServiceProvider();
    private readonly FakeHandler _handler = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ExistingCep_ReturnsTheAddress()
    {
        _handler.Respond(HttpStatusCode.OK, PaulistaJson);

        var result = await Lookup().LookupAsync("01310100", Ct);

        result.Status.ShouldBe(AddressLookupStatus.Found);
        result.Address.ShouldBe(new AddressInfo("01310-100", "Avenida Paulista", "Bela Vista", "São Paulo", "SP"));
        _handler.LastPath.ShouldBe("/ws/01310100/json/");
    }

    [Theory]
    [InlineData("""{"erro": true}""")]
    [InlineData("""{"erro": "true"}""")]
    public async Task UnknownCep_IsNotFound(string body)
    {
        _handler.Respond(HttpStatusCode.OK, body);

        (await Lookup().LookupAsync("99999-999", Ct)).Status.ShouldBe(AddressLookupStatus.NotFound);
    }

    [Fact]
    public async Task InvalidCep_DoesNotCallTheService()
    {
        (await Lookup().LookupAsync("123", Ct)).Status.ShouldBe(AddressLookupStatus.InvalidCep);
        _handler.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ServiceError_IsUnavailable_AndNotCached()
    {
        _handler.Respond(HttpStatusCode.InternalServerError, "oops");
        var lookup = Lookup();

        (await lookup.LookupAsync("01310-100", Ct)).Status.ShouldBe(AddressLookupStatus.Unavailable);

        // The service is back: the failure was not cached, so it is asked again.
        _handler.Respond(HttpStatusCode.OK, PaulistaJson);
        (await lookup.LookupAsync("01310-100", Ct)).Status.ShouldBe(AddressLookupStatus.Found);
    }

    [Fact]
    public async Task Timeout_IsUnavailable()
    {
        _handler.Fail(new TaskCanceledException("The request timed out."));

        (await Lookup().LookupAsync("01310-100", Ct)).Status.ShouldBe(AddressLookupStatus.Unavailable);
    }

    [Fact]
    public async Task SameCepTwice_AsksTheServiceOnce()
    {
        _handler.Respond(HttpStatusCode.OK, PaulistaJson);
        var lookup = Lookup();

        await lookup.LookupAsync("01310-100", Ct);
        await lookup.LookupAsync("01310100", Ct);

        _handler.Calls.ShouldBe(1);
    }

    public void Dispose() => _services.Dispose();

    private ViaCepAddressLookup Lookup() =>
        new(new HttpClient(_handler) { BaseAddress = new Uri("https://viacep.test/") },
            _services.GetRequiredService<HybridCache>(),
            NullLogger<ViaCepAddressLookup>.Instance);

    private sealed class FakeHandler : HttpMessageHandler
    {
        private Func<HttpResponseMessage> _respond = () => new HttpResponseMessage(HttpStatusCode.NotFound);

        public int Calls { get; private set; }

        public string? LastPath { get; private set; }

        public void Respond(HttpStatusCode status, string body) =>
            _respond = () => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        public void Fail(Exception exception) => _respond = () => throw exception;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastPath = request.RequestUri!.AbsolutePath;
            return Task.FromResult(_respond());
        }
    }
}
