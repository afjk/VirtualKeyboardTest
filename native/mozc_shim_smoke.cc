// Host smoke test for the C API: loads mozc.data from a file (the same path
// the Android build takes), types romaji, converts and commits.
//
// Usage: mozc_shim_smoke <profile_dir> <mozc.data>

#include <chrono>
#include <cstdio>
#include <string>

#include "google/protobuf/util/json_util.h"
#include "android/jni/mozc_shim.h"
#include "protocol/commands.pb.h"

namespace {

using Clock = std::chrono::steady_clock;

double Ms(Clock::time_point a, Clock::time_point b) {
  return std::chrono::duration<double, std::milli>(b - a).count();
}

bool Eval(const std::string& json, mozc::commands::Command* out) {
  char* result = mozc_shim_eval(json.c_str());
  if (result == nullptr) {
    std::printf("eval failed: %s\n  input: %s\n", mozc_shim_last_error(),
                json.c_str());
    return false;
  }
  google::protobuf::util::JsonParseOptions options;
  options.ignore_unknown_fields = true;
  const bool ok =
      google::protobuf::util::JsonStringToMessage(result, out, options).ok();
  mozc_shim_free(result);
  if (!ok) std::printf("could not parse output json\n");
  return ok;
}

std::string Preedit(const mozc::commands::Command& c) {
  std::string s;
  for (const auto& segment : c.output().preedit().segment()) s += segment.value();
  return s;
}

int failures = 0;
void Expect(bool condition, const std::string& what) {
  std::printf("[%s] %s\n", condition ? "OK" : "NG", what.c_str());
  if (!condition) ++failures;
}

}  // namespace

int main(int argc, char** argv) {
  if (argc != 3) {
    std::printf("usage: %s <profile_dir> <mozc.data>\n", argv[0]);
    return 2;
  }
  const auto t0 = Clock::now();
  if (!mozc_shim_initialize(argv[1], argv[2])) {
    std::printf("initialize failed: %s\n", mozc_shim_last_error());
    return 1;
  }
  const auto t1 = Clock::now();
  std::printf("initialize: %.1f ms, data version %s\n", Ms(t0, t1),
              mozc_shim_data_version());

  mozc::commands::Command c;
  // Same configuration as the Unity wrapper (MozcSession.InitializeEngine).
  Eval(R"({"input":{"type":"SET_CONFIG","config":{"sessionKeymap":"MSIME","preeditMethod":"ROMAN"}}})",
       &c);
  if (!Eval(R"({"input":{"type":"CREATE_SESSION"}})", &c)) return 1;
  const std::string id = std::to_string(c.output().id());
  Expect(c.output().id() != 0, "CREATE_SESSION returns a session id");

  auto key = [&](const std::string& key_event) {
    return Eval(R"({"input":{"type":"SEND_KEY","id":")" + id +
                    R"(","key":)" + key_event + "}}",
                &c);
  };

  key(R"({"specialKey":"ON"})");
  const auto t2 = Clock::now();
  for (char ch : std::string("nihongo")) {
    key(R"({"keyCode":)" + std::to_string(static_cast<int>(ch)) + "}");
  }
  const auto t3 = Clock::now();
  Expect(Preedit(c) == "にほんご", "romaji nihongo -> preedit にほんご (got " +
                                       Preedit(c) + ")");

  key(R"({"specialKey":"SPACE"})");
  const auto t4 = Clock::now();
  bool has_nihongo = false;
  for (const auto& candidate : c.output().candidate_window().candidate()) {
    has_nihongo |= candidate.value() == "日本語";
  }
  // The candidate window may open only on the second conversion key; the
  // first SPACE already converts the preedit.
  Expect(Preedit(c) == "日本語" || has_nihongo,
         "SPACE converts to 日本語 (preedit " + Preedit(c) + ")");

  key(R"({"specialKey":"ENTER"})");
  Expect(c.output().result().value() == "日本語",
         "ENTER commits 日本語 (got " + c.output().result().value() + ")");

  std::printf("typing 7 keys: %.2f ms total, conversion: %.2f ms\n",
              Ms(t2, t3), Ms(t3, t4));

  // Candidate selection, as in the Unity sample: the first choice for
  // かんじへんかん is 感じ変換 with the OSS dictionary, so pick 漢字.
  for (char ch : std::string("kannjihenkann")) {
    key(R"({"keyCode":)" + std::to_string(static_cast<int>(ch)) + "}");
  }
  Expect(Preedit(c) == "かんじへんかん", "preedit かんじへんかん (got " + Preedit(c) + ")");
  key(R"({"specialKey":"SPACE"})");
  std::printf("first choice: %s\n", Preedit(c).c_str());
  key(R"({"specialKey":"SPACE"})");  // Next candidate; opens the list.
  int kanji = -1;
  std::string listed;
  for (const auto& candidate : c.output().candidate_window().candidate()) {
    if (listed.size() < 120) listed += candidate.value() + " ";
    if (candidate.value() == "漢字" && kanji < 0) kanji = candidate.id();
  }
  std::printf("candidates: %s\n", listed.c_str());
  Expect(kanji >= 0, "漢字 offered for the first segment");
  if (kanji >= 0) {
    Eval(R"({"input":{"type":"SEND_COMMAND","id":")" + id +
             R"(","command":{"type":"SELECT_CANDIDATE","id":)" +
             std::to_string(kanji) + "}}}",
         &c);
  }
  key(R"({"specialKey":"ENTER"})");
  const std::string committed = c.output().result().value();
  Expect(committed.rfind("漢字", 0) == 0, "commit starts with 漢字 (got " + committed + ")");

  Eval(R"({"input":{"type":"DELETE_SESSION","id":")" + id + R"("}})", &c);
  if (failures == 0) {
    std::printf("[ PASSED ]\n");
  } else {
    std::printf("[ FAILED ] %d\n", failures);
  }
  return failures == 0 ? 0 : 1;
}
