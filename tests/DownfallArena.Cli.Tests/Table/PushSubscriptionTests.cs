using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The push services a browser may name (#1132): the host sends only to them, so a server a player controls cannot
/// hold the notices worker's time nor have the host call it.
/// </summary>
public sealed class PushSubscriptionTests
{
    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/abc")]
    [InlineData("https://FCM.GoogleAPIs.com/fcm/send/abc")]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/abc")]
    [InlineData("https://wns2-par02p.notify.windows.com/w/?token=abc")]
    [InlineData("https://web.push.apple.com/abc")]
    [InlineData("https://api.push.apple.com/abc")]
    public void An_endpoint_on_a_known_push_service_is_known(string endpoint) =>
        PushSubscription.OnKnownService(endpoint).ShouldBeTrue();

    [Theory]
    [InlineData("https://slow.example/abc")]
    [InlineData("http://fcm.googleapis.com/fcm/send/abc")]
    [InlineData("https://fcm.googleapis.com.evil.example/abc")]
    [InlineData("https://evilfcm.googleapis.com/abc")]
    [InlineData("https://fcm.googleapis.com.:443/abc")]
    [InlineData("https://fcm.googleapis.com:8443/abc")]
    [InlineData("https://fcm.googleapis.com@evil.example/abc")]
    [InlineData("https://user@fcm.googleapis.com/abc")]
    [InlineData("https://notify.windows.com/abc")]
    [InlineData("https://evilnotify.windows.com/abc")]
    [InlineData("https://push.apple.com/abc")]
    [InlineData("https://127.0.0.1/abc")]
    [InlineData("https://[::1]/abc")]
    [InlineData("fcm.googleapis.com/abc")]
    [InlineData("")]
    public void Any_other_endpoint_is_not(string endpoint) =>
        PushSubscription.OnKnownService(endpoint).ShouldBeFalse();
}
