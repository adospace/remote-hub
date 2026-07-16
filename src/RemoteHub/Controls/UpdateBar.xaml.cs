using RemoteHub.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace RemoteHub.Controls;

/// <summary>
/// Bottom-of-window strip announcing an available update. Bound to an <see cref="UpdateViewModel"/>
/// supplied by the host (MainWindow sets the DataContext); the control itself is pure presentation.
/// </summary>
public partial class UpdateBar : UserControl
{
    public UpdateBar() => InitializeComponent();
}
