/// UI 부품 → Avalonia 컨트롤(C#)
module GenAvalonia

open Model
open Emit
open Spec

let generate () =
    lines [
        header
        "#nullable enable"
        "using Avalonia;"
        "using Avalonia.Controls;"
        "using Avalonia.Controls.Shapes;"
        "using Avalonia.Input;"
        "using Avalonia.Layout;"
        "using Avalonia.Media;"
        "namespace Dsl;"
        ""
        "static class Ui"
        "{"
        "    static IBrush B(string c) => new SolidColorBrush(Color.Parse(c));"
        "    public static Control Row(double spacing, params Control?[] kids)"
        "    {"
        "        var p = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing, VerticalAlignment = VerticalAlignment.Center };"
        "        foreach (var k in kids) if (k is not null) p.Children.Add(k);"
        "        return p;"
        "    }"
        "    public static Control Dot(double size, string fill) => new Ellipse { Width = size, Height = size, Fill = B(fill), VerticalAlignment = VerticalAlignment.Center };"
        "    public static TextBlock Label(string text, double size, bool bold, string color) =>"
        "        new() { Text = text, FontSize = size, FontWeight = bold ? FontWeight.Bold : FontWeight.Normal, Foreground = B(color), VerticalAlignment = VerticalAlignment.Center };"
        "    public static Control Glyph(string glyph, double size, string color, Action onTap)"
        "    {"
        "        var t = Label(glyph, size, false, color);"
        "        t.Background = Brushes.Transparent;   // 글자 사이 빈 곳도 눌리게"
        "        t.Cursor = new Cursor(StandardCursorType.Hand);"
        "        t.PointerReleased += (_, e) => { e.Handled = true; onTap(); };"
        "        return t;"
        "    }"
        "    public static Control Pill(double l, double t, double r, double b, double radius, string fill, Control? child) =>"
        "        new Border { Padding = new Thickness(l, t, r, b), CornerRadius = new CornerRadius(radius), Background = B(fill), Child = child, VerticalAlignment = VerticalAlignment.Center };"
        "}"
        for c in components do
            ""
            $"/// {c.Doc}"
            $"public sealed class {c.Name} : Decorator"
            "{"
            yield! componentMembers c
            ""
            $"    public {c.Name}()"
            "    {"
            match c.OnTap with
            | Some e ->
                "        Cursor = new Cursor(StandardCursorType.Hand);"
                $"        PointerReleased += (_, _) => {e}?.Invoke(this, EventArgs.Empty);"
            | None -> ()
            "        Render();"
            "    }"
            ""
            let root = nodeExpr "Ui" c.Root
            $"    void Render() => Child = {root};"
            "}" ]
