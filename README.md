# VirtualKeyboardTest

日本語変換ができる Unity 用のソフトウェアキーボードです。ローマ字で入力し、
[Google Mozc](https://github.com/google/mozc) でかな漢字に変換します。
変換はすべて端末内で行い、ネットワークを使いません。

- 対応: Android arm64、Linux x64（Editor・Player）。Mozc の動作は XREAL AURA（Android 14）で確認済み
- 未対応: macOS / Windows / iOS（ネイティブライブラリ未作成）
- Unity 2021.3 以降、IMGUI のみ（追加パッケージ不要）

## 使い方

1. [Releases](https://github.com/afjk/VirtualKeyboardTest/releases) の `mozc-unity-assets-*.zip`
   （または Actions の成果物 `mozc-unity-assets`）を展開し、`Assets/` をプロジェクトにコピーする。
2. GameObject に `MozcSoftwareKeyboard` を追加する。起動時に辞書（約 19 MB）を準備し、
   準備ができるまでは英数入力だけで動く。
3. 入力結果は `Buffer` から読む。

```csharp
var keyboard = gameObject.AddComponent<MozcSoftwareKeyboard>();
keyboard.multiLine = false;                       // ENTER で送信
keyboard.BufferReplaced += buffer =>              // 辞書の準備ができたとき
    buffer.Submitted += text => Debug.Log(text);
```

| キー | 動作 |
|---|---|
| 英数 / かな | 入力モードを切り替える（変換中の文字はそのまま確定） |
| ⇧ | 次の 1 文字を大文字にする |
| 変換 / 空白 | 変換・次の候補。何も入力していなければ空白 |
| 候補 | タップで選択 |
| 確定 / 改行 / 送信 | 変換中なら確定。そうでなければ改行、または `Submitted` |
| BS、← → | 1 文字削除、文節の移動 |

ハードウェアキーボードの文字キー、Space、Enter、Backspace、Esc、矢印キーも使えます。

表示を自作する場合は、キー入力の処理だけを持つ `JapaneseKeyboardBuffer` を直接使えます
（`Type`、`Space`、`Enter`、`Backspace`、`SelectCandidate`、`ToggleMode`、`Composition` の
候補・文節）。変換エンジンは `IKanaKanjiConverter` で差し替えられます。

| ファイル | 役割 |
|---|---|
| `Runtime/MozcSoftwareKeyboard.cs` | 画面キーボード（IMGUI）。辞書の準備とハードウェアキー |
| `Runtime/JapaneseKeyboardBuffer.cs` | 表示に依存しない入力状態（確定文字列、変換中、モード） |
| `Runtime/MozcSession.cs`、`MozcNative.cs` | Mozc の C API の呼び出し |
| `Runtime/MozcDataInstaller.cs` | APK 内の辞書を初回だけ端末に展開 |
| `Samples/MozcKeyboardDemo.cs` | キーボードを置くだけのサンプル |
| `Samples/MozcSmokeTest.cs` | 実機の速度・メモリ確認（logcat に出力） |

日本語を表示するフォントは、OS の Noto Sans CJK JP などを自動で使います。

## Mozc Android ビルド

[Google Mozc](https://github.com/google/mozc) を、Unity から呼べる小さな C API
（`native/mozc_shim.h`）付きで Android arm64 と Linux x64 向けにビルドし、
GitHub Actions の成果物として配布します。Mozc のソースは `MOZC_COMMIT` で固定した
コミットをビルド時に取得します。このリポジトリに Mozc のソースやバイナリは含みません。

### C API

コマンドと応答は `mozc.commands.Command` の protobuf JSON です（フィールド名は
lowerCamelCase、enum は名前、64 bit 整数は文字列）。呼び出し側に protobuf ランタイムも、
Mozc 公式 JNI が要求する固定の Java クラス名も不要です。

```c
int   mozc_shim_initialize(const char* profile_dir, const char* data_file_path);
char* mozc_shim_eval(const char* command_json);   // mozc_shim_free で解放
void  mozc_shim_free(char*);
const char* mozc_shim_data_version(void);
const char* mozc_shim_last_error(void);
```

Mozc の `//session:session_handler` は `//android/jni` からしか参照できないため、
`scripts/add-shim.sh` が `native/` のファイルを Mozc の `src/android/jni/` にコピーして
ビルド定義を追記します。辞書ファイルを読めないときは、公式 JNI のように辞書なしの
エンジンへ切り替えず、初期化を失敗させます。

### 成果物

| artifact | 内容 |
|---|---|
| `mozc-unity-assets` | Unity プロジェクトにそのままコピーする `Assets/`（C# ラッパー、確認用サンプル、ライブラリ、`StreamingAssets/mozc/mozc.data`、ライセンス） |
| `mozc-shim-libs` | `arm64-v8a/libmozc_shim.so`、`x86_64/libmozc_shim.so`（どちらも strip 済み）、`mozc.data`、`licenses/`、`verify.txt`、`BUILD_INFO.txt`、`SHA256SUMS` |
| `mozc-shim-symbols` | strip 前の Android ライブラリ（クラッシュ解析用） |
| `smoke-test-log` | Linux でのスモークテスト出力 |

`v` で始まるタグを push すると、同じファイルを GitHub Release に添付します（Actions の成果物は 90 日で消えるため）。

### ワークフローで確認していること

- Linux で `test/smoke_scenario.txt`（Mozc 公式のシナリオ形式）が通ること
- Linux で C API から `mozc.data` をファイルとして読み込み、`nihongo` → `日本語` の確定、
  `kannjihenkann` の候補一覧から `漢字` を選んで確定できること（Unity サンプルと同じ手順）
- Android ライブラリが aarch64 で、LOAD セグメントが 16 KB 整列であること
- 両ライブラリが C API の 5 関数を公開していること
- Unity 用 C# が UnityEngine のスタブに対してコンパイルできること（Unity でのビルドではない）
- `JapaneseKeyboardBuffer` の動作（変換・確定・削除・モード切替・送信）を模擬エンジンで確認すること

Unity での IL2CPP ビルドと Android 実機での動作は、手元で XREAL AURA を使って確認しました（下記）。
Quest 実機、Unity Editor での読み込みは確認していません。

### Quest 3 での確認手順

1. Unity 2021.3 以降で空のプロジェクトを作り、Android（IL2CPP、ARM64）に切り替える。
2. `mozc-unity-assets` を展開し、`Assets/` をプロジェクトに上書きコピーする。
3. 空のシーンに GameObject を置き、`MozcSmokeTest` を追加してビルド・実行する。
4. `adb logcat -s Unity | grep MozcSmoke` で、データ展開・初期化時間、1 キーあたりの時間、
   変換時間、常駐メモリ増分、`[ PASSED ]` / `[ FAILED ]` を確認する。

### XREAL AURA での確認結果（2026-11-27）

Unity 6000.3.20f1、IL2CPP、ARM64、最小 API 29 の 2D アプリとしてビルドし、
XREAL Android XR Puck（Android 14 / API 34）で `MozcSmokeTest` を実行しました。
.meta、DllNotFoundException、`mozc.data` の展開、IL2CPP のマーシャリングに問題はなく、
初回の起動から `[ PASSED ]` でした。

| 項目 | 初回起動 | 2 回目 |
|---|---|---|
| データ展開（19 MB） | 49 ms | 33 ms（コピー済みのものを再利用） |
| 初期化 | 15 ms | 15 ms |
| RSS の増分 | +13.0 MB | +8.4 MB |
| nihongo | 0.70 ms/キー、変換 0.40 ms | 0.76 ms/キー、変換 0.62 ms |
| kannjihenkann | 0.92 ms/キー、変換 2.44 ms | 1.02 ms/キー、変換 4.11 ms |

2 回目は学習により `かんじへんかん` の第一候補が `漢字変換` に変わります
（テストは候補 id で `漢字` を選ぶため結果は同じ）。

Linux の Unity Editor でも `x86_64/libmozc_shim.so` で同じサンプルを再生できます
（macOS 用ライブラリは未作成）。

### OSS 辞書について

`かんじへんかん` の第一候補は `感じ変換` でした（2026-11-26 確認）。
候補一覧からの選択は必須の操作になります。

### ライセンス

このリポジトリのスクリプトとコードは BSD 3-Clause License です（`LICENSE`）。
成果物には Mozc（BSD 3-Clause）、辞書データ（IPAdic 由来の NAIST 条項ほか）、
用例辞書（BSD 2-Clause）、Abseil（Apache-2.0）、Protocol Buffers（BSD 3-Clause）、
zlib などが含まれます。配布時は `licenses/` の表示を同梱してください。
