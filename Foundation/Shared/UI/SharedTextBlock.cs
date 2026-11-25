using System.Drawing;
using System.Windows.Forms;

namespace AppFoundation.Shared.UI;

public class SharedTextBlock : Label
{
    public SharedTextBlock(bool muted = false)
    {
        Dock = DockStyle.Fill;
        TextAlign = ContentAlignment.MiddleLeft;
        Tag = muted ? "Muted" : null;
    }
}
