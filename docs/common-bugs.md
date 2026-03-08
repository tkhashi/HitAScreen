# よく発生する不具合メモ

最終更新: 2026-03-08 (branch: `codex/overlay-click-cancel-rainbow`)

## 1. オーバーレイは表示されるがラベルが出ない
- 事象:
  - オーバーレイ枠は表示されるが、ヒントラベルが 0 件になる。
- 主な原因:
  - `Accessibility` 権限が未許可で候補要素を取得できていない。
  - `ExcludedAxRoles` を広く設定しすぎて候補が全除外されている。
- 解決策:
  - 設定画面の `権限` タブで `Accessibility` を許可し、`権限状態を再取得` を実行する。
  - `ExcludedAxRoles` を最小構成（例: `AXGroup` のみ）へ戻して再解析する。
  - ログ (`~/Library/Application Support/HitAScreen/hitascreen.log`) の `session-started` / `candidateCount` を確認する。

## 2. ESC / Enter / Backspace などが反応しない
- 事象:
  - オーバーレイ中にキャンセル・確定・削除キーが効かない。
- 主な原因:
  - `Input Monitoring` 権限不足でグローバルキーイベントを取得できていない。
  - EventTap 登録失敗（`hotkey-register-failed` / `overlay-hotkey-register-failed`）が発生している。
- 解決策:
  - 設定画面の `権限` タブから `Input Monitoring` を許可し、アプリを再起動する。
  - バイナリ配置を変更した場合は macOS 側で権限が剥がれるため、権限を再付与する。
  - ログで EventTap 失敗メッセージが出ていないか確認する。

## 3. キー操作がアクティブアプリへ伝搬してしまう
- 事象:
  - オーバーレイ中のキー入力が背面アプリにも入力される。
- 主な原因:
  - キー抑止フラグ (`SuppressKeyPropagation`) が有効にできていない。
  - そもそも EventTap が有効でなく、抑止処理まで到達していない。
- 解決策:
  - まず `Input Monitoring` の許可状態を確認する（未許可だと抑止不可）。
  - セッションを一度終了して再開始し、改善しなければアプリ再起動。
  - 再発時はログと権限状態をセットで記録し、`hotkey-register-failed` の有無を確認する。

## 4. このブランチで実際に発生した複合不具合と修正
- 発生事象:
  - セッション開始してもオーバーレイ/ラベルが出ない（即終了する）。
  - プレビューの見た目と実ラベルサイズが乖離する。
- 原因:
  - セッション開始条件を厳しくしすぎたため、`Input Monitoring` 未許可や候補 0 件で開始直後に終了していた。
  - プレビュー側は `Viewbox` で縮尺補正され、さらに `LabelScale` を幅/高さにも乗算していたため、実サイズと一致しなかった。
- 修正内容:
  - `ScreenSearchOrchestrator.StartSession()` で権限不足時に即終了しないよう修正し、開始継続 + 診断ログ記録に変更。
  - 候補 0 件で即終了しないようにして、オーバーレイ表示経路を維持。
  - セッション中のホットキー再確保失敗時は即終了せず、伝搬抑止のみ解除して継続。
  - プレビューから `Viewbox` を外し、幅/高さ/フォントサイズ計算を実オーバーレイと共通ロジック化して整合を取った。
  - 表示スケール(DPI)を考慮したサイズ換算に変更し、プレビューが実表示より極端に大きく見える問題を解消。
  - 追加ログ:
    - `session-start-without-accessibility-permission`
    - `overlay-input-unavailable: continue-without-suppression`

## 5. Finder だけキーが伝搬する / ブラウザでラベルが3件固定になる
- 発生事象:
  - Finder 前面時のみ、オーバーレイ表示後のラベル入力が Finder 側へ流れ、オーバーレイ入力が進まない。
  - Chrome / Safari 等で、ラベルが最小化・最大化・閉じるの 3 件付近に偏る。
- 原因:
  - `MacHotkeyService.OnEvent()` でホットキーイベント内から `StartSession()` が同期実行され、重い解析時に EventTap が無効化されるケースがあった。
  - `MacAccessibilityCandidateProvider` の候補化条件が厳しく、`AXChildren` + `AXPosition/AXSize` + `AXPress` 前提になっていたため、ブラウザで露出する `AXVisibleChildren` / `AXContents` / `AXFrame` 系の要素を取りこぼしていた。
- 修正内容:
  - ホットキー一致時のハンドラ呼び出しを ThreadPool へオフロードし、EventTap コールバックを即時復帰させる。
  - `kCGEventTapDisabledByTimeout` / `kCGEventTapDisabledByUserInput` を検知したら `CGEventTapEnable(..., true)` で再有効化する。
  - `SuppressKeyPropagation` を `Volatile.Read/Write` 化して、抑止フラグのスレッド間可視性を担保する。
  - `AXEnhancedUserInterface=true` を安全に設定する。
  - 子要素探索を `AXChildren` に加えて `AXVisibleChildren` / `AXContents` まで拡張する。
  - 座標取得で `AXPosition/AXSize` が取れない場合に `AXFrame` をフォールバック参照する。
  - ブラウザプロセス時は、ブラウザ系ロール・アクションあり要素も候補化対象に含める。

## 6. 初回のみレインボー枠がズレる / ESCでクラッシュする
- 発生事象:
  - アプリ起動後の初回セッションだけ、ラベル表示直前のレインボー枠が下方向へズレる。
  - 本表示（ラベル表示）に切り替わると位置が戻る。
  - オーバーレイ表示中に `ESC` 押下でクラッシュすることがある。
- 原因:
  - 初回 `Show()` 後に、古い `OverlayViewState`（準備中）を `Dispatcher.Post` で再描画してしまい、最新状態と競合していた。
  - 準備中オーバーレイが `ActiveMonitor` 固定で計算され、本表示が `DefaultAnalysisTarget`（`ActiveWindow` を含む）で計算されるため、初回だけ表示領域が不一致になっていた。
  - EventTap スレッド（非UIスレッド）で `OverlayWindow.IsVisible` を参照し、UIスレッド境界違反を起こしていた。
- 修正内容:
  - `StartSession()` 開始時に `targetForSession` を固定し、準備中/本表示とも同じターゲットで `TargetBounds` を計算する。
  - 初回 `Show()` 後は、キャプチャ済みの古い `state` ではなく `_latestOverlayState` を使って再描画する。
  - グローバルキー処理で UI コントロール参照を行わず、`SessionState` のみで `ESC` を処理する。
  - プライマリディスプレイ上では枠の上マージンを固定値にし、メニューバー（トレイ）への被りを回避する。
- 再発防止チェック:
  - 準備中オーバーレイと本表示オーバーレイで、`Target` / `TargetBounds` / `Display` の算出元が一致していること。
  - `Dispatcher.Post` で UI を再描画する場合、クロージャに古い `state` を閉じ込めないこと（最新状態を参照すること）。
  - EventTap / ThreadPool 側コードから `Window` など UI オブジェクトを直接参照しないこと。
  - 手動確認で「起動直後の初回セッション」だけを必ず含めること（2回目以降だけで合格にしない）。

## 運用メモ
- 権限関連の不具合は「権限再取得 → アプリ再起動」で解消するケースが多い。
- 再発報告時は以下を必ず添付する。
  - 権限タブの 3 項目状態（Accessibility / Input Monitoring / Screen Recording）
  - `hitascreen.log` の直近 100 行
  - `settings.json` の `ExcludedAxRoles` 値
