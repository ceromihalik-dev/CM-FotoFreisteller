using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CMFotoFreisteller;
public partial class ColorPickerWindow : Window
{
    private bool _updating;
    private double _h, _s=0, _v=1;
    public string SelectedHex { get; private set; }

    public ColorPickerWindow(string initial)
    {
        InitializeComponent();
        SelectedHex=Normalize(initial) ?? "#FFFFFF";
        Loaded += (_,__) => { SetFromHex(SelectedHex); UpdateMarkers(); };
    }

    private static string? Normalize(string? s)
    {
        if(string.IsNullOrWhiteSpace(s)) return null;
        s=s.Trim(); if(!s.StartsWith("#")) s="#"+s;
        if(s.Length!=7) return null;
        try { Convert.ToInt32(s[1..],16); return s.ToUpperInvariant(); } catch { return null; }
    }

    private static Color Hsv(double h,double s,double v)
    {
        double c=v*s, x=c*(1-Math.Abs((h/60)%2-1)), m=v-c;
        (double r, double g, double b) = h switch
        {
            < 60  => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _     => (c, 0.0, x)
        };
        return Color.FromRgb((byte)Math.Round((r+m)*255),(byte)Math.Round((g+m)*255),(byte)Math.Round((b+m)*255));
    }

    private static (double h,double s,double v) ToHsv(Color c)
    {
        double r=c.R/255.0,g=c.G/255.0,b=c.B/255.0,max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b)),d=max-min,h=0;
        if(d!=0){ if(max==r)h=60*(((g-b)/d)%6); else if(max==g)h=60*((b-r)/d+2); else h=60*((r-g)/d+4); }
        if(h<0)h+=360; return(h,max==0?0:d/max,max);
    }

    private void SetFromHex(string hex)
    {
        var c=(Color)ColorConverter.ConvertFromString(hex);
        (_h,_s,_v)=ToHsv(c); SelectedHex=hex.ToUpperInvariant(); UpdateUi(c);
    }

    private void UpdateUi(Color c)
    {
        _updating=true;
        SelectedHex=$"#{c.R:X2}{c.G:X2}{c.B:X2}";
        HexBox.Text=SelectedHex; Preview.Background=new SolidColorBrush(c);
        RgbText.Text=$"RGB  {c.R}, {c.G}, {c.B}";
        HueBase.Fill=new SolidColorBrush(Hsv(_h,1,1));
        _updating=false; UpdateMarkers();
    }

    private void UpdateMarkers()
    {
        if(SvArea.ActualWidth>0){
            SvMarker.Margin=new Thickness(Math.Clamp(_s*SvArea.ActualWidth-8,-8,SvArea.ActualWidth-8),
                Math.Clamp((1-_v)*SvArea.ActualHeight-8,-8,SvArea.ActualHeight-8),0,0);
            SvMarker.HorizontalAlignment=HorizontalAlignment.Left; SvMarker.VerticalAlignment=VerticalAlignment.Top;
        }
        if(HueArea.ActualWidth>0) HueMarker.Margin=new Thickness(Math.Clamp(_h/360*HueArea.ActualWidth-2,0,HueArea.ActualWidth-5),0,0,0);
    }

    private void SvArea_Mouse(object s, MouseEventArgs e)
    {
        if(e.LeftButton!=MouseButtonState.Pressed && e.RoutedEvent.Name!="MouseLeftButtonDown")return;
        var p=e.GetPosition(SvArea); _s=Math.Clamp(p.X/SvArea.ActualWidth,0,1); _v=1-Math.Clamp(p.Y/SvArea.ActualHeight,0,1);
        UpdateUi(Hsv(_h,_s,_v));
    }

    private void HueArea_Mouse(object s, MouseEventArgs e)
    {
        if(e.LeftButton!=MouseButtonState.Pressed && e.RoutedEvent.Name!="MouseLeftButtonDown")return;
        var p=e.GetPosition(HueArea); _h=Math.Clamp(p.X/HueArea.ActualWidth,0,1)*360;
        UpdateUi(Hsv(_h,_s,_v));
    }

    private void HexBox_TextChanged(object s, TextChangedEventArgs e)
    {
        if(_updating)return; var h=Normalize(HexBox.Text); if(h!=null)SetFromHex(h);
    }
    private void Ok_Click(object s,RoutedEventArgs e)=>DialogResult=true;
    private void Cancel_Click(object s,RoutedEventArgs e)=>DialogResult=false;
}