using System.Windows.Forms;
using ChromeProfileLauncher.Shared;
using ChromeProfileLauncher.App;

namespace ChromeProfileLauncher.Areas.Body;

internal interface IBodyProfileColumn : IScopedDeclaration
{
    int Order { get; }
    DataGridViewColumn Build(IMiniMaxBodyContext context);
}
