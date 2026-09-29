using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace CMFotoFreisteller;

public partial class PreviewWindow : Window
{
    private readonly List<string> _files;
    private int _index;

    public PreviewWindow(IEnumerable<string> files, int startIndex = 0)
    {
        InitializeComponent();
        _files = files.Where(File.Exists).ToList();
        _index = Math.Clamp(startIndex, 0, Math.Max(0, _files.Count - 1));
        ShowCurrent();
    }

    private static BitmapImage LoadImage(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path);
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static string? FindProcessed(string source)
    {
        var folder = Path.GetDirectoryName(source)!;
        var output = Path.Combine(folder, "Bearbeitete Fotos");
        var same = Path.Combine(output, Path.GetFileName(source));
        if (File.Exists(same)) return same;
        var png = Path.Combine(output, Path.GetFileNameWithoutExtension(source) + ".png");
        return File.Exists(png) ? png : null;
    }

    private void ShowCurrent()
    {
        if (_files.Count == 0)
        {
            FileNameText.Text = "Keine Bilder";
            CounterText.Text = "0 / 0";
            return;
        }

        var source = _files[_index];
        FileNameText.Text = Path.GetFileName(source);
        CounterText.Text = $"{_index + 1} / {_files.Count}";

        try { OriginalImage.Source = LoadImage(source); }
        catch { OriginalImage.Source = null; }

        var processed = FindProcessed(source);
        if (processed != null)
        {
            try
            {
                ProcessedImage.Source = LoadImage(processed);
                NoResultText.Visibility = Visibility.Collapsed;
                StatusText.Text = "Bearbeitetes Bild gefunden.";
            }
            catch
            {
                ProcessedImage.Source = null;
                NoResultText.Visibility = Visibility.Visible;
                StatusText.Text = "Bearbeitetes Bild konnte nicht geladen werden.";
            }
        }
        else
        {
            ProcessedImage.Source = null;
            NoResultText.Visibility = Visibility.Visible;
            StatusText.Text = "Noch nicht bearbeitet.";
        }
    }

    private void Previous_Click(object sender, RoutedEventArgs e)
    {
        if (_files.Count == 0) return;
        _index = (_index - 1 + _files.Count) % _files.Count;
        ShowCurrent();
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_files.Count == 0) return;
        _index = (_index + 1) % _files.Count;
        ShowCurrent();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
