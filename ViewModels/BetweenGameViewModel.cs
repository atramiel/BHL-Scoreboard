using CommunityToolkit.Mvvm.ComponentModel;
using QRCoder;
using System.IO;
using System.Windows.Media.Imaging;

namespace Scoreboard.ViewModels;

public class BetweenGameViewModel : ObservableObject
{
    private Timer? _timer;

    private BitmapSource? _bracketQRCode;
    public BitmapSource? BracketQRCode
    {
        get => _bracketQRCode;
        set => SetProperty(ref _bracketQRCode, value);
    }

    private BitmapSource? _learnMoreQRCode;
    public BitmapSource? LearnMoreQRCode
    {
        get => _learnMoreQRCode;
        set => SetProperty(ref _learnMoreQRCode, value);
    }

    private TimeSpan _nextMatchTime = TimeSpan.FromMinutes(5);
    public TimeSpan NextMatchTime
    {
        get => _nextMatchTime;
        set
        {
            SetProperty(ref _nextMatchTime, value);
            OnPropertyChanged(nameof(StartsAtDisplay));
        }
    }

    public string StartsAtDisplay => $"Starts at {(DateTime.Now + NextMatchTime):h:mm tt}";

    private bool _isCountingDown;
    public bool IsCountingDown
    {
        get => _isCountingDown;
        set => SetProperty(ref _isCountingDown, value);
    }

    public event EventHandler? CountdownComplete;

    public BetweenGameViewModel(string? bracketUrl, string? learnMoreUrl)
    {
        if (!string.IsNullOrWhiteSpace(bracketUrl))
            BracketQRCode = GenerateQR(bracketUrl);
        if (!string.IsNullOrWhiteSpace(learnMoreUrl))
            LearnMoreQRCode = GenerateQR(learnMoreUrl);
    }

    private static BitmapSource? GenerateQR(string url)
    {
        try
        {
            var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(qrCodeData);
            var pngBytes = qrCode.GetGraphic(10);

            using var ms = new MemoryStream(pngBytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = ms;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public void Adjust(int deltaMinutes)
    {
        var newTime = NextMatchTime + TimeSpan.FromMinutes(deltaMinutes);
        if (newTime < TimeSpan.FromMinutes(1)) newTime = TimeSpan.FromMinutes(1);
        if (newTime > TimeSpan.FromMinutes(99)) newTime = TimeSpan.FromMinutes(99);
        NextMatchTime = newTime;
    }

    public void StartCountdown()
    {
        if (IsCountingDown) return;
        IsCountingDown = true;
        _timer = new Timer(Tick, null, 1000, 1000);
    }

    private void Tick(object? state)
    {
        if (NextMatchTime <= TimeSpan.Zero)
        {
            _timer?.Dispose();
            _timer = null;
            IsCountingDown = false;
            CountdownComplete?.Invoke(this, EventArgs.Empty);
            return;
        }
        NextMatchTime -= TimeSpan.FromSeconds(1);
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
