using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace App;

sealed class App : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();
        base.OnFrameworkInitializationCompleted();
    }
}

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        // 창 앱이라 콘솔이 없다 — 죽으면 실행 파일 옆 crash.txt
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "crash.txt"), $"{DateTime.Now:HH:mm:ss} {e.ExceptionObject}\n");
        return AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
    }
}
