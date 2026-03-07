# HitAScreen 実行タスク

## 方針
- 実行順は `PLAN.md` の Phase 1 -> 2 -> 3 -> 4 -> 5 に固定する。
- OCR / 画像ベース検出は本タスク対象外。
- GitHub Packages への配布は対象外（GitHub Releases のみ）。
- 2026-03-08 時点で `Screen Recording` は任意権限とする（未許可でも動作可能だが、一部アプリで対象ウィンドウ照合精度に影響）。
- チェック状態の更新日: 2026-03-08

## Phase 1: 設定UI拡張（最優先）

- [x] P1-1: ショートカット設定モデル追加
  - 作業内容:
    - `UserSettings` に起動 + 操作キーの設定モデルを追加
    - 既存設定ファイル互換（未定義項目はデフォルト補完）を維持
  - 完了条件:
    - 既存 `settings.json` から例外なく読み込みできる

- [x] P1-2: 設定UIにショートカット編集機能を追加
  - 作業内容:
    - 起動ホットキー、モニタ左右切替、再解析、アクション切替を編集可能化
    - `ESC/Enter/Backspace` は固定キーとして表示のみ
  - 完了条件:
    - UIから保存したキー設定が `UserSettings` に反映される

- [x] P1-3: 実行時のキー判定を設定参照へ置換
  - 作業内容:
    - ハードコード判定を廃止し、設定値からアクションへ解決
    - 保存後に即時反映（必要な再登録含む）
  - 完了条件:
    - 変更した操作キーでセッション制御できる

- [x] P1-4: AX除外Role設定を追加
  - 作業内容:
    - `ExcludedAxRoles` を設定モデル/UIへ追加
    - 初期値に `AXGroup` を含める
  - 完了条件:
    - 設定変更が保存され、再起動後も保持される

- [x] P1-5: AX候補抽出に除外Roleを適用
  - 作業内容:
    - 候補列挙時に除外Roleを除去
    - ログ/診断で適用有無が追跡できるようにする
  - 完了条件:
    - `AXGroup` 除外時に候補重なりが減ることを確認できる

- [x] P1-6: ラベル外観設定（色/透明度/サイズ/フォントサイズ）を追加
  - 作業内容:
    - 設定UIと `UserSettings` に外観項目を追加
    - 範囲外入力のバリデーション（クランプ）を実装
  - 完了条件:
    - 保存値がオーバーレイ描画へ反映される

- [x] P1-7: 許可状態表示UIを拡張
  - 作業内容:
    - Accessibility / Input Monitoring / Screen Recording を個別表示
    - 最新状態へ更新する再取得導線を用意
  - 完了条件:
    - 設定画面で3権限の状態を常時確認できる

- [x] P1-8: 権限別「設定を開く」ボタンを追加
  - 作業内容:
    - 権限ごとに OS 設定画面を開く処理を実装
    - 失敗時メッセージを表示
  - 完了条件:
    - 各ボタンから該当権限の設定導線へ遷移できる

## Phase 2: 要件ギャップ残件

- [x] P2-1: ホットキー競合検出（FR-2.2）
  - 作業内容:
    - 設定保存時に登録成否/競合を判定
    - 競合時にUI警告を表示
  - 完了条件:
    - 競合をユーザーが即時認識できる

- [x] P2-2: 自動起動 ON/OFF 実装（FR-1.2）
  - 作業内容:
    - macOS ログイン項目の登録/解除
    - 設定UIへのトグル追加
  - 完了条件:
    - ON/OFF 変更がOS設定に反映される

- [x] P2-3: 診断画面の拡張（FR-11）
  - 作業内容:
    - コンテキスト項目、処理時間、候補数、権限状態の可視化改善
  - 完了条件:
    - 開発者がセッション状態を1画面で追跡できる

## Phase 3: 品質保証

- [x] P3-0: 既存Coreテスト（ラベル確定/抑制判定/フォールバック）を整備済み

- [x] P3-1: 設定拡張に対する単体テスト追加
  - 作業内容:
    - 設定保存/読込の互換
    - ショートカット解決
    - AX除外Roleフィルター
    - 外観設定値バリデーション
  - 完了条件:
    - 追加テストが安定して通過する

- [ ] P3-2: 手動テスト MT-01〜MT-09 実施
  - 作業内容:
    - グローバルホットキー、権限導線、他アプリ操作、複数モニタを実機確認
  - 完了条件:
    - テンプレートに沿った結果記録を残す

- [x] P3-3: NFR 計測（遅延/メモリ/安定性）
  - 完了条件:
    - 測定値と合否判断を記録できる

- [x] P3-4: ドキュメント更新（結果反映）
  - 完了条件:
    - `README.md` / `docs/` / `PLAN.md` / `TASK.md` の内容が一致

## Phase 4: 後回し（最後に着手）

- [x] P4-1: ラベル生成/確定方式の改修（prefix衝突排除）
  - 作業内容:
    - 固定長ラベル生成へ変更（同一セッション内で同じ文字数）
    - prefix衝突を生成しない規則へ変更（`A` と `AB` の同時存在を禁止）
    - 入力途中は候補フィルタのみ、実行は完全一致時のみへ統一
    - 文字割当を home row 優先（`ASDFGHJKL`）へ変更
    - balanced hint generation で視線移動/手の移動を抑える割当アルゴリズムを導入
    - ラベル生成/確定ロジックの単体テスト追加
  - 完了条件:
    - prefix衝突ラベルが生成されない
    - 2文字以上ラベルが追加クリック/追加ステップなしで選択できる
    - 入力1文字目では実行されず、完全一致でのみ実行される

- [x] P4-2: 初回ホットキー時のラベル縦ズレ修正

- [x] P4-3: 2回目以降ヘルプテキスト隠れ修正

- [x] P4-4: ラベル重なり対策
  - 作業内容:
    - 親子候補の間引きルール調整
    - Phase 1 の AX除外Role設定と整合
  - 完了条件:
    - 視認性が維持され、選択ミスが減る

## Phase 5: 配布・リリース（GitHub Releases）

- [x] P5-1: Release workflow 作成（タグ `vX.Y.Z` 起動）
  - 作業内容:
    - `.github/workflows/release.yml` を追加
    - `dotnet test` / publish / packaging / Release upload のジョブを定義
  - 完了条件:
    - タグ push で workflow が起動する

- [x] P5-2: self-contained publish ステップ作成
  - 作業内容:
    - `dotnet publish -c Release -r osx-arm64 --self-contained true` を標準化
  - 完了条件:
    - CI 上で publish 出力が生成される

- [x] P5-3: `.app` バンドル生成スクリプト作成
  - 作業内容:
    - `Contents/MacOS`, `Contents/Resources`, `Info.plist` を組み立てる
  - 完了条件:
    - publish 出力から `.app` が自動生成される

- [x] P5-4: `hit-a-screen-icon.png` から `icns` 生成
  - 作業内容:
    - `iconset` を経由して `AppIcon.icns` を生成
    - 必要に応じて視認性のために単純化版を用意
  - 完了条件:
    - `.app` にアイコンが反映される

- [x] P5-5: codesign + entitlements 組み込み
  - 作業内容:
    - `.app` と内部実行ファイルを署名
    - hardened runtime と entitlements を適用
  - 完了条件:
    - `codesign --verify` が成功する

- [x] P5-6: notarize + staple
  - 作業内容:
    - Apple notarization を実行し、`.app` または `dmg` に staple
  - 完了条件:
    - notarization 成功ログが取得できる

- [x] P5-7: `dmg` と `SHA256SUMS.txt` 生成
  - 作業内容:
    - 配布用 `dmg` を作成
    - ハッシュファイルを生成
  - 完了条件:
    - 2つの成果物が CI で生成される

- [x] P5-8: GitHub Release へ資産アップロード
  - 作業内容:
    - `HitAScreen-<version>-macos-arm64.dmg` と `SHA256SUMS.txt` を添付
  - 完了条件:
    - Release 画面で資産が確認できる

- [x] P5-9: README に導入手順/権限付与手順を追記
  - 作業内容:
    - Release ダウンロード手順
    - 初回起動時の権限設定手順
  - 完了条件:
    - README の手順だけで初回導入が完了できる

## 保留課題（旧Phase A）
現在問題が顕在化していないため保留。再発時に再オープンする。

- [ ] H-1: 終了時チラつき調査と修正
- [ ] H-2: 入力遅延の再現固定/計測/修正
- [ ] H-3: クリック信頼性の追加改善（`AXPress` フォールバック含む）

## ユーザー実施項目（必須）
- [ ] [USER] Apple Developer Program アカウントを準備
- [ ] [USER] Developer ID Application 証明書（p12）を作成
- [ ] [USER] notarization 認証情報を準備
- [ ] [USER] GitHub Secrets を登録
  - `APPLE_CERT_P12_BASE64`
  - `APPLE_CERT_PASSWORD`
  - `APPLE_TEAM_ID`
  - `APPLE_ID`
  - `APPLE_APP_SPECIFIC_PASSWORD`（または ASC API key）
- [ ] [USER] `CFBundleIdentifier` を確定

## 最終完了チェック
- [ ] 自動テスト通過
- [ ] MT-01〜MT-13 実施完了
- [ ] GitHub Release に `dmg` + `SHA256SUMS.txt` が公開済み
- [ ] `README.md` / `PLAN.md` / `TASK.md` / `ADR.md` の整合確認
