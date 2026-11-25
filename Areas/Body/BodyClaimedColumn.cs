using System.Windows.Forms;
using ChromeProfileLauncher.App;
using ChromeProfileLauncher.Models;

namespace ChromeProfileLauncher.Areas.Body;
internal sealed class BodyClaimedColumn : IBodyProfileColumn
{
    public string Scope => "MiniMax"; public string Id => "claimed"; public int Order => 0;
    public DataGridViewColumn Build(IMiniMaxBodyContext context) => new DataGridViewCheckBoxColumn
    { Name = Id, DataPropertyName = nameof(ProfileRow.IsClaimed), HeaderText = "Claimed", Width = 70, ReadOnly = true, ThreeState = false };
}
