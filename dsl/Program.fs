/// 생성기 — dotnet Dsl.dll <출력 폴더> <대상...>   대상: contracts avalonia android web
module Program

open System.IO

let private save (dir: string) (name: string) (text: string) =
    let path = Path.Combine(dir, name)
    Directory.CreateDirectory(string (Path.GetDirectoryName path)) |> ignore
    // 같으면 쓰지 않는다 — 시각이 안 바뀌어야 증분 빌드가 산다
    if not (File.Exists path) || File.ReadAllText path <> text then File.WriteAllText(path, text)

[<EntryPoint>]
let main argv =
    match List.ofArray argv with
    | dir :: targets when not targets.IsEmpty ->
        for t in targets do
            match t with
            | "contracts" -> save dir "Contracts.g.cs" (GenContracts.generate ())
            | "avalonia" -> save dir "Components.Avalonia.g.cs" (GenAvalonia.generate ())
            | "android" -> save dir "Components.Android.g.cs" (GenAndroid.generate ())
            | "web" ->
                save dir "web/bridge.g.js" (GenWeb.bridgeJs ())
                save dir "web/bridge.g.d.ts" (GenWeb.bridgeDts ())
                save dir "web/app.g.d.ts" (GenWeb.appDts ())
            | t -> failwith $"모르는 대상 {t}"
        0
    | _ ->
        eprintfn "dotnet Dsl.dll <출력 폴더> <contracts|avalonia|android|web>..."
        2
