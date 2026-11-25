using System.Windows.Forms;
using ChromeProfileLauncher.App;

namespace ChromeProfileLauncher.Areas.Body;
internal sealed class BodySignInActionColumn : IBodyProfileColumn
{
    public string Scope => "MiniMax"; public string Id => "sign-in"; public int Order => 3;
    public DataGridViewColumn Build(IMiniMaxBodyContext context)
    {
        if (!context.RowActions.TryGet(Id, out var action)) throw new System.InvalidOperationException("Missing row action: " + Id);
        return new DataGridViewButtonColumn { Name = Id, Tag = Id, HeaderText = action.Header,
            DataPropertyName = action.StatusProperty, Width = action.Width, UseColumnTextForButtonValue = false, ReadOnly = true };
    }
}
