namespace RemoteHub.Core.Models;

/// <summary>
/// When to route the connection through an RD Gateway. Values match the RDP control's
/// <c>GatewayUsageMethod</c>.
/// </summary>
public enum GatewayUsage
{
    None = 0,
    Always = 1,
    IfDirectConnectionFails = 2,
}

/// <summary>
/// How the user authenticates to the RD Gateway. Values match the RDP control's
/// <c>GatewayCredsSource</c>.
/// </summary>
public enum GatewayLogonMethod
{
    Password = 0,
    SmartCard = 1,
    ChooseLater = 4,
}

/// <summary>
/// RD Gateway preferences for an <see cref="RdpConnection"/>. With <see cref="Usage"/> left at
/// <see cref="GatewayUsage.None"/> the control's own gateway defaults are not touched at all.
/// </summary>
public sealed class RdpGatewaySettings
{
    public GatewayUsage Usage { get; set; } = GatewayUsage.None;

    public string? Host { get; set; }

    public GatewayLogonMethod LogonMethod { get; set; } = GatewayLogonMethod.ChooseLater;

    /// <summary>Uses the gateway credentials for the remote computer too, so they are asked for once.</summary>
    public bool ShareCredentials { get; set; } = true;
}
