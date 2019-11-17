using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

public class TrayIconForm : ApplicationContext
{
    private NotifyIcon trayIcon;

    public TrayIconForm()
    {
        trayIcon = new NotifyIcon()
        {
            Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location),
            ContextMenu = new ContextMenu(new MenuItem[] {
                new MenuItem("Exit", Exit)
            }),
            Visible = true
        };
    }

    void Exit(object sender, EventArgs e)
    {
        // Hide tray icon, otherwise it will remain shown until user mouses over it
        trayIcon.Visible = false;

        Application.Exit();
    }
}
