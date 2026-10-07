using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Exchoribur.App.Controls;
using Exchoribur.Core.Models;

namespace Exchoribur.App.ViewModels;

/// <summary>
/// 编辑面板里的颜色。上面几组(常用色块、色块选择器、Hex、RGB 滑块)都是八位空间的
/// 挑选工具;下面那组四位值才是真正写进帧、发给灯的东西,两边双向联动。
/// </summary>
/// <remarks>
/// 数值框绑的是字符串而不是数字:直接绑数字时,用户把框清空或者打到一半,
/// 绑定转换就会失败报错。换成字符串以后,解析不了的输入先放着不管,
/// 用户接着打完就是了。
/// </remarks>
public partial class ColorEditorViewModel : ObservableObject
{
    private const double MaxEightBit = 255;
    private const double MaxFourBit = 15;

    /// <summary>防重入:改一个值会顺手改另外几个,不能让它们再回头改一遍。</summary>
    private bool _syncing;

    // ---- 八位分量:滑条用数值,输入框用文字 ----

    [ObservableProperty]
    public partial double Red { get; set; } = MaxEightBit;

    [ObservableProperty]
    public partial string RedText { get; set; }

    [ObservableProperty]
    public partial double Green { get; set; } = MaxEightBit;

    [ObservableProperty]
    public partial string GreenText { get; set; }

    [ObservableProperty]
    public partial double Blue { get; set; } = MaxEightBit;

    [ObservableProperty]
    public partial string BlueText { get; set; }

    // ---- 色相那一组,给色块选择器用 ----

    /// <summary>色相 0-360。</summary>
    [ObservableProperty]
    public partial double Hue { get; set; }

    /// <summary>饱和度 0-1。</summary>
    [ObservableProperty]
    public partial double Saturation { get; set; }

    /// <summary>明度 0-1。</summary>
    [ObservableProperty]
    public partial double Value { get; set; } = 1;

    // ---- 四位输出:真正写进帧的值 ----

    [ObservableProperty]
    public partial double FourBitRed { get; set; } = MaxFourBit;

    [ObservableProperty]
    public partial string FourBitRedText { get; set; }

    [ObservableProperty]
    public partial double FourBitGreen { get; set; } = MaxFourBit;

    [ObservableProperty]
    public partial string FourBitGreenText { get; set; }

    [ObservableProperty]
    public partial double FourBitBlue { get; set; } = MaxFourBit;

    [ObservableProperty]
    public partial string FourBitBlueText { get; set; }

    // ---- 十六进制输入框 ----

    /// <summary>六位十六进制,不带 #,字母一律大写。</summary>
    [ObservableProperty]
    public partial string HexText { get; set; }

    public ColorEditorViewModel()
    {
        HexText = "FFFFFF";
        RedText = "255";
        GreenText = "255";
        BlueText = "255";
        FourBitRedText = "15";
        FourBitGreenText = "15";
        FourBitBlueText = "15";

        SyncFromComponents();
    }

    /// <summary>八位颜色,给上面那块预览用。</summary>
    public IBrush PreviewBrush => new SolidColorBrush(
        Color.FromRgb(ToByte(Red), ToByte(Green), ToByte(Blue)));

    /// <summary>四位颜色,给下面那块预览用:这才是灯会亮的颜色。</summary>
    public IBrush OutputBrush => new SolidColorBrush(Color.FromRgb(
        (byte)ColorMath.FromFourBit(FourBitRed),
        (byte)ColorMath.FromFourBit(FourBitGreen),
        (byte)ColorMath.FromFourBit(FourBitBlue)));

    /// <summary>真正写进帧的颜色。</summary>
    public LightColor OutputColor => new(
        (byte)Math.Clamp(Math.Round(FourBitRed), 0, MaxFourBit),
        (byte)Math.Clamp(Math.Round(FourBitGreen), 0, MaxFourBit),
        (byte)Math.Clamp(Math.Round(FourBitBlue), 0, MaxFourBit));

    /// <summary>点常用色块:换成那个颜色。</summary>
    [RelayCommand]
    private void SetPreset(string? hex) => HexText = NormalizeHex(hex);

    // ---- 滑条动了 ----

    partial void OnRedChanged(double value) => SyncComponents(value, MaxEightBit, snapped => Red = snapped);

    partial void OnGreenChanged(double value) => SyncComponents(value, MaxEightBit, snapped => Green = snapped);

    partial void OnBlueChanged(double value) => SyncComponents(value, MaxEightBit, snapped => Blue = snapped);

    partial void OnFourBitRedChanged(double value) => SyncComponents(value, MaxFourBit, snapped => FourBitRed = snapped);

    partial void OnFourBitGreenChanged(double value) => SyncComponents(value, MaxFourBit, snapped => FourBitGreen = snapped);

    partial void OnFourBitBlueChanged(double value) => SyncComponents(value, MaxFourBit, snapped => FourBitBlue = snapped);

    // ---- 输入框里的文字改了 ----

    partial void OnRedTextChanged(string value) => ReadText(value, MaxEightBit, parsed => Red = parsed);

    partial void OnGreenTextChanged(string value) => ReadText(value, MaxEightBit, parsed => Green = parsed);

    partial void OnBlueTextChanged(string value) => ReadText(value, MaxEightBit, parsed => Blue = parsed);

    partial void OnFourBitRedTextChanged(string value) => ReadText(value, MaxFourBit, parsed => FourBitRed = parsed);

    partial void OnFourBitGreenTextChanged(string value) => ReadText(value, MaxFourBit, parsed => FourBitGreen = parsed);

    partial void OnFourBitBlueTextChanged(string value) => ReadText(value, MaxFourBit, parsed => FourBitBlue = parsed);

    // ---- 色相那一组 ----

    partial void OnHueChanged(double value) => SyncFromHsv();

    partial void OnSaturationChanged(double value) => SyncFromHsv();

    partial void OnValueChanged(double value) => SyncFromHsv();

    partial void OnHexTextChanged(string value) => SyncFromHex();

    /// <summary>
    /// 一个数值改了:滑条是连续的,但颜色只认整数,所以先把小数抹掉再往下传。
    /// 抹完之后重新赋值,会再进来一次,那次数值是整的就直接同步。
    /// </summary>
    private void SyncComponents(double value, double max, Action<double> resnap)
    {
        if (_syncing)
        {
            return;
        }

        var snapped = Math.Clamp(Math.Round(value), 0, max);
        if (snapped != value)
        {
            resnap(snapped);
            return;
        }

        if (max == MaxFourBit)
        {
            SyncFromFourBit();
        }
        else
        {
            SyncFromComponents();
        }
    }

    /// <summary>框里的文字变了:能读成数字就跟着走,空着或者打了一半就先不管。</summary>
    private void ReadText(string? text, double max, Action<double> apply)
    {
        if (_syncing
            || !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return;
        }

        apply(Math.Clamp(Math.Round(parsed), 0, max));
    }

    /// <summary>八位分量动了:更新色相那一组、四位值和 Hex。</summary>
    private void SyncFromComponents()
    {
        if (_syncing)
        {
            return;
        }

        _syncing = true;

        (Hue, Saturation, Value) = ColorMath.ToHsv(Red, Green, Blue);
        UpdateFourBit();
        UpdateComponentText();
        HexText = FormatHex(Red, Green, Blue);

        _syncing = false;
        NotifyDerived();
    }

    /// <summary>
    /// 色相、饱和度、明度动了:换算回八位分量,再更新四位值和 Hex。
    /// 这里不反过来重算 HSV——不然把明度拉到 0 的时候色相会被抹成 0,滑条会跳。
    /// </summary>
    private void SyncFromHsv()
    {
        if (_syncing)
        {
            return;
        }

        _syncing = true;

        (Red, Green, Blue) = ColorMath.ToRgb(Hue, Saturation, Value);
        UpdateFourBit();
        UpdateComponentText();
        HexText = FormatHex(Red, Green, Blue);

        _syncing = false;
        NotifyDerived();
    }

    /// <summary>直接改四位值:换算回八位分量,色相那一组也跟着走。</summary>
    private void SyncFromFourBit()
    {
        if (_syncing)
        {
            return;
        }

        _syncing = true;

        Red = ColorMath.FromFourBit(FourBitRed);
        Green = ColorMath.FromFourBit(FourBitGreen);
        Blue = ColorMath.FromFourBit(FourBitBlue);
        (Hue, Saturation, Value) = ColorMath.ToHsv(Red, Green, Blue);
        UpdateComponentText();
        HexText = FormatHex(Red, Green, Blue);

        _syncing = false;
        NotifyDerived();
    }

    /// <summary>
    /// 改 Hex:字母先统一成大写(长度不变,不会把光标挪走),
    /// 六个字符都齐了才解析;打了一半(比如 C6)就原样留着,不打断输入。
    /// </summary>
    private void SyncFromHex()
    {
        if (_syncing)
        {
            return;
        }

        var normalized = NormalizeHex(HexText);
        if (!string.Equals(normalized, HexText, StringComparison.Ordinal))
        {
            HexText = normalized; // 会再进来一次,那次再做下面的同步
            return;
        }

        if (!TryParseHex(HexText, out var red, out var green, out var blue))
        {
            return;
        }

        _syncing = true;

        Red = red;
        Green = green;
        Blue = blue;
        (Hue, Saturation, Value) = ColorMath.ToHsv(red, green, blue);
        UpdateFourBit();
        UpdateComponentText();

        _syncing = false;
        NotifyDerived();
    }

    private void UpdateFourBit()
    {
        FourBitRed = ColorMath.ToFourBit(Red);
        FourBitGreen = ColorMath.ToFourBit(Green);
        FourBitBlue = ColorMath.ToFourBit(Blue);

        FourBitRedText = FormatNumber(FourBitRed);
        FourBitGreenText = FormatNumber(FourBitGreen);
        FourBitBlueText = FormatNumber(FourBitBlue);
    }

    private void UpdateComponentText()
    {
        RedText = FormatNumber(Red);
        GreenText = FormatNumber(Green);
        BlueText = FormatNumber(Blue);
    }

    /// <summary>预览色块和输出颜色都是算出来的,值一变就要通知界面。</summary>
    private void NotifyDerived()
    {
        OnPropertyChanged(nameof(PreviewBrush));
        OnPropertyChanged(nameof(OutputBrush));
        OnPropertyChanged(nameof(OutputColor));
    }

    /// <summary>整数就写整数,别在框里显示 175.0。</summary>
    private static string FormatNumber(double value)
        => Math.Round(value).ToString(CultureInfo.InvariantCulture);

    /// <summary>框里只放六个字符:井号在框外面当标签,字母统一大写。</summary>
    private static string FormatHex(double red, double green, double blue)
        => $"{ToByte(red):X2}{ToByte(green):X2}{ToByte(blue):X2}";

    /// <summary>去掉井号、去掉首尾空白、统一大写。</summary>
    private static string NormalizeHex(string? text)
        => (text ?? string.Empty).Trim().TrimStart('#').ToUpperInvariant();

    /// <summary>认 RRGGBB 和 #RRGGBB 两种写法(粘贴时可能带井号),别的都当作还没输完。</summary>
    private static bool TryParseHex(string? text, out double red, out double green, out double blue)
    {
        red = 0;
        green = 0;
        blue = 0;

        var digits = NormalizeHex(text);
        if (digits.Length != 6)
        {
            return false;
        }

        if (!byte.TryParse(digits.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
            || !byte.TryParse(digits.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
            || !byte.TryParse(digits.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return false;
        }

        red = r;
        green = g;
        blue = b;
        return true;
    }

    private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);
}
