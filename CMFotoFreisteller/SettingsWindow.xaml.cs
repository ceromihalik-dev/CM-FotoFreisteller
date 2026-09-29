using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CMFotoFreisteller;

public sealed class AppSettings
{
    public int BackgroundMode { get; set; } = 0;
    public string BackgroundColor { get; set; } = "#FFFFFF";
    public bool MoveOriginal { get; set; } = true;
    public bool OpenOutput { get; set; } = false;
    public bool AutoRotate { get; set; } = true;
    public bool Optimize { get; set; } = true;
    public int SizePreset { get; set; } = 2; // Standard 600x600
    public int CustomWidth { get; set; } = 600;
    public int CustomHeight { get; set; } = 600;
    public int MarginPercent { get; set; } = 8;
    public bool FitToCanvas { get; set; } = true;
    public int ObjectMode { get; set; } = 1; // Hauptobjekt priorisieren
    public int OverflowMode { get; set; } = 0; // Vollständig einpassen
    public int QualityMode { get; set; } = 1; // Standard
    public int MaskCleanup { get; set; } = 1; // Normal

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CM FotoFreisteller", "settings.json");

    public static AppSettings Load()
    {
        try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new() : new(); }
        catch { return new(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}));
    }
}

public partial class SettingsWindow : Window
{
    public AppSettings Settings { get; private set; }
    public SettingsWindow(AppSettings current)
    {
        InitializeComponent(); Settings=current;
        BackgroundCombo.SelectedIndex=current.BackgroundMode;
        HexBox.Text=current.BackgroundColor;
        MoveOriginalCheck.IsChecked=current.MoveOriginal;
        OpenOutputCheck.IsChecked=current.OpenOutput;
        AutoRotateCheck.IsChecked=current.AutoRotate;
        OptimizeCheck.IsChecked=current.Optimize;
        SizeCombo.SelectedIndex=current.SizePreset;
        CustomWidthBox.Text=current.CustomWidth.ToString();
        CustomHeightBox.Text=current.CustomHeight.ToString();
        MarginBox.Text=current.MarginPercent.ToString();
        FitToCanvasCheck.IsChecked=current.FitToCanvas;
        ObjectModeCombo.SelectedIndex=current.ObjectMode;
        OverflowModeCombo.SelectedIndex=current.OverflowMode;
        QualityModeCombo.SelectedIndex=current.QualityMode;
        MaskCleanupCombo.SelectedIndex=current.MaskCleanup;
        UpdateSizeFields();
        UpdateColor();
    }
    private void UpdateColor()
    {
        try { ColorPreview.Background=(System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(HexBox.Text)!; } catch {}
        ColorButton.IsEnabled = BackgroundCombo.SelectedIndex == 2;
        HexBox.IsEnabled = BackgroundCombo.SelectedIndex == 2;
        InlineColorPanel.Visibility = BackgroundCombo.SelectedIndex == 2
            ? Visibility.Visible : Visibility.Collapsed;
    }
    private void BackgroundCombo_SelectionChanged(object s, SelectionChangedEventArgs e)=>UpdateColor();
    private void SizeCombo_SelectionChanged(object s, SelectionChangedEventArgs e)=>UpdateSizeFields();
    private void UpdateSizeFields()
    {
        if(CustomSizePanel == null || SizeCombo == null) return;
        CustomSizePanel.Visibility = SizeCombo.SelectedIndex == 7 ? Visibility.Visible : Visibility.Collapsed;
    }
    private void HexBox_TextChanged(object s, TextChangedEventArgs e)=>UpdateColor();
    private void Color_Click(object s, RoutedEventArgs e)
    {
        var d = new ColorPickerWindow(HexBox.Text) { Owner = this };
        if (d.ShowDialog() == true)
            HexBox.Text = d.SelectedHex;
    }
    private void Defaults_Click(object s,RoutedEventArgs e)
    {
        BackgroundCombo.SelectedIndex=0; HexBox.Text="#FFFFFF";
        MoveOriginalCheck.IsChecked=true; OpenOutputCheck.IsChecked=false;
        AutoRotateCheck.IsChecked=true; OptimizeCheck.IsChecked=true;
        SizeCombo.SelectedIndex=2; CustomWidthBox.Text="600"; CustomHeightBox.Text="600";
        MarginBox.Text="8"; FitToCanvasCheck.IsChecked=true;
        ObjectModeCombo.SelectedIndex=1;
        OverflowModeCombo.SelectedIndex=0;
        QualityModeCombo.SelectedIndex=1;
        MaskCleanupCombo.SelectedIndex=1;
    }
    private void Save_Click(object s,RoutedEventArgs e)
    {
        Settings=new AppSettings{
            BackgroundMode=BackgroundCombo.SelectedIndex,
            BackgroundColor=HexBox.Text,
            MoveOriginal=MoveOriginalCheck.IsChecked==true,
            OpenOutput=OpenOutputCheck.IsChecked==true,
            AutoRotate=AutoRotateCheck.IsChecked==true,
            Optimize=OptimizeCheck.IsChecked==true,
            SizePreset=SizeCombo.SelectedIndex,
            CustomWidth=int.TryParse(CustomWidthBox.Text,out var cw)?Math.Max(1,cw):600,
            CustomHeight=int.TryParse(CustomHeightBox.Text,out var ch)?Math.Max(1,ch):600,
            MarginPercent=int.TryParse(MarginBox.Text,out var mp)?Math.Clamp(mp,0,40):5,
            FitToCanvas=FitToCanvasCheck.IsChecked==true,
            ObjectMode=ObjectModeCombo.SelectedIndex,
            OverflowMode=OverflowModeCombo.SelectedIndex,
            QualityMode=QualityModeCombo.SelectedIndex,
            MaskCleanup=MaskCleanupCombo.SelectedIndex};
        Settings.Save(); DialogResult=true;
    }
    private void Cancel_Click(object s,RoutedEventArgs e)=>DialogResult=false;
}