# HitAScreen

HitAScreen は .NET 10 + Avalonia で作る macOS 向け常駐ユーティリティです。

## 実装済み
- トレイアイコン常駐、明示終了。
- EventTap によるグローバルホットキー登録（デフォルト: `Cmd+Shift+M`）。
- 抑制ポリシー（フルスクリーン/プロセス除外、抑制時は `Unregister()` 方式）。
- 前面ウィンドウ文脈取得（失敗時はカーソル位置モニタへフォールバック）。
- アクセシビリティ（Accessibility）ベースの候補要素取得。
- オーバーレイでのヒントラベル表示とキーボードのみの選択。
- アクション: 左クリック/右クリック/ダブルクリック/フォーカス移動。
- モニタ切替（左右キー）、再解析（Tab）、キャンセル（Esc）。
- 設定永続化と診断パネル。
- Core の単体テスト。

## 構成
- `src/HitAScreen.App`: Avalonia アプリ本体（トレイ/設定パネル/オーバーレイ）。
- `src/HitAScreen.Core`: オーケストレーション/状態遷移/ラベル生成/抑制判定。
- `src/HitAScreen.Platform.Abstractions`: OS 抽象（契約・型）。
- `src/HitAScreen.Platform.MacOS`: macOS 実装（EventTap/AX/CGEvent 等）。
- `src/HitAScreen.Infrastructure`: JSON 設定ストア、ログ。
- `tests/HitAScreen.Core.Tests`: 単体テスト。
- `docs/manual-tests`: 手動テスト計画と結果テンプレート。

## ビルド
```bash
dotnet build HitAScreen.slnx
```

## テスト
```bash
dotnet test tests/HitAScreen.Core.Tests/HitAScreen.Core.Tests.csproj
```

## 実行
```bash
dotnet run --project src/HitAScreen.App/HitAScreen.App.csproj
```

## オーバーレイ操作キー
- `Esc`: キャンセル
- `Backspace`: 1文字削除
- `Enter`: 確定
- `Left` / `Right`: モニタ切替
- `Tab`: 再解析
- `F1`: 左クリック
- `F2`: 右クリック
- `F3`: ダブルクリック
- `F4`: フォーカス移動
- `A-Z`, `0-9`: ラベル入力

## 補足
- macOS での完全な動作には、画面収録 / 入力監視 / アクセシビリティの権限が必要です。
- OCR と画像ベースフォールバックは、このイテレーションではスコープ外です。
