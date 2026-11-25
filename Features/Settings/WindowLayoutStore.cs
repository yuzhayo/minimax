using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using System.Drawing;
using AppFoundation.Shared.UI;

namespace ChromeProfileLauncher.Services;

/// <summary>
/// Remembers how the user left the MiniMax windows: their size and position,
/// and the width of every table column. Saved beside the other app state in
/// LocalAppData, like the tick and claim files.
/// </summary>
internal sealed class WindowLayoutStore
{
    private readonly string _path;

    public BoundsData MainBounds { get; set; } = new();
    public BoundsData ProfileBounds { get; set; } = new();
    public BoundsData SettingsBounds { get; set; } = new();
    public int[] MainColumns { get; set; } = Array.Empty<int>();
    public int[] ProfileColumns { get; set; } = Array.Empty<int>();

    public WindowLayoutStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ChromeProfileLauncher",
            "layout.json");
    }

    public void Load()
    {
        try
        {
            if (!File.Exists(_path))
                return;

            var data = JsonSerializer.Deserialize<LayoutData>(
                File.ReadAllText(_path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (data == null)
                return;

            MainBounds = data.MainBounds ?? new BoundsData();
            ProfileBounds = data.ProfileBounds ?? new BoundsData();
            SettingsBounds = data.SettingsBounds ?? new BoundsData();
            MainColumns = data.MainColumns ?? Array.Empty<int>();
            ProfileColumns = data.ProfileColumns ?? Array.Empty<int>();
        }
        catch (Exception)
        {
            // A bad layout file must never stop the app from starting.
        }
    }

    public void Save()
    {
        try
        {
            var folder = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            var data = new LayoutData
            {
                MainBounds = MainBounds,
                ProfileBounds = ProfileBounds,
                SettingsBounds = SettingsBounds,
                MainColumns = MainColumns,
                ProfileColumns = ProfileColumns
            };
            File.WriteAllText(_path, JsonSerializer.Serialize(data,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception)
        {
            // Saving the layout is a convenience, never a reason to interrupt.
        }
    }

    /// <summary>Restores a saved size only when it is sane; otherwise leaves defaults.</summary>
    public static void ApplyBounds(Form form, BoundsData bounds)
    {
        if (bounds == null) return;
        SharedWindowLayout.ApplyBounds(form, new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height));
    }

    public static BoundsData ReadBounds(Form form)
    {
        var bounds = SharedWindowLayout.ReadBounds(form);
        return new BoundsData
        {
            X = bounds.X,
            Y = bounds.Y,
            Width = bounds.Width,
            Height = bounds.Height
        };
    }

    /// <summary>Restores column widths, skipping Fill columns that size themselves.</summary>
    public static void ApplyColumns(DataGridView grid, int[] widths)
    {
        SharedWindowLayout.ApplyColumns(grid, widths);
    }

    public static int[] ReadColumns(DataGridView grid)
    {
        return SharedWindowLayout.ReadColumns(grid);
    }

    internal sealed class BoundsData
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    private sealed class LayoutData
    {
        public BoundsData? MainBounds { get; set; }
        public BoundsData? ProfileBounds { get; set; }
        public BoundsData? SettingsBounds { get; set; }
        public int[]? MainColumns { get; set; }
        public int[]? ProfileColumns { get; set; }
    }
}
