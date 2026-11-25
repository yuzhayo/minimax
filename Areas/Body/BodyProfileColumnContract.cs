using System.Windows.Forms;
using AppFoundation.Discovery;
using ChromeProfileLauncher.App;

namespace ChromeProfileLauncher.Areas.Body;

internal interface IBodyProfileColumn : IFoundationScopedDeclaration
{
    int Order { get; }
    DataGridViewColumn Build(IMiniMaxBodyContext context);
}
