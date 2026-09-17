using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DownfallArena.Cli.Hosting;

/// <summary>
/// The addresses of this machine a host could bind, so a player is told what to type rather than sent to look
/// it up. Loopback is left out: it is the default, and naming it as an option would be naming the thing that
/// does not reach the phone.
/// </summary>
internal static class NetworkAddresses
{
    public static IReadOnlyList<string> OfThisMachine()
    {
        try
        {
            return
            [
                .. NetworkInterface.GetAllNetworkInterfaces()
                    .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up && adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)
                    .Select(unicast => unicast.Address)
                    .Where(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                    .Select(address => address.ToString())
                    .Distinct(StringComparer.Ordinal),
            ];
        }
        catch (NetworkInformationException)
        {
            // A machine that will not say what its interfaces are is one where the player reads the address off
            // their own network settings. It is a hint, and a hint that throws would be worse than none.
            return [];
        }
    }
}
