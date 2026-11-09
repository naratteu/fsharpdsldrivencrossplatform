using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Dsl;
using Microsoft.Web.WebView2.Core;

namespace App;

/// 상단 바(생성된 부품) + 탭마다 웹뷰. 런처 메시지 · 앱 기능(계약)은 여기서 구현한다
sealed class MainWindow : Window, ILauncherHandler, ICapabilities
{
    static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FSharpDslDrivenCrossPlatform");
    static readonly string ServerFile = Path.Combine(DataDir, "server.txt");

    readonly StackPanel _tabStrip = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    readonly BarButton _newTab = new() { Glyph = "+", On = true };
    readonly BarButton _reload = new() { Glyph = "↻" };
    readonly ScannerChip _chip = new();
    readonly Panel _content = new();
    readonly NativeWebView _launcher = new();
    readonly Dictionary<string, NativeWebView> _webs = [];
    readonly List<string> _tabs = [];
    string? _active;
    string _server;
    int _scanners;
    /// 카메라 화면을 띄운 요청 — 런처가 카메라를 보여 주는 중
    TaskCompletionSource<string?>? _photo;

    public MainWindow()
    {
        Title = "fsharpdsldrivencrossplatform";
        Width = 1100;
        Height = 750;
        Background = new SolidColorBrush(Color.Parse(Tokens.Page));
        _server = File.Exists(ServerFile) ? File.ReadAllText(ServerFile).Trim() : Modules.DefaultServer;

        var home = new HomeMark();
        home.Clicked += (_, _) => Show(null);
        _newTab.Clicked += (_, _) => Show(null);
        _reload.Clicked += (_, _) => { if (_active is { } key) _webs[key].Refresh(); };

        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(4, 0, 0, 0), Children = { home, _tabStrip, _newTab } };
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 0, 4, 0), Children = { _chip, _reload } };
        Grid.SetColumn(right, 1);
        var bar = new Grid { Background = new SolidColorBrush(Color.Parse(Tokens.Bar)), Height = Tokens.BarHeight, ColumnDefinitions = new("*,Auto"), Children = { left, right } };
        DockPanel.SetDock(bar, Dock.Top);
        Content = new DockPanel { Children = { bar, _content } };

        // 런처만 앱과 메시지를 주고받는다 — 모듈 페이지(외부 서버)는 허락한 앱 기능만
        _content.Children.Add(_launcher);
        _launcher.WebMessageReceived += (_, e) => { if (FromLauncher.Dispatch(e.Body ?? "", this) is { } error) Log(error); };
        _launcher.AdapterCreated += (_, _) =>
        {
            // 카메라는 런처(앱 자체 페이지)에만
            if (Core(_launcher) is { } core)
                core.PermissionRequested += (_, e) => { if (e.PermissionKind == CoreWebView2PermissionKind.Camera) e.State = CoreWebView2PermissionState.Allow; };
            _launcher.Navigate(new Uri(Path.Combine(AppContext.BaseDirectory, "web", "index.html")));
        };

        UsbScanner.Start(
            data => Dispatcher.UIThread.Post(() => { if (_active is { } key) Run(_webs[key], ModuleEvents.Scan(data)); }),
            on => Dispatcher.UIThread.Post(() => { _scanners = Math.Max(0, _scanners + (on ? 1 : -1)); RenderBar(); }),
            Log);
        RenderBar();
    }

    static CoreWebView2? Core(NativeWebView web) =>
        web.TryGetPlatformHandle() is IWindowsWebView2PlatformHandle h ? CoreWebView2.CreateFromComICoreWebView2(h.CoreWebView2) : null;

    static void Run(NativeWebView web, string script) => _ = web.InvokeScript(script);

    static string Origin(string url) => new Uri(url).GetLeftPart(UriPartial.Authority);

    static void Log(string s)
    {
        try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "log.txt"), $"{DateTime.Now:HH:mm:ss.fff} {s}\n"); } catch { }
    }

    /// 탭을 보이거나(키) 런처를(null) — 카메라를 띄운 채 다른 데로 가면 취소
    void Show(string? key)
    {
        if (_photo is not null && key is not null) _photo.TrySetResult(null);
        _active = key;
        _launcher.IsVisible = key is null;
        foreach (var (k, w) in _webs) w.IsVisible = k == key;
        RenderBar();
    }

    void Close(string key)
    {
        var i = _tabs.IndexOf(key);
        _tabs.Remove(key);
        _content.Children.Remove(_webs[key]);
        _webs.Remove(key);
        if (_active == key) Show(_tabs.Count == 0 ? null : _tabs[Math.Min(i, _tabs.Count - 1)]);
        else RenderBar();
        Run(_launcher, ToLauncher.Launcher(_server, _tabs));
    }

    void RenderBar()
    {
        _tabStrip.Children.Clear();
        for (var i = 0; i < _tabs.Count; i++)
        {
            var key = _tabs[i];
            var module = Modules.Find(key)!;
            if (i > 0) _tabStrip.Children.Add(new TabDivider { Visible = _active != key && _active != _tabs[i - 1] });
            var tab = new TabHeader { Title = module.Name, Accent = module.Accent, Active = _active == key };
            tab.Clicked += (_, _) => Show(key);
            tab.CloseClicked += (_, _) => Close(key);
            _tabStrip.Children.Add(tab);
        }
        _newTab.IsVisible = Modules.All.Any(m => !_tabs.Contains(m.Key));
        _reload.On = _active is not null;
        _chip.Text = _scanners > 0 ? "스캐너 연결됨" : "스캐너 대기";
        _chip.On = _scanners > 0;
    }

    // ── 런처 → 앱 ──

    public void Ready()
    {
        Run(_launcher, ToLauncher.Hello($"Windows {RuntimeInformation.ProcessArchitecture}", !RuntimeFeature.IsDynamicCodeSupported));
        Run(_launcher, ToLauncher.Launcher(_server, _tabs));
    }

    public void OpenModule(string key)
    {
        if (_tabs.Contains(key)) { Show(key); return; }
        if (Modules.Find(key) is not { } module) { Log($"모르는 모듈 {key}"); return; }
        var url = _server.TrimEnd('/') + module.Path;
        var web = new NativeWebView { IsVisible = false };
        web.AdapterCreated += async (_, _) =>
        {
            // 이 모듈에 허락한 앱 기능(window.app) — 서버 출처에서 온 요청만 받는다
            if (Capabilities.Script(key) is { Length: > 0 } script && Core(web) is { } core)
            {
                await core.AddScriptToExecuteOnDocumentCreatedAsync(script);
                core.WebMessageReceived += async (_, e) =>
                {
                    if (Origin(e.Source) != Origin(_server)) return;
                    Run(web, await Capabilities.Handle(key, e.TryGetWebMessageAsString(), this));
                };
            }
            web.Navigate(new Uri(url));
        };
        _webs[key] = web;
        _tabs.Add(key);
        _content.Children.Add(web);
        Show(key);
        Run(_launcher, ToLauncher.Launcher(_server, _tabs));
    }

    public void SetServer(string url)
    {
        var s = url.Trim().TrimEnd('/');
        if (!s.Contains("://")) s = "http://" + s;
        if (!Uri.TryCreate(s, UriKind.Absolute, out _)) { Log($"올바른 주소가 아님 {url}"); return; }
        if (s == _server) return;
        foreach (var key in _tabs.ToList()) { _content.Children.Remove(_webs[key]); _webs.Remove(key); }
        _tabs.Clear();
        _server = s;
        try { Directory.CreateDirectory(DataDir); File.WriteAllText(ServerFile, s); } catch (Exception e) { Log($"서버 주소 저장 실패 {e.Message}"); }
        Show(null);
        Run(_launcher, ToLauncher.Launcher(_server, _tabs));
    }

    public void Photo(string data)
    {
        var photo = _photo;
        _photo = null;
        photo?.TrySetResult(data == "" ? null : data);
    }

    // ── 앱 기능(모듈 페이지가 부름) ──

    public async Task<string?> TakePhoto(string moduleKey)
    {
        if (_photo is not null || _active != moduleKey) return null;   // 보고 있는 탭에서 한 번에 하나만
        var photo = _photo = new TaskCompletionSource<string?>();
        _active = null;   // 런처(카메라 화면)를 보이되 탭은 그대로
        _launcher.IsVisible = true;
        foreach (var w in _webs.Values) w.IsVisible = false;
        Run(_launcher, ToLauncher.Camera());
        var result = await photo.Task;
        _photo = null;
        if (_tabs.Contains(moduleKey)) Show(moduleKey);
        return result;
    }
}
