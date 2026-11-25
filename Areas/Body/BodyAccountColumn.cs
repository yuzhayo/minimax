using System.Windows.Forms;
using ChromeProfileLauncher.App;
using ChromeProfileLauncher.Models;

namespace ChromeProfileLauncher.Areas.Body;
internal sealed class BodyAccountColumn : IBodyProfileColumn
{
    public string Scope => "MiniMax"; public string Id => "account"; public int Order => 2;
    public DataGridViewColumn Build(IMiniMaxBodyContext context) => new DataGridViewTextBoxColumn
    { Name = Id, DataPropertyName = nameof(ProfileRow.UserName), HeaderText = "Account", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true };
}
