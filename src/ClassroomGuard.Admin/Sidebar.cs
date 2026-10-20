using System.Windows;
using System.Windows.Controls;
namespace ClassroomGuard.Admin;
public partial class MainWindow
{
    private bool _sidebarCollapsed;
    private void OnToggleAdminSidebar(object sender,RoutedEventArgs e)
    {
        _sidebarCollapsed=!_sidebarCollapsed;
        AdminSidebarColumn.Width=new GridLength(_sidebarCollapsed?76:200);
        AdminSidebarBrand.Visibility=AdminSidebarFooter.Visibility=_sidebarCollapsed?Visibility.Collapsed:Visibility.Visible;
        foreach(var button in new[]{NavDash,NavPush,NavSettings})
        {
            if(button.Content is not StackPanel panel || panel.Children.Count<2)continue;
            var label=(TextBlock)panel.Children[1];button.ToolTip=label.Text;label.Visibility=_sidebarCollapsed?Visibility.Collapsed:Visibility.Visible;
            ((FrameworkElement)panel.Children[0]).Margin=new Thickness(0,0,_sidebarCollapsed?0:10,0);
            button.Padding=new Thickness(12);
        }
    }
}
