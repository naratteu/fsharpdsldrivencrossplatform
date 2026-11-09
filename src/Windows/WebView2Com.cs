// WebView2 COM 인터페이스 — NativeAOT 판. 공식 래퍼(옛 COM 방식)는 NativeAOT 에서 못 쓴다.
// 공식 Microsoft.Web.WebView2.Core.Raw 의 GUID · 메서드 순서 그대로, 쓰는 메서드까지만 슬롯을 채운다(안 쓰는 메서드는 _ 로 자리만)
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace App;

static class WebView2Com
{
    static readonly StrategyBasedComWrappers Wrappers = new();
    /// Avalonia 가 주는 ICoreWebView2 포인터 → 아래 선언
    public static ICoreWebView2 From(nint pointer) => (ICoreWebView2)Wrappers.GetOrCreateObjectForComInstance(pointer, CreateObjectFlags.None);
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16), Guid("76ECEACB-0462-4D94-AC83-423A6793775E")]
public partial interface ICoreWebView2
{
    void _get_Settings(); void _get_Source();
    void Navigate(string uri);
    void _NavigateToString();
    void _add_NavigationStarting(); void _remove_NavigationStarting(); void _add_ContentLoading(); void _remove_ContentLoading();
    void _add_SourceChanged(); void _remove_SourceChanged(); void _add_HistoryChanged(); void _remove_HistoryChanged();
    void _add_NavigationCompleted(); void _remove_NavigationCompleted(); void _add_FrameNavigationStarting(); void _remove_FrameNavigationStarting();
    void _add_FrameNavigationCompleted(); void _remove_FrameNavigationCompleted(); void _add_ScriptDialogOpening(); void _remove_ScriptDialogOpening();
    void AddPermissionRequested(ICoreWebView2PermissionRequestedEventHandler handler, out long token);   // add_PermissionRequested
    void _remove_PermissionRequested(); void _add_ProcessFailed(); void _remove_ProcessFailed();
    void AddScriptToExecuteOnDocumentCreated(string javaScript, ICoreWebView2AddScriptToExecuteOnDocumentCreatedCompletedHandler handler); void _RemoveScriptToExecuteOnDocumentCreated();
    void ExecuteScript(string javaScript, nint completedHandler);                         // 결과는 받지 않는다(0)
    void _CapturePreview();
    void Reload();
    void _PostWebMessageAsJson(); void _PostWebMessageAsString();
    void AddWebMessageReceived(ICoreWebView2WebMessageReceivedEventHandler handler, out long token);   // add_WebMessageReceived
    void _remove_WebMessageReceived(); void _CallDevToolsProtocolMethod(); void _get_BrowserProcessId();
    int GetCanGoBack();                                                                    // get_CanGoBack(BOOL*)
    void _get_CanGoForward();
    void GoBack();
}

[GeneratedComInterface, Guid("57213F19-00E6-49FA-8E07-898EA01ECBD2")]
public partial interface ICoreWebView2WebMessageReceivedEventHandler
{
    void Invoke(ICoreWebView2 sender, ICoreWebView2WebMessageReceivedEventArgs args);
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16), Guid("0F99A40C-E962-4207-9E92-E3D542EFF849")]
public partial interface ICoreWebView2WebMessageReceivedEventArgs
{
    string GetSource();                                                                    // get_Source — 보낸 페이지 주소
    void _get_WebMessageAsJson();
    string TryGetWebMessageAsString();
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16), Guid("B99369F3-9B11-47B5-BC6F-8E7895FCEA17")]
public partial interface ICoreWebView2AddScriptToExecuteOnDocumentCreatedCompletedHandler
{
    void Invoke(int errorCode, string? id);
}

[GeneratedComInterface, Guid("15E1C6A3-C72A-4DF3-91D7-D097FBEC6BFD")]
public partial interface ICoreWebView2PermissionRequestedEventHandler
{
    void Invoke(ICoreWebView2 sender, ICoreWebView2PermissionRequestedEventArgs args);
}

[GeneratedComInterface, Guid("973AE2EF-FF18-4894-8FB2-3C758F046810")]
public partial interface ICoreWebView2PermissionRequestedEventArgs
{
    void _get_uri();
    int GetPermissionKind();                                                               // COREWEBVIEW2_PERMISSION_KIND — 카메라 2
    void _get_IsUserInitiated(); void _get_State();
    void SetState(int state);                                                              // COREWEBVIEW2_PERMISSION_STATE — 허락 1, 거절 2
}

// 콜백 — 람다를 받아 COM 객체로 넘긴다
[GeneratedComClass]
public sealed partial class WebMessageReceived(Action<string, string> f) : ICoreWebView2WebMessageReceivedEventHandler
{
    /// 보낸 페이지 주소, 메시지
    public void Invoke(ICoreWebView2 sender, ICoreWebView2WebMessageReceivedEventArgs args) => f(args.GetSource(), args.TryGetWebMessageAsString());
}

[GeneratedComClass]
public sealed partial class ScriptAdded(Action<int> f) : ICoreWebView2AddScriptToExecuteOnDocumentCreatedCompletedHandler
{
    public void Invoke(int errorCode, string? id) => f(errorCode);
}

[GeneratedComClass]
public sealed partial class PermissionRequested(Action<ICoreWebView2PermissionRequestedEventArgs> f) : ICoreWebView2PermissionRequestedEventHandler
{
    public void Invoke(ICoreWebView2 sender, ICoreWebView2PermissionRequestedEventArgs args) => f(args);
}
