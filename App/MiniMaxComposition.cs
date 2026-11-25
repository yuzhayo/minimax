using System;
using System.Drawing;
using System.IO;
using AppFoundation.Window;
using ChromeProfileLauncher.Services;

namespace ChromeProfileLauncher.App;

internal sealed class MiniMaxComposition
{
    public MiniMaxAppContext Context { get; }
    public FoundationConnectionRegistry Connections { get; }
    public FoundationWindowOptions WindowOptions { get; }

    public MiniMaxComposition(MiniMaxSettingsStore? settings = null, WindowLayoutStore? layout = null,
        CheckedProfilesStore? checkedStore = null, ClaimedProfilesStore? claimedStore = null)
    {
        Context = new(settings ?? new MiniMaxSettingsStore(), layout ?? new WindowLayoutStore(),
            checkedStore ?? new CheckedProfilesStore(), claimedStore ?? new ClaimedProfilesStore());
        Connections = new FoundationConnectionRegistry()
            .Add<IMiniMaxActionContext>(Context)
            .Add<IMiniMaxBodyContext>(Context)
            .Add<IMiniMaxBottomTextContext>(Context);
        Icon? icon = null;
        using (var stream = typeof(MiniMaxComposition).Assembly.GetManifestResourceStream("MiniMax.ico"))
            if (stream != null) icon = new Icon(stream);
        Rectangle? bounds = Context.Layout.MainBounds.Width >= 400 && Context.Layout.MainBounds.Height >= 300
            ? new Rectangle(Context.Layout.MainBounds.X, Context.Layout.MainBounds.Y,
                Context.Layout.MainBounds.Width, Context.Layout.MainBounds.Height) : null;
        WindowOptions = new("MiniMax", "MiniMax", new Size(820, 480), new Size(980, 640), bounds, icon,
            SaveBounds: rectangle =>
            {
                Context.Layout.MainBounds = new WindowLayoutStore.BoundsData
                { X = rectangle.X, Y = rectangle.Y, Width = rectangle.Width, Height = rectangle.Height };
                Context.Layout.Save();
            },
            InitializationFailed: ex => Context.Text.Status("MiniMax initialization failed: " + ex.Message));
    }
}
