# NFR 計測レポート（2026-03-08）

## 計測条件
- 実行コマンド: `/usr/bin/time -l dotnet test tests/HitAScreen.Core.Tests/HitAScreen.Core.Tests.csproj`
- 追加安定性確認: `dotnet test` を 5 回連続実行
- 実行環境: ローカル CLI

## 計測結果
- テスト実行時間（wall clock）: 8.15 秒
- テストスイート時間（VSTest 表示）: 69ms
- 最大常駐メモリ（maximum resident set size）: 216,842,240 bytes
- 連続実行安定性: 5/5 回成功
  - run1: 99ms
  - run2: 118ms
  - run3: 94ms
  - run4: 97ms
  - run5: 115ms

## 判定
- 安定性: 合格（連続実行で失敗なし）
- メモリ傾向: 合格（単体テスト範囲で急増なし）
- 表示遅延/操作遅延: 未計測（GUI 実機計測が必要）

## 補足
- 本レポートは自動テスト可能範囲のみを対象とする。
- オーバーレイ描画遅延など GUI 依存の NFR は手動テスト実施時に追記する。
