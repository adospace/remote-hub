using UserControl = System.Windows.Controls.UserControl;

namespace RemoteHub.Controls;

/// <summary>
/// Hosts a single RDP session: a <c>WindowsFormsHost</c> wrapping an <see cref="RdpClientHost"/>
/// plus a small session toolbar. The bound <c>SessionViewModel</c> drives connect/disconnect.
/// </summary>
public partial class RdpSessionView : UserControl
{
    private readonly RdpClientHost _client = new();

    public RdpSessionView()
    {
        InitializeComponent();
        RdpHostContainer.Child = _client;
        // TODO(Implement): on Connect, read the bound RdpConnection, call _client.Setup(...) then Connect().
    }

    /// <summary>The hosted RDP ActiveX wrapper.</summary>
    public RdpClientHost Client => _client;
}
