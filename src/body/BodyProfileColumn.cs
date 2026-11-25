using System.Windows.Forms;
using ChromeProfileLauncher.App;
using ChromeProfileLauncher.Models;

namespace ChromeProfileLauncher.Areas.Body;
internal sealed class BodyProfileColumn : IBodyProfileColumn
{
    public string Scope => "MiniMax"; public string Id => "profile"; public int Order => 1;
    public DataGridViewColumn Build(IMiniMaxBodyContext context) => new DataGridViewTextBoxColumn
    { Name = Id, DataPropertyName = nameof(ProfileRow.DisplayName), HeaderText = "Profile", Width = 200, ReadOnly = true };
}
