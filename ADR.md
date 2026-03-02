# ADR: HitAScreen アーキテクチャ決定記録

- ステータス: Accepted
- 日付: 2026-03-02
- 対象: macOS 実装（将来 Windows 拡張を前提）

## 1. 背景
`requiement.md` では、常駐/ホットキー/コンテキスト取得/オーバーレイ操作を低遅延かつ低負荷で実現することが求められている。
また、将来 Windows を同一コードベースで対応するため、OS 依存実装の分離が必要である。

## 2. 採用アーキテクチャ
レイヤード構成を採用する。

- `HitAScreen.App`
  - Avalonia UI（トレイ、設定画面、オーバーレイ）
  - Core のイベント購読と表示反映
- `HitAScreen.Core`
  - セッション状態遷移（Idle -> CaptureContext -> Analyze -> OverlayActive -> ExecuteAction -> End）
  - ラベル生成、抑制判定、実行オーケストレーション
- `HitAScreen.Platform.Abstractions`
  - OS 非依存インターフェース（`IHotkeyService` 等）
- `HitAScreen.Platform.MacOS`
  - EventTap、Accessibility API、CGEvent などの P/Invoke 実装
- `HitAScreen.Infrastructure`
  - 設定永続化（JSON）、ログ

## 3. 主要な設計判断

### 3.1 OS 依存処理は Abstractions 経由に限定
- 決定:
  - Core は `Platform.Abstractions` の型/IF のみ依存する。
- 理由:
  - 将来 Windows 実装を追加する際、Core/UI の変更を最小化するため。

### 3.2 入力制御は EventTap 中心、抑制時は登録解除
- 決定:
  - グローバルホットキー監視は EventTap で実装し、抑制時は `Unregister()` する。
- 理由:
  - 「ブロックし続ける」方式より入力遅延のリスクを下げるため。

### 3.3 オーバーレイは非アクティブ・クリック透過
- 決定:
  - オーバーレイ表示時に前面アプリを奪わない。
  - クリック透過（ヒットテスト無効 + macOS 側 ignoresMouseEvents）とする。
- 理由:
  - 対象アプリへのクリック注入成功率と操作体験を優先するため。

### 3.4 候補抽出は当面 Accessibility のみ
- 決定:
  - 候補抽出は FR-5.1（Accessibility）に限定。
- 理由:
  - 今回スコープから OCR/画像検出を除外するため。

### 3.5 設定/診断は永続化を最小限にする
- 決定:
  - 設定は JSON 永続化、機微なコンテキスト情報は既定で永続化しない。
- 理由:
  - 要件のプライバシー制約（FR-3.3、NFR-4）を満たすため。

## 4. データフロー
1. `IHotkeyService` が起動ホットキー検知
2. Core が `IActiveWindowService` で前面文脈を取得（失敗時フォールバック）
3. Core が `IAccessibilityElementProvider` で候補抽出
4. Core がラベル生成し `OverlayViewState` を UI に通知
5. 入力確定で Core が `IInputInjectionService` を呼び出し操作実行

## 5. 非採用案
- UI から直接 P/Invoke を呼ぶ構成
  - 理由: 責務が肥大化し、Windows 対応時の差し替えコストが高い。
- 常時キーボードフック + 条件分岐で抑制
  - 理由: 入力遅延リスクが高い。
- OCR/画像検出の同時導入
  - 理由: 現時点スコープ外であり、安定化を優先する。

## 6. 影響と今後
- 影響:
  - レイヤー分離により保守性と移植性が向上。
  - 一方で、IF 設計と実装の同期コストが発生。
- 今後:
  - `TASK.md` の Phase A〜D を順に実施。
  - OCR/画像検出は別 ADR で再評価する。
