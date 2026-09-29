using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace CMFotoFreisteller;

public partial class MainWindow : Window
{
    private readonly List<string> _files = new();
    private string? _baseFolder;
    private bool _busy;
    private AppSettings _settings = AppSettings.Load();

    public MainWindow()
    {
        InitializeComponent();
        ApplySettings();
    }

    private void ApplySettings()
    {
        string bg = _settings.BackgroundMode switch
        {
            0 => "Weiß",
            1 => "Transparent (PNG)",
            2 => $"Eigene Farbe {_settings.BackgroundColor}",
            _ => "Weiß"
        };
        string originals = _settings.MoveOriginal
            ? "Originale nach Erfolg verschieben"
            : "Originale am Ursprungsort belassen";
        string size = _settings.SizePreset switch
        {
            0 => "256 × 256 px", 1 => "400 × 400 px", 2 => "600 × 600 px",
            3 => "800 × 800 px", 4 => "1200 × 1200 px", 5 => "1600 × 1600 px",
            6 => "Originalgröße",
            7 => $"{_settings.CustomWidth} × {_settings.CustomHeight} px",
            _ => "600 × 600 px"
        };
        string objectMode = _settings.ObjectMode switch
        {
            0 => "Gesamtobjekt", 1 => "Hauptobjekt priorisiert",
            2 => "Größte Fläche", _ => "Hauptobjekt priorisiert"
        };
        string overflow = _settings.OverflowMode switch
        {
            0 => "Alles einpassen", 1 => "Hauptobjekt groß", 2 => "Beschnitt erlaubt",
            _ => "Alles einpassen"
        };
        string quality = _settings.QualityMode switch
        {
            0 => "Schnell", 1 => "Standard", 2 => "Alpha-Matting", _ => "Standard"
        };
        string cleanup = _settings.MaskCleanup switch
        {
            0 => "Maske: Aus", 1 => "Maske: Normal", 2 => "Maske: Stark", _ => "Maske: Normal"
        };
        ActiveSettingsText.Text = $"{bg} · {size} · Rand {_settings.MarginPercent} % · {quality} · {cleanup} · {objectMode} · {overflow} · {originals}";
    }

    private static bool Supported(string p) =>
        new[] { ".jpg", ".jpeg", ".png" }.Contains(Path.GetExtension(p).ToLowerInvariant());

    private void Log(string text)
    {
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
        LogBox.ScrollToEnd();
    }

    private void SetFiles(IEnumerable<string> files)
    {
        if (_busy) return;
        _files.Clear();
        _files.AddRange(files.Where(File.Exists).Where(Supported).Distinct());
        _baseFolder = _files.Count > 0 ? Path.GetDirectoryName(_files[0]) : null;
        SelectionText.Text = _files.Count == 0 ? "Keine unterstützten Bilder gefunden."
            : $"{_files.Count} Bild(er) ausgewählt.";
        QueueText.Text = _files.Count == 0 ? "Warteschlange leer."
            : $"Warteschlange: {_files.Count} Bild(er) · Status: bereit";
        Progress.Value = 0;
        StatusText.Text = "Bereit";
        Log($"{_files.Count} Bild(er) geladen.");
    }

    private void SelectFiles_Click(object sender, RoutedEventArgs e)
    {
        var d = new OpenFileDialog { Multiselect = true, Filter = "Bilder|*.jpg;*.jpeg;*.png" };
        if (d.ShowDialog() == true) SetFiles(d.FileNames);
    }

    private void SelectFolder_Click(object sender, RoutedEventArgs e)
    {
        var d = new OpenFolderDialog { Title = "Bilderordner auswählen" };
        if (d.ShowDialog() == true)
            SetFiles(Directory.EnumerateFiles(d.FolderName).Where(Supported));
    }

    private void DropArea_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void DropArea_Drop(object sender, DragEventArgs e)
    {
        if (_busy || e.Data.GetData(DataFormats.FileDrop) is not string[] items) return;
        var all = new List<string>();
        foreach (var item in items)
        {
            if (File.Exists(item)) all.Add(item);
            else if (Directory.Exists(item))
                all.AddRange(Directory.EnumerateFiles(item).Where(Supported));
        }
        SetFiles(all);
    }

    private async Task<(bool ok, string python, string message)> EnsureEngineAsync()
    {
        string bundledPython = Path.Combine(AppContext.BaseDirectory, "Runtime", "python.exe");
        if (File.Exists(bundledPython))
        {
            Log("Gebündelte Offline-Bildengine gefunden.");
            return (true, bundledPython, "");
        }

        string appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CM FotoFreisteller");
        string venv = Path.Combine(appData, "engine");
        string python = Path.Combine(venv, "Scripts", "python.exe");

        if (File.Exists(python))
        {
            Log("Lokale Bildengine gefunden.");
            return (true, python, "");
        }

        Directory.CreateDirectory(appData);
        Log("Bildengine wird beim ersten Start eingerichtet …");

        // Use the Windows Python launcher. This also gives us a clear error if Python is absent.
        var create = await RunAsync("py", new[] { "-3", "-m", "venv", venv });
        if (create.exitCode != 0 || !File.Exists(python))
            return (false, "", "Python 3 wurde nicht gefunden oder die virtuelle Umgebung konnte nicht erstellt werden.\n\n" +
                "Bitte Python 3 installieren (Option „Add Python to PATH“ ist nicht zwingend nötig) und CM FotoFreisteller erneut starten.\n\n" +
                create.stderr);

        Log("Installiere rembg, ONNX Runtime und Pillow …");
        var install = await RunAsync(python, new[] {
            "-m", "pip", "install", "--disable-pip-version-check",
            "rembg", "onnxruntime", "pillow"
        });

        if (install.exitCode != 0)
            return (false, "", "Die Bildengine konnte nicht installiert werden.\n\n" + install.stderr);

        Log("Bildengine erfolgreich eingerichtet.");
        return (true, python, "");
    }

    private static async Task<(int exitCode, string stdout, string stderr)> RunAsync(
        string exe, IEnumerable<string> args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var p = Process.Start(psi);
            if (p is null) return (-1, "", $"Prozess konnte nicht gestartet werden: {exe}");

            Task<string> stdout = p.StandardOutput.ReadToEndAsync();
            Task<string> stderr = p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync();
            return (p.ExitCode, await stdout, await stderr);
        }
        catch (Exception ex)
        {
            return (-1, "", ex.Message);
        }
    }

    private async void Process_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (_files.Count == 0)
        {
            MessageBox.Show("Bitte zuerst Bilder oder einen Ordner auswählen.",
                "CM FotoFreisteller", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string engine = Path.Combine(AppContext.BaseDirectory, "Engine", "foto_engine.py");
        if (!File.Exists(engine))
        {
            MessageBox.Show($"Die Bildengine fehlt:\n{engine}", "CM FotoFreisteller",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _busy = true;
        LogBox.Clear();
        int ok = 0, fail = 0;

        try
        {
            var setup = await EnsureEngineAsync();
            if (!setup.ok)
            {
                Log("FEHLER: " + setup.message);
                MessageBox.Show(setup.message, "CM FotoFreisteller – Engine-Fehler",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string python = setup.python;
            Log($"Engine: {python}");

            for (int i = 0; i < _files.Count; i++)
            {
                string src = _files[i];
                string folder = Path.GetDirectoryName(src)!;
                string outDir = Path.Combine(folder, "Bearbeitete Fotos");
                string doneDir = Path.Combine(folder, "Originale abgearbeitet");
                Directory.CreateDirectory(outDir);
                Directory.CreateDirectory(doneDir);

                bool transparent = _settings.BackgroundMode == 1;
                string dst = Path.Combine(outDir, Path.GetFileName(src));
                if (transparent) dst = Path.ChangeExtension(dst, ".png");

                StatusText.Text = $"Bearbeite {i + 1}/{_files.Count}: {Path.GetFileName(src)}";
                QueueText.Text = $"Bild {i + 1} von {_files.Count} · Bearbeitung: {Path.GetFileName(src)}";
                Progress.Value = (double)i / _files.Count * 100;
                Log($"START {Path.GetFileName(src)}");

                if (File.Exists(dst))
                {
                    Log("ÜBERSPRUNGEN: Ausgabedatei existiert bereits.");
                    fail++;
                    continue;
                }

                (int tw, int th) = _settings.SizePreset switch
                {
                    0 => (256,256), 1 => (400,400), 2 => (600,600), 3 => (800,800),
                    4 => (1200,1200), 5 => (1600,1600), 6 => (0,0),
                    7 => (_settings.CustomWidth,_settings.CustomHeight), _ => (600,600)
                };
                var run = await RunAsync(python, new[] {
                    engine, src, dst, transparent ? "1" : "0",
                    _settings.BackgroundMode == 2 ? _settings.BackgroundColor : "#FFFFFF",
                    _settings.Optimize ? "1" : "0",
                    tw.ToString(), th.ToString(), _settings.MarginPercent.ToString(),
                    _settings.FitToCanvas ? "1" : "0", _settings.ObjectMode.ToString(), _settings.OverflowMode.ToString(), _settings.QualityMode.ToString(), _settings.MaskCleanup.ToString()
                });

                if (run.exitCode != 0 || !File.Exists(dst))
                {
                    fail++;
                    Log($"FEHLER {Path.GetFileName(src)}: ExitCode {run.exitCode}");
                    if (!string.IsNullOrWhiteSpace(run.stderr)) Log(run.stderr.Trim());
                    try { if (File.Exists(dst)) File.Delete(dst); } catch { }
                    continue;
                }

                // Safety: only move original after a real, non-empty result exists.
                if (new FileInfo(dst).Length == 0)
                {
                    fail++;
                    Log("FEHLER: Ausgabedatei ist leer.");
                    try { File.Delete(dst); } catch { }
                    continue;
                }

                if (_settings.MoveOriginal)
                {
                    string archive = Path.Combine(doneDir, Path.GetFileName(src));
                    if (File.Exists(archive))
                    {
                        fail++;
                        Log("FEHLER: Gleichnamiges Original existiert bereits im Archiv. Original bleibt unangetastet.");
                        try { File.Delete(dst); } catch { }
                        continue;
                    }
                    File.Move(src, archive);
                }

                ok++;
                Log($"OK {Path.GetFileName(dst)}");
                QueueText.Text = $"Bild {i + 1} von {_files.Count} · Fertig: {Path.GetFileName(dst)}";
                Progress.Value = (double)(i + 1) / _files.Count * 100;
            }
        }
        finally
        {
            Progress.Value = 100;
            StatusText.Text = $"Fertig · {ok} erfolgreich · {fail} Fehler/übersprungen";
            QueueText.Text = $"Abgeschlossen · {ok} erfolgreich · {fail} Fehler/übersprungen";
            _busy = false;
        }

        if (_settings.OpenOutput && !string.IsNullOrWhiteSpace(_baseFolder))
        {
            var outputPath = Path.Combine(_baseFolder, "Bearbeitete Fotos");
            if (Directory.Exists(outputPath))
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{outputPath}\"") { UseShellExecute = true });
        }
    }

    private void OpenOutput_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_baseFolder)) return;
        var path = Path.Combine(_baseFolder, "Bearbeitete Fotos");
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    private static void OpenWeb(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Link konnte nicht geöffnet werden.\n\n{ex.Message}",
                "CM FotoFreisteller", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Developer_Click(object sender, RoutedEventArgs e)
    {
        OpenWeb("https://github.com/ceromihalik-dev");
    }

    private void Support_Click(object sender, RoutedEventArgs e)
    {
        OpenWeb("https://www.paypal.com/donate/?hosted_button_id=Y39Q96VMSJWG2");
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "CM FotoFreisteller\nVersion 0.2.5\n\n" +
            "Lokale Stapel-Freistellung für Gerätefotos.\n\n" +
            "Entwickler: C. Mihalik\n" +
            "GitHub: ceromihalik-dev\n\n" +
            "Die Anwendung ist kostenlos. Eine Unterstützung des Projekts ist freiwillig.",
            "Über CM FotoFreisteller",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }



    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (_files.Count == 0)
        {
            MessageBox.Show("Bitte zuerst Bilder oder einen Ordner auswählen.",
                "CM FotoFreisteller", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var preview = new PreviewWindow(_files) { Owner = this };
        preview.ShowDialog();
    }



    private void Queue_Click(object sender, RoutedEventArgs e)
    {
        if (_files.Count == 0)
        {
            MessageBox.Show("Bitte zuerst Bilder oder einen Ordner auswählen.",
                "CM FotoFreisteller", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var queue = new QueueWindow(_files) { Owner = this };
        queue.ShowDialog();
    }



    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var w = new SettingsWindow(_settings) { Owner = this };
        if (w.ShowDialog() == true)
        {
            _settings = w.Settings;
            ApplySettings();
            Log($"Einstellungen gespeichert · Hintergrund: {(_settings.BackgroundMode==0 ? "Weiß" : _settings.BackgroundMode==1 ? "Transparent" : _settings.BackgroundColor)}");
        }
    }


}
