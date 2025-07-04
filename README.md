# AMIOGamePadTest

**AMIOGamePadTest**は、USB接続のカスタムゲームパッドデバイス「AMIO GamePad」をPC上でテストするための**C#製Windowsアプリケーション**です。

---

## 目的

このツールは、AMIO GamePadがPCに正しく認識され、HID通信によるボタンやスティック入力の取得、およびLED制御コマンドの送信などが期待通りに動作するかどうかを**簡単にテスト・検証する**ことを目的としています。

---

## 主な特徴・技術スタック

- **言語・環境**:  
  - C# (.NET Framework)
  - Windows デスクトップアプリ（WPF）
- **主な機能**:
  - AMIO GamePad（USB HIDデバイス）の自動検出・接続
  - ボタン・アナログスティック入力のリアルタイム表示
  - HIDアウトレポート送信によるLEDなどデバイス制御
  - ログ表示、状態の可視化
- **HID通信ライブラリ**:
  - [HidSharp](https://github.com/mikeobrien/HidSharp) を使用  
    - クロスプラットフォーム対応の.NET用HID通信ライブラリ
    - NuGetから簡単にインストール可能

---

## 動作概要

1. AMIO GamePadデバイスをUSBでPCに接続
2. アプリを起動すると、HidSharp経由で自動的にデバイスを検出
3. ボタン・スティック入力が**リアルタイムで画面表示**
4. LED制御コマンドなどの送信機能も利用可能

---

## ビルド・実行方法

1. Visual Studioで `AMIOGamePadTest.sln` を開く
2. NuGetで `HidSharp` パッケージを復元（通常は自動でOK）
3. ビルドし、アプリを実行

---

## 注意

- このリポジトリには**AMIO GamePad本体のファームウェアやSTM32関連コードは含まれていません**。
- 別途、AMIO GamePadのハードウェア本体と、対応ファームウェアが必要です。

---

## ライセンス

本プロジェクトのライセンスは `LICENSE` ファイルをご参照ください。

---
