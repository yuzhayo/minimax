using System.Drawing;
using System.Windows.Forms;

namespace ChromeProfileLauncher.Shared;

public class SharedActionButton : Button
{
    public SharedActionButton(string label)
    {
        Text = label;
        Dock = DockStyle.Fill;
        Margin = new Padding(6, 2, 6, 2);
        Padding = new Padding(8, 0, 8, 0);
        MinimumSize = new Size(0, 30);
        TextAlign = ContentAlignment.MiddleCenter;
    }
}
