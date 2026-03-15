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

## リリース運用方針
- GitHub Actions では package / publish を実行しません（CI の build / test のみ）。
- リリース用成果物（`.app` / `.dmg`）はローカル環境で手動作成して公開します。

## 手動 publish 手順（macOS）
1. 前提ツールを準備する。
   - .NET SDK 10
   - Xcode Command Line Tools（`codesign` / `xcrun` / `notarytool` / `hdiutil`）
2. Apple 関連の値を環境変数に設定する。
```bash
export APP_NAME="Hit A Screen"
export EXECUTABLE_NAME="HitAScreen.App"
export VERSION="<version>" # 例: 0.5.0
export APPLE_BUNDLE_ID="<bundle-id>"
export APPLE_CERT_P12_PATH="<path-to-p12>"
export APPLE_CERT_PASSWORD="<p12-password>"
export APPLE_ID="<apple-id>"
export APPLE_TEAM_ID="<apple-team-id>"
export APPLE_APP_SPECIFIC_PASSWORD="<app-specific-password>"
export CODESIGN_IDENTITY="Developer ID Application: <team-name> (<team-id>)"
```
3. 依存復元とテストを実行する。
```bash
dotnet restore HitAScreen.slnx
dotnet test tests/HitAScreen.Core.Tests/HitAScreen.Core.Tests.csproj --configuration Release --no-restore
```
4. publish と成果物作成を実行する。
   - App Store sandbox でのクラッシュ回避のため、`PublishSingleFile=true` を必ず有効化する（参考: Avalonia Docs「Sandbox and bundle」）。
```bash
TFM="net10.0"
RID="osx-arm64"
BUILD_ROOT="src/HitAScreen.App/bin/Release"
PUBLISH_DIR="${BUILD_ROOT}/${TFM}/${RID}/publish"
RELEASE_DIR="${BUILD_ROOT}/release"
NOTARY_PROFILE="hitascreen-notary"
DMG_NAME="HitAScreen-${VERSION}-macos-arm64.dmg"
KEYCHAIN_PATH="${TMPDIR:-/tmp}/hitascreen-build.keychain-db"
KEYCHAIN_PASSWORD="$(uuidgen)"

dotnet publish src/HitAScreen.App/HitAScreen.App.csproj \
  -c Release \
  -r "${RID}" \
  --framework "${TFM}" \
  --self-contained true \
  -p:PublishSingleFile=true \
  --no-restore

mkdir -p "${RELEASE_DIR}"

scripts/release/create-icns.sh \
  hit-a-screen-icon.png \
  "${RELEASE_DIR}/AppIcon.icns"

scripts/release/create-app-bundle.sh \
  "${PUBLISH_DIR}" \
  "${APP_NAME}" \
  "${APPLE_BUNDLE_ID}" \
  "${VERSION}" \
  "${EXECUTABLE_NAME}" \
  "${RELEASE_DIR}/AppIcon.icns" \
  "${RELEASE_DIR}"

security delete-keychain "${KEYCHAIN_PATH}" 2>/dev/null || true
security create-keychain -p "${KEYCHAIN_PASSWORD}" "${KEYCHAIN_PATH}"
security set-keychain-settings -lut 21600 "${KEYCHAIN_PATH}"
security unlock-keychain -p "${KEYCHAIN_PASSWORD}" "${KEYCHAIN_PATH}"
security import "${APPLE_CERT_P12_PATH}" -k "${KEYCHAIN_PATH}" -P "${APPLE_CERT_PASSWORD}" -T /usr/bin/codesign -T /usr/bin/security
security set-key-partition-list -S apple-tool:,apple: -s -k "${KEYCHAIN_PASSWORD}" "${KEYCHAIN_PATH}"
security list-keychains -d user -s "${KEYCHAIN_PATH}" $(security list-keychains -d user | tr -d '"')

scripts/release/codesign-app.sh \
  "${RELEASE_DIR}/${APP_NAME}.app" \
  "${CODESIGN_IDENTITY}" \
  "scripts/release/entitlements.plist"

xcrun notarytool store-credentials "${NOTARY_PROFILE}" \
  --apple-id "${APPLE_ID}" \
  --team-id "${APPLE_TEAM_ID}" \
  --password "${APPLE_APP_SPECIFIC_PASSWORD}"

scripts/release/notarize-and-staple.sh \
  "${RELEASE_DIR}/${APP_NAME}.app" \
  "${NOTARY_PROFILE}"

scripts/release/create-dmg.sh \
  "${RELEASE_DIR}/${APP_NAME}.app" \
  "${RELEASE_DIR}/${DMG_NAME}" \
  "HitAScreen"

(cd "${RELEASE_DIR}" && shasum -a 256 "${DMG_NAME}" > SHA256SUMS.txt)
```
5. `src/HitAScreen.App/bin/Release/release` 配下の `*.dmg` と `SHA256SUMS.txt` を GitHub Releases に手動アップロードする。

## リリース版の導入（GitHub Releases）
1. Releases から `HitAScreen-<version>-macos-arm64.dmg` と `SHA256SUMS.txt` を取得する。
2. ターミナルで `shasum -a 256 HitAScreen-<version>-macos-arm64.dmg` を実行し、`SHA256SUMS.txt` の値と一致することを確認する。
3. `dmg` を開いて `Hit A Screen.app` を `Applications` へコピーする。
4. 初回起動時に Gatekeeper 警告が出た場合は、`システム設定 > プライバシーとセキュリティ` から実行を許可する。

## 初回起動時の権限設定
1. `Hit A Screen.app` を起動し、コントロールパネルを開く。
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
