using System.Diagnostics;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Voyage.EarthquakeWarning.Services;

namespace Voyage.EarthquakeWarning.UI;

public partial class ErrorWindow : Avalonia.Controls.Window
{
    private static ErrorWindow? _current;
    private ErrorReport? _report;

    public ErrorWindow()
    {
        InitializeComponent();

        IssuesButton.Click += async (_, _) => await OpenIssuesAsync();
        LogFolderButton.Click += (_, _) => OpenLogFolder();
        CloseButton.Click += (_, _) => CloseReport();
        RestartButton.Click += (_, _) => RestartSoftware();
    }

    public static void ShowReport(ErrorReport report)
    {
        if (_current is null)
        {
            _current = new ErrorWindow();
            _current.Closed += (_, _) => _current = null;
            _current.Show();
        }

        _current.SetReport(report);
        _current.Activate();
    }

    private void SetReport(ErrorReport report)
    {
        _report = report;

        FeedbackText.Text = ErrorReporter.FeedbackText(report.LogFile);

        DescriptionText.Inlines = new InlineCollection
        {
            new Run { Text = report.Fatal ? ErrorReporter.FatalText : ErrorReporter.NonFatalText },
            new Run
            {
                Text = report.Fatal ? ErrorReporter.FatalEmphasisText : ErrorReporter.NonFatalEmphasisText,
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(Color.Parse("#FFFFFF"))
            }
        };

        TypeText.Text = report.Type;
        ContentText.Text = report.Detail;

        LocationText.Text = report.Location;
        LocationSection.IsVisible = report.Location.Length > 0;
        CloseButton.IsVisible = !report.Fatal;
    }

    private void CloseReport()
    {
        if (_report is not null) ErrorReporter.Suppress(_report);

        Close();
    }

    private void OnHeaderPressed(object? sender, PointerPressedEventArgs e) { if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e); }

    private async Task OpenIssuesAsync()
    {
        try
        {
            var launcher = Avalonia.Controls.TopLevel.GetTopLevel(this)?.Launcher;

            if (launcher is not null) await launcher.LaunchUriAsync(new Uri(ErrorReporter.IssuesUrl));
        }
        catch { }
    }

    private void OpenLogFolder()
    {
        try
        {
            var directory = ErrorReporter.LogFolder;

            Directory.CreateDirectory(directory);
            Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
        }
        catch { }
    }

    private void RestartSoftware()
    {
        try { ClassIsland.Core.AppBase.Current?.Restart(); }
        catch { }
    }
}
