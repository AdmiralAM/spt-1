from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "tools" / "package_spt413_exact_candidate.ps1"


class ExactRuntimeHandoffContractTests(unittest.TestCase):
    def test_wrapper_exists_and_calls_exact_runtime_builder(self):
        text = SCRIPT.read_text(encoding="utf-8")
        self.assertIn("build_spt413_test_candidate.ps1", text)
        self.assertIn("-ExpectedHeadSha", text)
        self.assertIn("exact-installed-runtime", text)

    def test_wrapper_rejects_published_api_provenance(self):
        text = SCRIPT.read_text(encoding="utf-8")
        self.assertIn("publishedApiCoreSha256", text)
        self.assertIn("Refusing to package non-exact candidate", text)
        self.assertIn("physicalRuntimeEvidenceEligible", text)
        self.assertIn("runtimeCoreSha256", text)

    def test_wrapper_revalidates_runtime_fatal_questassort_contract(self):
        text = SCRIPT.read_text(encoding="utf-8")
        for key in ("started", "success", "fail"):
            self.assertIn(key, text)
        self.assertIn("exactly seven success unlock mappings", text)

    def test_wrapper_emits_single_obvious_exact_head_zip_and_checksum(self):
        text = SCRIPT.read_text(encoding="utf-8")
        self.assertIn('Admiral-Trader-SPT413-$sourceHead.zip', text)
        self.assertIn("Compress-Archive", text)
        self.assertIn("SPT_Runtime/user/mods/Admiral-Trader", text)
        self.assertIn("SHA-256", text)


if __name__ == "__main__":
    unittest.main()
