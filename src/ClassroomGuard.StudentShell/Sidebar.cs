using System.Windows;
using System.Windows.Controls;
namespace ClassroomGuard.StudentShell;
public partial class MainWindow
{
    private bool _sidebarCollapsed;
    private void OnToggleSidebar(object sender,RoutedEventArgs e)
    {
        _sidebarCollapsed=!_sidebarCollapsed;
        SidebarColumn.Width=new GridLength(_sidebarCollapsed?76:(ActualWidth<1000?190:230));
        StudentSidebarBrand.Visibility=StudentSidebarFooter.Visibility=_sidebarCollapsed?Visibility.Collapsed:Visibility.Visible;
        CompactPcButton.Visibility=_sidebarCollapsed?Visibility.Visible:Visibility.Collapsed;
        foreach(var button in new[]{NavHome,NavLibrary,NavSites,NavApps,NavTests,NavTogarak,NavSettings})
        {
            if(button.Content is not StackPanel panel || panel.Children.Count<2)continue;
            var label=(TextBlock)panel.Children[1];button.ToolTip=label.Text;
            label.Visibility=_sidebarCollapsed?Visibility.Collapsed:Visibility.Visible;
            ((FrameworkElement)panel.Children[0]).Margin=new Thickness(0,0,_sidebarCollapsed?0:12,0);
            button.Padding=new Thickness(_sidebarCollapsed?12:14,12,_sidebarCollapsed?12:14,12);
        }
    }
}
