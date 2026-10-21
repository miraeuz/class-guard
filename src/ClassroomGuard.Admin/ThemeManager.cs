using System.IO;
using System.Windows;
using System.Windows.Media;
namespace ClassroomGuard.Admin;
internal static class ThemeManager
{
    public static bool Dark { get; private set; }
    private static string FilePath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ClassroomGuard","Admin-theme.txt");
    private static readonly Dictionary<string,(string Light,string Dark)> Colors=new()
    {
        ["AccentText"]=("#6D3CE8","#C8ACFF"),
        ["BgApp"]=("#F5F3FC","#10121E"),
        ["BgSidebar"]=("#FFFFFF","#171A2A"),
        ["BgCard"]=("#FFFFFF","#1C2033"),
        ["BgInput"]=("#FAF9FE","#252A40"),
        ["BgHover"]=("#EEF2FF","#30364F"),
        ["BgNavActive"]=("#EEE7FF","#352957"),
        ["Border"]=("#E5DFF2","#414962"),
        ["BorderFocus"]=("#7C3AED","#B39AFF"),
        ["Text"]=("#211A38","#F3F2FA"),
        ["TextSecondary"]=("#514763","#D1CEE1"),
        ["TextMuted"]=("#6B607C","#B0ACC5"),
        ["Accent"]=("#6D3CE8","#7540D8"),
        ["AccentHover"]=("#5427BD","#6935C8"),
        ["AccentSoft"]=("#EEE7FF","#352957"),
        ["Success"]=("#047857","#68E0BA"),
        ["SuccessSoft"]=("#D1FAE5","#163E39"),
        ["Warning"]=("#995A07","#FFD080"),
        ["WarningSoft"]=("#FEF3C7","#493922"),
        ["Danger"]=("#DC2626","#FFA5B3"),
        ["DangerSoft"]=("#FEE2E2","#492A38"),
        ["HeaderBg"]=("#26194A","#26194A"),
        ["HeaderText"]=("#F1F5F9","#F1F5F9"),
        ["HeaderTextMuted"]=("#D6C8EE","#D6C8EE"),
        ["TassleAccent"]=("#38BDF8","#38BDF8")
    };
    public static void Load() { try { Dark=File.ReadAllText(FilePath).Trim()=="dark"; } catch { Dark=false; } Apply(); }
    public static void Toggle() { Dark=!Dark;Apply();Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);File.WriteAllText(FilePath,Dark?"dark":"light"); }
    private static void Apply()
    {
        foreach(var (key,pair) in Colors)Application.Current.Resources[key]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(Dark?pair.Dark:pair.Light));
        Application.Current.Resources["HeroGradient"]=new LinearGradientBrush((Color)ColorConverter.ConvertFromString(Dark?"#302747":"#EEE5FF"),(Color)ColorConverter.ConvertFromString(Dark?"#193737":"#E0F5F5"),45);
    }
}
