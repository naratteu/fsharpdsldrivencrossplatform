using System.Text;
using Android.Content;
using Android.Hardware.Usb;
using Dsl;

namespace App;

/// USB 시리얼 스캐너(Android) — 지원 기기가 꽂히면 권한을 받아 CDC-ACM 으로 열고, 빠지면 닫는다. 콜백은 UI 스레드에서
sealed class UsbScanner(Activity activity, Action<string> onScan, Action<bool> onConnected, Action<string> log)
{
    readonly UsbManager _usb = (UsbManager)activity.GetSystemService(Context.UsbService)!;
    readonly string _grantAction = activity.PackageName + ".USB_PERMISSION";
    (UsbDevice Device, UsbDeviceConnection Connection, CancellationTokenSource Stop)? _opened;
    Receiver? _receiver;

    static bool Supported(UsbDevice d) => Scanners.Devices.Any(s => s.Vid == d.VendorId && s.Pid == d.ProductId);

    public void Start()
    {
        _receiver = new Receiver(OnIntent);
        var filter = new IntentFilter();
        foreach (var a in new[] { UsbManager.ActionUsbDeviceAttached, UsbManager.ActionUsbDeviceDetached, _grantAction }) filter.AddAction(a);
        if (OperatingSystem.IsAndroidVersionAtLeast(33)) activity.RegisterReceiver(_receiver, filter, ReceiverFlags.NotExported);
        else activity.RegisterReceiver(_receiver, filter);
        TryConnect();
    }

    public void Stop()
    {
        try { activity.UnregisterReceiver(_receiver); } catch { }
        Close();
    }

    void OnIntent(Intent intent)
    {
        if (intent.Action == UsbManager.ActionUsbDeviceAttached) TryConnect();
        else if (intent.Action == UsbManager.ActionUsbDeviceDetached)
        {
#pragma warning disable CA1422
            if (_opened is { } o && intent.GetParcelableExtra(UsbManager.ExtraDevice) is UsbDevice gone && gone.DeviceName == o.Device.DeviceName) Close();
#pragma warning restore CA1422
        }
        else if (intent.Action == _grantAction)
        {
            if (intent.GetBooleanExtra(UsbManager.ExtraPermissionGranted, false)) TryConnect();
            else log("스캐너 USB 권한 거부");
        }
    }

    void TryConnect()
    {
        if (_opened is not null) return;
        if (_usb.DeviceList?.Values.FirstOrDefault(Supported) is not { } d) return;
        if (_usb.HasPermission(d)) { Open(d); return; }
        var intent = new Intent(_grantAction).SetPackage(activity.PackageName);
        _usb.RequestPermission(d, PendingIntent.GetBroadcast(activity, 0, intent, PendingIntentFlags.Mutable));
    }

    void Close()
    {
        if (_opened is not { } o) return;
        _opened = null;
        o.Stop.Cancel();
        o.Connection.Close();
        onConnected(false);
    }

    /// CDC-ACM: 통신(제어) 인터페이스에 회선 설정 · DTR/RTS, 데이터 인터페이스의 bulk IN 을 읽는다
    void Open(UsbDevice d)
    {
        var ifaces = Enumerable.Range(0, d.InterfaceCount).Select(d.GetInterface).ToList();
        var control = ifaces.FirstOrDefault(i => i.InterfaceClass == UsbClass.Comm);
        var data = ifaces.FirstOrDefault(i => i.InterfaceClass == UsbClass.CdcData);
        var bulkIn = data is null ? null : Enumerable.Range(0, data.EndpointCount).Select(data.GetEndpoint)
            .FirstOrDefault(e => e!.Type == UsbAddressing.XferBulk && e.Direction == UsbAddressing.In);
        var conn = _usb.OpenDevice(d);
        if (control is null || data is null || bulkIn is null || conn is null)
        {
            conn?.Close();
            log($"CDC-ACM 이 아니거나 열 수 없음 {d.ProductName}");
            return;
        }
        conn.ClaimInterface(control, true);
        conn.ClaimInterface(data, true);
        var b = Scanners.BaudRate;
        // SET_LINE_CODING: 속도(LE 4바이트) · 정지 비트 1 · 패리티 없음 · 8비트
        conn.ControlTransfer((UsbAddressing)0x21, 0x20, 0, control.Id, [(byte)b, (byte)(b >> 8), (byte)(b >> 16), (byte)(b >> 24), 0, 0, 8], 7, 1000);
        // SET_CONTROL_LINE_STATE: DTR | RTS — DTR 을 올려야 보내기 시작하는 스캐너가 있다(KDC280)
        conn.ControlTransfer((UsbAddressing)0x21, 0x22, 0x3, control.Id, null, 0, 1000);
        var stop = new CancellationTokenSource();
        _opened = (d, conn, stop);
        log($"스캐너 연결 {d.ProductName}");
        onConnected(true);
        new Thread(() =>
        {
            var lines = new LineBuffer();
            var buf = new byte[4096];
            while (!stop.IsCancellationRequested)
            {
                var n = conn.BulkTransfer(bulkIn, buf, buf.Length, 500);   // 시간 초과도 음수 — 계속 돈다
                if (n > 0) foreach (var line in lines.Feed(buf, n)) activity.RunOnUiThread(() => onScan(line));
            }
        }) { IsBackground = true }.Start();
    }

    sealed class Receiver(Action<Intent> f) : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent) { if (intent is not null) f(intent); }
    }
}

/// 받은 바이트를 모아 CR · LF 로 끝난 줄마다 돌려준다. 빈 줄은 버린다
sealed class LineBuffer
{
    readonly Decoder _decoder = Encoding.UTF8.GetDecoder();   // 여러 바이트 글자가 나뉘어 와도 이어 붙인다
    readonly StringBuilder _pending = new();

    public List<string> Feed(byte[] data, int count)
    {
        var chars = new char[_decoder.GetCharCount(data, 0, count)];
        _decoder.GetChars(data, 0, count, chars, 0);
        var lines = new List<string>();
        foreach (var c in chars)
        {
            if (c is '\r' or '\n') { if (_pending.Length > 0) { lines.Add(_pending.ToString()); _pending.Clear(); } }
            else _pending.Append(c);
        }
        return lines;
    }
}
