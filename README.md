# VirtualKeyboardTest

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

### ワークフローで確認していること

- Linux で `test/smoke_scenario.txt`（Mozc 公式のシナリオ形式）が通ること
- Linux で C API から `mozc.data` をファイルとして読み込み、`nihongo` → `日本語` の確定、
  `kannjihenkann` の候補一覧から `漢字` を選んで確定できること（Unity サンプルと同じ手順）
- Android ライブラリが aarch64 で、LOAD セグメントが 16 KB 整列であること
- 両ライブラリが C API の 5 関数を公開していること
- Unity 用 C# が UnityEngine のスタブに対してコンパイルできること（Unity でのビルドではない）

Quest 実機での動作、Unity Editor での読み込み、IL2CPP ビルドは確認していません。

### Quest 3 での確認手順

1. Unity 2021.3 以降で空のプロジェクトを作り、Android（IL2CPP、ARM64）に切り替える。
2. `mozc-unity-assets` を展開し、`Assets/` をプロジェクトに上書きコピーする。
3. 空のシーンに GameObject を置き、`MozcSmokeTest` を追加してビルド・実行する。
4. `adb logcat -s Unity | grep MozcSmoke` で、データ展開・初期化時間、1 キーあたりの時間、
   変換時間、常駐メモリ増分、`[ PASSED ]` / `[ FAILED ]` を確認する。

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
