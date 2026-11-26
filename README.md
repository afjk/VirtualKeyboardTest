# VirtualKeyboardTest

## Mozc Android ビルド

[Google Mozc](https://github.com/google/mozc) の公式 Android ターゲット
（`//android/jni:mozc.arm64`）と OSS 辞書データ（`mozc.data`）を
GitHub Actions でビルドし、成果物として配布するためのリポジトリです。
Mozc のソースは `MOZC_COMMIT` で固定したコミットをビルド時に取得します。
このリポジトリに Mozc のソースやバイナリは含みません。

### 成果物（Actions の artifact `mozc-android-arm64`）

| ファイル | 内容 |
|---|---|
| `arm64-v8a/libmozc.so` | Android arm64-v8a 用 Mozc 本体（JNI、16 KB ページ整列） |
| `mozc.data` | OSS 辞書データ。`onPostLoad` に渡すデータファイル |
| `licenses/` | Mozc・辞書・同梱ライブラリのライセンス表示 |
| `verify.txt` | ELF 種別、依存ライブラリ、LOAD 整列、JNI 入口の確認結果 |
| `BUILD_INFO.txt` / `SHA256SUMS` | Mozc コミット、Bazel・NDK 版、サイズ、ハッシュ |

### ワークフローで確認していること

- `libmozc.so` が aarch64 の ELF で、LOAD セグメントが 16 KB 整列であること
- JNI 入口 `Java_com_google_android_apps_inputmethod_libs_mozc_session_MozcJni_initialize` が公開されていること
- Linux 上で同じコミットの Mozc と OSS 辞書を使い、`test/smoke_scenario.txt`
  （`nihongo` → `にほんご` → `日本語`、`kannjihenkann` → `漢字変換`）が通ること

Android 実機での動作（`mozc.data` の読み込み、変換速度、メモリ）はこのワークフローでは確認していません。

### JNI の呼び出し方（Mozc 側の仕様）

`libmozc.so` は Java クラス
`com.google.android.apps.inputmethod.libs.mozc.session.MozcJni` の static native
メソッドとして次を登録します（`initialize()` を最初に呼ぶ）。

- `boolean onPostLoad(String userProfileDirectory, String dataFilePath)`
- `byte[] evalCommand(byte[] command)` — `mozc.commands.Command` protobuf のシリアライズ
- `String getDataVersion()`

### ローカルでのビルド

```sh
scripts/fetch-mozc.sh
cd mozc/src
python3 build_tools/update_deps.py --noqt --nollvm --nomsys2 --nowix
bazelisk build //android/jni:mozc.arm64 --config oss_android --config release_build
bazelisk build //data_manager/oss:mozc_dataset_for_oss //session:session_handler_main
cd ../..
scripts/package.sh
```

### ライセンス

このリポジトリのスクリプトは BSD 3-Clause License です（`LICENSE`）。
ビルドした成果物には Mozc（BSD 3-Clause）、辞書データ（IPAdic 由来の NAIST 条項ほか、
`licenses/` 参照）、Abseil（Apache-2.0）、Protocol Buffers（BSD 3-Clause）などが含まれます。
配布時は `licenses/` の表示を同梱してください。
