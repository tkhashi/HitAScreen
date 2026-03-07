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

## リリース版の導入（GitHub Releases）
1. Releases から `HitAScreen-<version>-macos-arm64.dmg` と `SHA256SUMS.txt` を取得する。
2. ターミナルで `shasum -a 256 HitAScreen-<version>-macos-arm64.dmg` を実行し、`SHA256SUMS.txt` の値と一致することを確認する。
3. `dmg` を開いて `HitAScreen.app` を `Applications` へコピーする。
4. 初回起動時に Gatekeeper 警告が出た場合は、`システム設定 > プライバシーとセキュリティ` から実行を許可する。

## 初回起動時の権限設定
1. `HitAScreen.app` を起動し、コントロールパネルを開く。
2. 設定画面の `Accessibility を開く` / `Input Monitoring を開く` / `Screen Recording を開く` ボタンから各設定画面を開く。
3. 以下 2 権限は必須で許可する。
   - Accessibility
   - Input Monitoring
4. `Screen Recording` は任意。未許可でも動作するが、一部アプリではウィンドウタイトル取得が制限され、対象ウィンドウ照合精度が下がる場合がある。
5. 設定画面の `権限状態を再取得` を押し、必須 2 項目が `許可済み` になっていることを確認する。

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
- macOS での基本動作には、入力監視 / アクセシビリティの権限が必要です。`Screen Recording` は任意で、許可すると一部アプリで前面ウィンドウ照合精度が上がります。
- OCR と画像ベースフォールバックは、このイテレーションではスコープ外です。
