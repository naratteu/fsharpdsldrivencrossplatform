using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Views;
using Android.Webkit;
using Android.Widget;
using AndroidX.WebKit;
using Dsl;

namespace App;

/// 상단 바(생성된 부품) + 탭마다 WebView. 런처 메시지 · 앱 기능(계약)은 여기서 구현한다
[Activity(Label = "fsharpdsldrivencrossplatform", MainLauncher = true, Theme = "@android:style/Theme.Material.Light.NoActionBar",
          ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout)]
public sealed class MainActivity : Activity, ILauncherHandler, ICapabilities
{
    const string Origin = "https://appassets.androidplatform.net";
    LinearLayout _tabStrip = null!;
    BarButton _newTab = null!, _reload = null!;
    ScannerChip _chip = null!;
    FrameLayout _content = null!;
    WebView _launcher = null!;
    readonly Dictionary<string, WebView> _webs = [];
    readonly List<string> _tabs = [];
    string? _active;
    string _server = "";
    int _scanners;
    ISharedPreferences _prefs = null!;
    TaskCompletionSource<string?>? _photo;
    UsbScanner? _scanner;
    PermissionRequest? _pendingCamera;

    protected override void OnCreate(Bundle? saved)
    {
        base.OnCreate(saved);
        _prefs = GetSharedPreferences("app", FileCreationMode.Private)!;
        _server = _prefs.GetString("server", Modules.DefaultServer) ?? Modules.DefaultServer;
        var dp = Resources!.DisplayMetrics!.Density;

        var home = new HomeMark(this);
        home.Clicked += (_, _) => Show(null);
        _tabStrip = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        _tabStrip.SetGravity(GravityFlags.CenterVertical);
        _newTab = new BarButton(this) { Glyph = "+", On = true };
        _newTab.Clicked += (_, _) => Show(null);
        _reload = new BarButton(this) { Glyph = "↻" };
        _reload.Clicked += (_, _) => { if (_active is { } key) _webs[key].Reload(); };
        _chip = new ScannerChip(this);

        var bar = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        bar.SetGravity(GravityFlags.CenterVertical);
        bar.SetBackgroundColor(Color.ParseColor(Tokens.Bar));
        bar.SetPadding((int)(4 * dp), 0, (int)(4 * dp), 0);
        bar.AddView(home);
        bar.AddView(_tabStrip);
        bar.AddView(_newTab);
        bar.AddView(new Space(this), new LinearLayout.LayoutParams(0, 1, 1f));
        bar.AddView(_chip);
        bar.AddView(_reload);

        _content = new FrameLayout(this);
        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetFitsSystemWindows(true);   // 상태 바 · 내비게이션 바 밑으로 들어가지 않게
        root.SetBackgroundColor(Color.ParseColor(Tokens.Bar));
        root.AddView(bar, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, (int)(Tokens.BarHeight * dp)));
        root.AddView(_content, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));
        SetContentView(root);

        // 런처만 앱과 메시지를 주고받는다 — 모듈 페이지(외부 서버)는 허락한 앱 기능만
        _launcher = NewWeb();
        if (WebViewFeature.IsFeatureSupported(WebViewFeature.WebMessageListener))
            WebViewCompat.AddWebMessageListener(_launcher, "appBridge", new HashSet<string> { Origin }, new MessageListener(json =>
            {
                if (FromLauncher.Dispatch(json, this) is { } error) Log(error);
            }));
        var loader = new WebViewAssetLoader.Builder().AddPathHandler("/assets/", new WebViewAssetLoader.AssetsPathHandler(this))!.Build()!;
        _launcher.SetWebViewClient(new AssetClient(loader));
        _launcher.SetWebChromeClient(new LauncherChrome(this));
        _launcher.LoadUrl($"{Origin}/assets/web/index.html");

        _scanner = new UsbScanner(this, data => { if (_active is { } key) Run(_webs[key], ModuleEvents.Scan(data)); },
                                  on => { _scanners = Math.Max(0, _scanners + (on ? 1 : -1)); RenderBar(); }, Log);
        _scanner.Start();
        RenderBar();
    }

    protected override void OnDestroy()
    {
        _scanner?.Stop();
        base.OnDestroy();
    }

    WebView NewWeb()
    {
        var w = new WebView(this);
        w.Settings.JavaScriptEnabled = true;
        w.Settings.DomStorageEnabled = true;
        _content.AddView(w, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
        return w;
    }

    static void Run(WebView web, string script) => web.EvaluateJavascript(script, null);
    static void Log(string s) => Android.Util.Log.Warn("fsharpdsldrivencrossplatform", s);
    string ServerOrigin => new Uri(_server).GetLeftPart(UriPartial.Authority);

    /// 탭을 보이거나(키) 런처를(null) — 카메라를 띄운 채 다른 데로 가면 취소
    void Show(string? key)
    {
        if (_photo is not null && key is not null) _photo.TrySetResult(null);
        _active = key;
        _launcher.Visibility = key is null ? ViewStates.Visible : ViewStates.Gone;
        foreach (var (k, w) in _webs) w.Visibility = k == key ? ViewStates.Visible : ViewStates.Gone;
        RenderBar();
    }

    void Close(string key)
    {
        var i = _tabs.IndexOf(key);
        _tabs.Remove(key);
        _content.RemoveView(_webs[key]);
        _webs[key].Destroy();
        _webs.Remove(key);
        if (_active == key) Show(_tabs.Count == 0 ? null : _tabs[Math.Min(i, _tabs.Count - 1)]);
        else RenderBar();
        Run(_launcher, ToLauncher.Launcher(_server, _tabs));
    }

    void RenderBar()
    {
        _tabStrip.RemoveAllViews();
        for (var i = 0; i < _tabs.Count; i++)
        {
            var key = _tabs[i];
            var module = Modules.Find(key)!;
            if (i > 0) _tabStrip.AddView(new TabDivider(this) { Visible = _active != key && _active != _tabs[i - 1] });
            var tab = new TabHeader(this) { Title = module.Name, Accent = module.Accent, Active = _active == key };
            tab.Clicked += (_, _) => Show(key);
            tab.CloseClicked += (_, _) => Close(key);
            _tabStrip.AddView(tab);
        }
        _newTab.Visibility = Modules.All.Any(m => !_tabs.Contains(m.Key)) ? ViewStates.Visible : ViewStates.Gone;
        _reload.On = _active is not null;
        _chip.Text = _scanners > 0 ? "스캐너 연결됨" : "스캐너 대기";
        _chip.On = _scanners > 0;
    }

    /// 뒤로 — 보고 있는 탭에 이전 페이지가 있으면 그리로, 아니면 기본 동작
    public override void OnBackPressed()
    {
        if (_active is { } key && _webs[key].CanGoBack()) _webs[key].GoBack();
        else base.OnBackPressed();
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (_pendingCamera is { } r)
        {
            if (grantResults.All(g => g == Permission.Granted)) r.Grant(r.GetResources()); else r.Deny();
            _pendingCamera = null;
        }
    }

    /// 런처의 카메라 권한 요청 — 앱 권한(CAMERA)을 받은 뒤 허락한다
    internal void OnCameraRequest(PermissionRequest request)
    {
        if (CheckSelfPermission(Android.Manifest.Permission.Camera) == Permission.Granted) request.Grant(request.GetResources());
        else { _pendingCamera = request; RequestPermissions([Android.Manifest.Permission.Camera], 1); }
    }

    // ── 런처 → 앱 ──

    public void Ready()
    {
        Run(_launcher, ToLauncher.Hello($"Android {RuntimeInformation.ProcessArchitecture}", !RuntimeFeature.IsDynamicCodeSupported));
        Run(_launcher, ToLauncher.Launcher(_server, _tabs));
    }

    public void OpenModule(string key)
    {
        if (_tabs.Contains(key)) { Show(key); return; }
        if (Modules.Find(key) is not { } module) { Log($"모르는 모듈 {key}"); return; }
        var web = NewWeb();
        web.SetWebViewClient(new WebViewClient());   // 링크도 이 탭 안에서 연다
        // 이 모듈에 허락한 앱 기능(window.app) — 서버 출처에만 넣고, 거기서 온 요청만 받는다
        if (Capabilities.Script(key) is { Length: > 0 } script
            && WebViewFeature.IsFeatureSupported(WebViewFeature.DocumentStartScript)
            && WebViewFeature.IsFeatureSupported(WebViewFeature.WebMessageListener))
        {
            var origins = new HashSet<string> { ServerOrigin };
            WebViewCompat.AddDocumentStartJavaScript(web, script, origins);
            WebViewCompat.AddWebMessageListener(web, "appBridge", origins, new MessageListener(async json => Run(web, await Capabilities.Handle(key, json, this))));
        }
        web.LoadUrl(_server.TrimEnd('/') + module.Path);
        _webs[key] = web;
        _tabs.Add(key);
        Show(key);
        Run(_launcher, ToLauncher.Launcher(_server, _tabs));
    }

    public void SetServer(string url)
    {
        var s = url.Trim().TrimEnd('/');
        if (!s.Contains("://")) s = "http://" + s;
        if (!Uri.TryCreate(s, UriKind.Absolute, out _)) { Log($"올바른 주소가 아님 {url}"); return; }
        if (s == _server) return;
        foreach (var key in _tabs.ToList()) { _content.RemoveView(_webs[key]); _webs[key].Destroy(); _webs.Remove(key); }
        _tabs.Clear();
        _server = s;
        _prefs.Edit()!.PutString("server", s)!.Apply();
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
        _launcher.Visibility = ViewStates.Visible;
        foreach (var w in _webs.Values) w.Visibility = ViewStates.Gone;
        Run(_launcher, ToLauncher.Camera());
        var result = await photo.Task;
        _photo = null;
        if (_tabs.Contains(moduleKey)) Show(moduleKey);
        return result;
    }
}

/// 웹 → 앱: addWebMessageListener — @JavascriptInterface([Export]) 없이 메시지를 받는다. UI 스레드로 온다
sealed class MessageListener(Action<string> onMessage) : Java.Lang.Object, WebViewCompat.IWebMessageListener
{
    public void OnPostMessage(WebView? view, WebMessageCompat? message, Android.Net.Uri? sourceOrigin, bool isMainFrame, JavaScriptReplyProxy? replyProxy) =>
        onMessage(message?.Data ?? "");
}

/// 런처(assets/web)를 https://appassets.androidplatform.net 로 — 메시지 출처 규칙에 걸 출처가 생긴다
sealed class AssetClient(WebViewAssetLoader loader) : WebViewClientCompat
{
    public override WebResourceResponse? ShouldInterceptRequest(WebView? view, IWebResourceRequest? request) => loader.ShouldInterceptRequest(request!.Url!);
}

sealed class LauncherChrome(MainActivity activity) : WebChromeClient
{
    public override void OnPermissionRequest(PermissionRequest? request)
    {
        if (request is null) return;
        if (request.GetResources().Contains(PermissionRequest.ResourceVideoCapture)) activity.OnCameraRequest(request);
        else request.Deny();
    }
}
