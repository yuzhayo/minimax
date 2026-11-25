using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using ChromeProfileLauncher.Shared;
using ChromeProfileLauncher.Window;
using ChromeProfileLauncher.App;
using ChromeProfileLauncher.Models;
using ChromeProfileLauncher.Services;

namespace ChromeProfileLauncher.UI;

/// <summary>MiniMax's thin .NET 10 wrapper over its three-area window structure.</summary>
internal sealed class MiniMaxForm : AppWindow
{
    private readonly MiniMaxComposition _composition;
    internal MiniMaxAppContext AppContext => _composition.Context;

    public MiniMaxForm(MiniMaxSettingsStore? settings = null, WindowLayoutStore? layout = null,
        CheckedProfilesStore? checkedStore = null, ClaimedProfilesStore? claimedStore = null)
        : this(new MiniMaxComposition(settings, layout, checkedStore, claimedStore)) { }

    private MiniMaxForm(MiniMaxComposition composition)
        : base(Assembly.GetExecutingAssembly(), composition.Connections, composition.WindowOptions)
    {
        _composition = composition;
        SharedTheme.Apply(this);
    }

    public DataGridView ProfileTable => _composition.Context.MainTable ??
        throw new System.InvalidOperationException("Body table has not connected.");
    public BindingList<ProfileRow> Rows => _composition.Context.Rows;
    public List<ProfileRow> CheckedRows() => Rows.Where(row => row.IsChecked).ToList();
    internal string CheckInUrl => _composition.Context.Settings.CheckInUrl;
}
