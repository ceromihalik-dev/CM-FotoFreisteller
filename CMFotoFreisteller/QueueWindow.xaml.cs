using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CMFotoFreisteller;

public sealed class QueueItem
{
    public required string Path { get; init; }
    public string Name => System.IO.Path.GetFileName(Path);
    public BitmapImage? Thumbnail { get; init; }
    public required string Status { get; init; }
    public Brush StatusBackground =>
        Status == "Fertig" ? new SolidColorBrush(Color.FromRgb(220,252,231)) :
        Status == "Fehler" ? new SolidColorBrush(Color.FromRgb(254,226,226)) :
        new SolidColorBrush(Color.FromRgb(238,242,246));
}

public partial class QueueWindow : Window
{
    private readonly List<string> _files;

    public QueueWindow(IEnumerable<string> files)
    {
        InitializeComponent();
        _files = files.Where(File.Exists).ToList();
        RefreshItems();
    }

    private static BitmapImage? Thumb(string path)
    {
        try
        {
            var b = new BitmapImage();
            b.BeginInit();
            b.CacheOption = BitmapCacheOption.OnLoad;
            b.DecodePixelWidth = 120;
            b.UriSource = new Uri(path);
            b.EndInit();
            b.Freeze();
            return b;
        }
        catch { return null; }
    }

    private static string StatusFor(string source)
    {
        string folder = System.IO.Path.GetDirectoryName(source)!;
        string outDir = System.IO.Path.Combine(folder, "Bearbeitete Fotos");
        string same = System.IO.Path.Combine(outDir, System.IO.Path.GetFileName(source));
        string png = System.IO.Path.Combine(outDir, System.IO.Path.GetFileNameWithoutExtension(source) + ".png");
        return File.Exists(same) || File.Exists(png) ? "Fertig" : "Wartet";
    }

    private void RefreshItems()
    {
        var items = _files.Select(f => new QueueItem
        {
            Path = f,
            Thumbnail = Thumb(f),
            Status = StatusFor(f)
        }).ToList();

        QueueList.ItemsSource = items;
        int done = items.Count(x => x.Status == "Fertig");
        SummaryText.Text = $"{items.Count} Bilder · {done} fertig · {items.Count-done} wartend";
    }

    private void QueueList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (QueueList.SelectedItem is not QueueItem item) return;
        int index = _files.FindIndex(x => string.Equals(x, item.Path, StringComparison.OrdinalIgnoreCase));
        var p = new PreviewWindow(_files, Math.Max(0,index)) { Owner = this };
        p.ShowDialog();
        RefreshItems();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
