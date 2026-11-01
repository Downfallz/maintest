using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>The request a push service receives, against a listener standing in for one: signed with the host's keys, encrypted, and read for the one answer that matters.</summary>
public sealed class WebPushSenderTests
{
    [Fact]
    public async Task A_notice_is_signed_with_the_host_key_encrypted_to_the_browser_and_kept_ten_minutes()
    {
        var keys = PushKeys.Generate() with { Subject = "mailto:table@example.org" };
        using var service = new PushService(201);
        using var sender = new WebPushSender(keys, service.Client);

        (await sender.SendAsync(service.Subscription(), """{"title":"Your turn"}""", TestContext.Current.CancellationToken)).ShouldBeTrue();

        var request = service.Received.ShouldHaveSingleItem();
        request.Headers["Authorization"].ShouldNotBeNull().ShouldStartWith("vapid t=");
        request.Headers["Authorization"]!.ShouldContain($"k={keys.PublicKey}");
        request.Headers["Content-Encoding"].ShouldBe("aes128gcm");
        request.Headers["TTL"].ShouldBe(((int)WebPushSender.KeptFor.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Headers["Topic"].ShouldBe("turn");
        request.Headers["Urgency"].ShouldBe("high");
        request.Body.Contains("Your turn", StringComparison.Ordinal).ShouldBeFalse("the payload is encrypted to the browser");
    }

    [Fact]
    public async Task What_the_sender_encrypts_the_browser_s_keys_decrypt()
    {
        using var service = new PushService(201);
        using var sender = new WebPushSender(PushKeys.Generate(), service.Client);
        using var browser = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var auth = RandomNumberGenerator.GetBytes(16);
        var point = browser.PublicKey.ExportSubjectPublicKeyInfo()[^65..];
        var subscription = new PushSubscription($"{service.Url}push/one", Base64Url.EncodeToString(point), Base64Url.EncodeToString(auth));

        await sender.SendAsync(subscription, """{"title":"Your turn"}""", TestContext.Current.CancellationToken);

        var body = service.Received.ShouldHaveSingleItem().Bytes;
        // RFC 8188 header: salt, record size, the sender's public key; then one record (RFC 8291).
        var salt = body[..16];
        var senderPoint = body[21..86];
        var record = body[86..];
        using var senderKey = ECDiffieHellman.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, Q = new ECPoint { X = senderPoint[1..33], Y = senderPoint[33..65] } });
        var shared = browser.DeriveRawSecretAgreement(senderKey.PublicKey);
        var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, 32, auth, [.. "WebPush: info\0"u8, .. point, .. senderPoint]);
        var key = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt, "Content-Encoding: aes128gcm\0"u8.ToArray());
        var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, salt, "Content-Encoding: nonce\0"u8.ToArray());
        var plain = new byte[record.Length - 16];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(nonce, record[..^16], record[^16..], plain);

        System.Text.Encoding.UTF8.GetString(plain).TrimEnd('\0').TrimEnd('\u0002').ShouldBe("""{"title":"Your turn"}""");
    }

    [Theory]
    [InlineData(404)]
    [InlineData(410)]
    public async Task A_subscription_the_service_no_longer_knows_is_reported_gone(int status)
    {
        using var service = new PushService(status);
        using var sender = new WebPushSender(PushKeys.Generate(), service.Client);

        (await sender.SendAsync(service.Subscription(), "{}", TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_service_that_is_out_is_an_http_failure_and_not_a_lost_subscription()
    {
        using var service = new PushService(503);
        using var sender = new WebPushSender(PushKeys.Generate(), service.Client);

        var failure = await Should.ThrowAsync<HttpRequestException>(() => sender.SendAsync(service.Subscription(), "{}", TestContext.Current.CancellationToken));

        failure.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    /// <summary>A push service on this machine: it answers one status, and keeps what it was sent.</summary>
    private sealed class PushService : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly string _url;

        public PushService(int status)
        {
            var port = FreePort();
            _url = $"http://127.0.0.1:{port}/";
            _listener.Prefixes.Add(_url);
            _listener.Start();
            _ = AnswerAsync(status);
            Client = new HttpClient();
        }

        public HttpClient Client { get; }

        public string Url => _url;

        public List<(System.Collections.Specialized.NameValueCollection Headers, string Body, byte[] Bytes)> Received { get; } = [];

        /// <summary>A browser's subscription to this service: its own P-256 key and sixteen bytes of auth secret, as the browser would serialise them.</summary>
        public PushSubscription Subscription()
        {
            using var browser = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var parameters = browser.ExportParameters(includePrivateParameters: false);
            var point = new byte[65];
            point[0] = 0x04;
            parameters.Q.X!.CopyTo(point, 1 + (32 - parameters.Q.X!.Length));
            parameters.Q.Y!.CopyTo(point, 33 + (32 - parameters.Q.Y!.Length));
            // The sender takes an https endpoint from a page; the listener here is reached by its own address.
            return new PushSubscription($"{_url}push/one", Base64Url.EncodeToString(point), Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16)));
        }

        private async Task AnswerAsync(int status)
        {
            try
            {
                while (_listener.IsListening)
                {
                    var context = await _listener.GetContextAsync();
                    using var buffer = new MemoryStream();
                    await context.Request.InputStream.CopyToAsync(buffer);
                    var bytes = buffer.ToArray();
                    Received.Add((context.Request.Headers, System.Text.Encoding.Latin1.GetString(bytes), bytes));
                    context.Response.StatusCode = status;
                    context.Response.Close();
                }
            }
            catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException)
            {
                // The listener was closed under the wait, which is how this test ends.
            }
        }

        private static int FreePort()
        {
            using var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            return ((IPEndPoint)probe.LocalEndpoint).Port;
        }

        public void Dispose()
        {
            _listener.Close();
            Client.Dispose();
        }
    }
}
