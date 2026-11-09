using System.IO.Ports;
using System.Runtime.Versioning;
using System.Text;
using Dsl;
using Microsoft.Win32;

namespace App;

/// USB 시리얼 스캐너(Windows) — 지원 기기의 COM 포트가 나타나면 열고, 끊기면 닫는다(2초마다 확인)
[SupportedOSPlatform("windows")]
static class UsbScanner
{
    /// 지원 기기의 COM 포트 이름 — 장치 관리자가 남긴 레지스트리(뺀 기기도 남아 있다)
    static IEnumerable<string> KnownPorts()
    {
        using var usb = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USB");
        if (usb is null) yield break;
        foreach (var (vid, pid, _) in Scanners.Devices)
        {
            var prefix = $"VID_{vid:X4}&PID_{pid:X4}";   // 복합 장치는 뒤에 &MI_xx 가 붙는다
            foreach (var dev in usb.GetSubKeyNames().Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                using var d = usb.OpenSubKey(dev);
                if (d is null) continue;
                foreach (var inst in d.GetSubKeyNames())
                {
                    using var p = d.OpenSubKey($@"{inst}\Device Parameters");
                    if (p?.GetValue("PortName") is string name) yield return name;
                }
            }
        }
    }

    public static void Start(Action<string> onScan, Action<bool> onConnected, Action<string> log) => _ = Task.Run(async () =>
    {
        Task reading = Task.CompletedTask;
        while (true)
        {
            if (reading.IsCompleted)
            {
                var present = SerialPort.GetPortNames().ToHashSet();   // 지금 꽂혀 있는 포트만
                if (KnownPorts().FirstOrDefault(present.Contains) is { } name)
                {
                    try
                    {
                        // DTR 을 올려야 보내기 시작하는 스캐너가 있다(KDC280)
                        var port = new SerialPort(name, Scanners.BaudRate, Parity.None, 8, StopBits.One) { DtrEnable = true, RtsEnable = true };
                        port.Open();
                        log($"스캐너 연결 {name}");
                        onConnected(true);
                        reading = Read(port, onScan, onConnected, log);
                    }
                    catch (Exception e) { log($"스캐너 열기 실패 {name}: {e.Message}"); }
                }
            }
            await Task.Delay(2000);
        }
    });

    static async Task Read(SerialPort port, Action<string> onScan, Action<bool> onConnected, Action<string> log)
    {
        var lines = new LineBuffer();
        var buf = new byte[4096];
        try
        {
            while (true)
            {
                var n = await port.BaseStream.ReadAsync(buf);
                if (n == 0) throw new IOException("포트가 닫힘");
                foreach (var line in lines.Feed(buf, n)) onScan(line);
            }
        }
        catch (Exception e) { log($"스캐너 끊김 {port.PortName}: {e.Message}"); }
        try { port.Dispose(); } catch { }
        onConnected(false);
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
