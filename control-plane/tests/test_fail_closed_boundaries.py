from __future__ import annotations

import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time
import unittest
from unittest.mock import patch

from forgepilot.process import PilotError, _process_group_options, run_command_stream
from forgepilot.protocol import (
    validate_executor,
    validate_plan,
    validate_review,
    write_normalized_json,
)
from forgepilot.publication import (
    changed_paths,
    enforce_allowed_paths,
    stage_explicit_paths,
    staged_paths,
    working_tree_paths,
)


def valid_plan(**updates: object) -> dict[str, object]:
    payload: dict[str, object] = {
        "task": "lot borné",
        "scope": "un fichier",
        "acceptance_criteria": ["le comportement est prouvé"],
        "files_to_read": ["CLAUDE.md"],
        "files_allowed_to_change": ["allowed/**"],
        "checks": ["test ciblé"],
        "risks": ["régression"],
        "blocked": False,
    }
    payload.update(updates)
    return payload


def valid_review(**updates: object) -> dict[str, object]:
    payload: dict[str, object] = {
        "verdict": "PASS",
        "acceptance_criteria": [
            {
                "criterion": "le comportement est prouvé",
                "status": "PASS",
                "evidence": "test rouge puis vert",
            }
        ],
        "findings": [],
        "checks_observed": [
            {"check": "test ciblé", "status": "PASS", "evidence": "code 0"}
        ],
        "human_decision_required": True,
    }
    payload.update(updates)
    return payload


def valid_executor(**updates: object) -> dict[str, object]:
    payload: dict[str, object] = {
        "summary": "correction appliquée",
        "files_modified": ["allowed/change.txt"],
        "checks": [{"check": "test ciblé", "status": "PASS", "evidence": "code 0"}],
        "blockages": [],
    }
    payload.update(updates)
    return payload


class GitRepoMixin:
    def init_repo(self, repo: Path) -> str:
        subprocess.run(
            ["git", "init", "-b", "main"], cwd=repo, check=True, capture_output=True
        )
        subprocess.run(["git", "config", "user.email", "test@example.invalid"], cwd=repo, check=True)
        subprocess.run(["git", "config", "user.name", "ForgePilot Test"], cwd=repo, check=True)
        (repo / "forbidden").mkdir()
        (repo / "forbidden" / "old.txt").write_text("base\n", encoding="utf-8")
        subprocess.run(["git", "add", "--all"], cwd=repo, check=True)
        subprocess.run(["git", "commit", "-m", "base"], cwd=repo, check=True, capture_output=True)
        return subprocess.run(
            ["git", "rev-parse", "HEAD"],
            cwd=repo,
            check=True,
            capture_output=True,
            text=True,
        ).stdout.strip()


class PublicationFailClosedTests(unittest.TestCase, GitRepoMixin):
    def test_staged_rename_exposes_both_sides_and_scope_refuses_source(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            self.init_repo(repo)
            (repo / "allowed").mkdir()
            subprocess.run(
                ["git", "mv", "forbidden/old.txt", "allowed/new.txt"], cwd=repo, check=True
            )

            self.assertEqual(
                ["allowed/new.txt", "forbidden/old.txt"], working_tree_paths(repo)
            )
            self.assertEqual(
                ["allowed/new.txt", "forbidden/old.txt"], staged_paths(repo)
            )
            with self.assertRaisesRegex(PilotError, "hors files_allowed_to_change"):
                enforce_allowed_paths(working_tree_paths(repo), ["allowed/**"])

    def test_committed_rename_exposes_both_sides(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            base = self.init_repo(repo)
            (repo / "allowed").mkdir()
            subprocess.run(
                ["git", "mv", "forbidden/old.txt", "allowed/new.txt"], cwd=repo, check=True
            )
            subprocess.run(["git", "commit", "-m", "rename"], cwd=repo, check=True, capture_output=True)
            self.assertEqual(
                ["allowed/new.txt", "forbidden/old.txt"], changed_paths(repo, base)
            )

    def test_staging_uses_literal_pathspec(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            self.init_repo(repo)
            (repo / "allowed").mkdir()
            (repo / "allowed" / "[x].txt").write_text("literal\n", encoding="utf-8")
            (repo / "allowed" / "x.txt").write_text("glob target\n", encoding="utf-8")

            stage_explicit_paths(repo, ["allowed/[x].txt"])
            self.assertEqual(["allowed/[x].txt"], staged_paths(repo))

    def test_staging_force_adds_an_explicit_ignored_proof(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            self.init_repo(repo)
            (repo / ".gitignore").write_text("artifacts/\n", encoding="utf-8")
            (repo / "artifacts").mkdir()
            (repo / "artifacts" / "proof.json").write_text("{}\n", encoding="utf-8")

            stage_explicit_paths(repo, ["artifacts/proof.json"])

            self.assertEqual(["artifacts/proof.json"], staged_paths(repo))

    def test_staging_revalidates_preexisting_index(self):
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            self.init_repo(repo)
            (repo / "allowed").mkdir()
            (repo / "allowed" / "ok.txt").write_text("ok\n", encoding="utf-8")
            (repo / "forbidden" / "extra.txt").write_text("no\n", encoding="utf-8")
            subprocess.run(
                ["git", "add", "--", "forbidden/extra.txt"], cwd=repo, check=True
            )
            with self.assertRaisesRegex(PilotError, "index final"):
                stage_explicit_paths(repo, ["allowed/ok.txt"])


class ProtocolFailClosedTests(unittest.TestCase):
    def test_plan_is_closed_and_requires_a_useful_glob_prefix(self):
        with self.assertRaisesRegex(PilotError, "champs non autorisés"):
            validate_plan(valid_plan(echo="surprise"))
        for pattern in ("**/**", "*/**", "[ab]/**", "**/*.md"):
            with self.subTest(pattern=pattern), self.assertRaisesRegex(
                PilotError, "universel|sans préfixe"
            ):
                validate_plan(valid_plan(files_allowed_to_change=[pattern]))
        self.assertEqual(
            ["allowed/**"], validate_plan(valid_plan())["files_allowed_to_change"]
        )

    def test_review_pass_is_nonempty_structured_and_coherent(self):
        expected = ["le comportement est prouvé"]
        self.assertEqual(
            "PASS", validate_review(valid_review(), expected_criteria=expected)["verdict"]
        )
        invalid = (
            valid_review(acceptance_criteria=[]),
            valid_review(checks_observed=[]),
            valid_review(human_decision_required=False),
            valid_review(extra="surprise"),
            valid_review(
                acceptance_criteria=[
                    {"criterion": expected[0], "status": "FAIL", "evidence": "cassé"}
                ]
            ),
            valid_review(
                findings=[
                    {
                        "id": "F1",
                        "path": "allowed/change.txt",
                        "issue": "cassé",
                        "evidence": "reproduction",
                    }
                ]
            ),
        )
        for payload in invalid:
            with self.subTest(payload=payload), self.assertRaises(PilotError):
                validate_review(payload, expected_criteria=expected)

    def test_review_rejects_unstructured_or_incomplete_results(self):
        with self.assertRaises(PilotError):
            validate_review(valid_review(checks_observed=["test ciblé"]))
        with self.assertRaisesRegex(PilotError, "correspondent pas"):
            validate_review(valid_review(), expected_criteria=["autre critère"])
        with self.assertRaisesRegex(PilotError, "aucun constat"):
            validate_review(valid_review(verdict="FAIL"))

    def test_executor_schema_is_closed_and_iteration_explicit(self):
        self.assertEqual("correction appliquée", validate_executor(valid_executor())["summary"])
        with self.assertRaisesRegex(PilotError, "champs non autorisés"):
            validate_executor(valid_executor(echo="surprise"))
        with self.assertRaisesRegex(PilotError, "approach_changed"):
            validate_executor(valid_executor(), iteration=True)
        payload = valid_executor(approach_changed=False)
        self.assertFalse(validate_executor(payload, iteration=True)["approach_changed"])

    def test_prompt_echo_is_refused_before_archival(self):
        prompt = "PROMPT-EXACT-UNIQUE"
        with self.assertRaisesRegex(PilotError, "copie du prompt"):
            validate_plan(valid_plan(task=f"copie {prompt}"), forbidden_prompt=prompt)
        with tempfile.TemporaryDirectory() as tmp, self.assertRaisesRegex(
            PilotError, "copie du prompt"
        ):
            write_normalized_json(
                Path(tmp) / "agent.json",
                {"message": f"écho {prompt}"},
                forbidden_texts=[prompt],
            )


class StreamingFailClosedTests(unittest.TestCase):
    def test_process_group_configuration_covers_windows_and_posix(self):
        self.assertIn("creationflags", _process_group_options("nt"))
        self.assertEqual({"start_new_session": True}, _process_group_options("posix"))

    def test_stream_bounds_are_refused_before_process_start(self):
        with self.assertRaisesRegex(PilotError, "strictement positif"):
            run_command_stream(
                ["binary-that-must-not-start"],
                cwd=Path.cwd(),
                timeout_seconds=0,
            )

    def test_large_stdin_and_early_stderr_do_not_deadlock(self):
        with tempfile.TemporaryDirectory() as tmp:
            prompt = "p" * (2 * 1024 * 1024)
            code = (
                "import json,os,sys,threading; "
                "watchdog=threading.Timer(3.0,lambda:os._exit(86)); "
                "watchdog.daemon=True; watchdog.start(); "
                "sys.stderr.write('x'*200000); sys.stderr.flush(); "
                "data=sys.stdin.buffer.read(); "
                "print(json.dumps({'bytes':len(data)}), flush=True)"
            )
            result = run_command_stream(
                [sys.executable, "-c", code],
                cwd=Path(tmp),
                timeout_seconds=10,
                stdin=prompt,
            )
            self.assertEqual({"bytes": len(prompt)}, result.json())

    def test_oversized_json_line_is_refused_without_becoming_the_tail(self):
        with tempfile.TemporaryDirectory() as tmp, self.assertRaisesRegex(
            PilotError, "ligne JSON supérieure"
        ):
            code = (
                "import json,sys; "
                "sys.stdout.write('x'*8192+'\\n'); sys.stdout.flush(); "
                "print(json.dumps({'done':True}), flush=True)"
            )
            run_command_stream(
                [sys.executable, "-c", code],
                cwd=Path(tmp),
                timeout_seconds=10,
                max_line_bytes=1024,
            )

    def test_callback_failure_kills_the_child_process_group(self):
        with tempfile.TemporaryDirectory() as tmp:
            marker = Path(tmp) / "grandchild-survived.txt"
            grandchild = (
                "import pathlib,time; time.sleep(1.0); "
                f"pathlib.Path({str(marker)!r}).write_text('survived')"
            )
            child = (
                "import json,subprocess,sys,time; "
                f"subprocess.Popen([sys.executable,'-c',{grandchild!r}]); "
                "print(json.dumps({'started':True}),flush=True); time.sleep(10)"
            )

            def fail_callback(event: object) -> None:
                raise RuntimeError("callback failure")

            with self.assertRaisesRegex(RuntimeError, "callback failure"):
                run_command_stream(
                    [sys.executable, "-c", child],
                    cwd=Path(tmp),
                    timeout_seconds=5,
                    on_event=fail_callback,
                )
            time.sleep(1.5)
            self.assertFalse(marker.exists())


if __name__ == "__main__":
    unittest.main()


class RedactionMinimumLengthTests(unittest.TestCase):
    """
    Une variable d'environnement au nom « token-esque » mais à la valeur très
    courte ne doit pas être masquée : masquer « 4 » corrompt tout texte qui en
    contient un — un SHA de commit, par exemple, qui devient plus long que 40
    caractères et ne correspond plus à rien.

    Défaut réel : CLAUDE_CODE_OAUTH_TOKEN_FILE_DESCRIPTOR vaut un chiffre. Avec
    lui dans l'environnement, deux tests du flux d'itération échouaient sur
    « Feedback périmé : il ne vise pas le head SHA courant » — le SHA relu avait
    été mangé par la redaction.
    """

    SHA = "b6d814f0e528545e4e8ccdda63cea2feb24996f6"

    def test_une_valeur_courte_ne_corrompt_pas_un_sha(self):
        from forgepilot.state import sanitize_error

        with patch.dict(os.environ, {"UN_TOKEN_FILE_DESCRIPTOR": "4"}, clear=False):
            self.assertIn(self.SHA, sanitize_error(f"head {self.SHA}"))

    def test_un_vrai_secret_reste_masque(self):
        from forgepilot.state import sanitize_error

        secret = "sk-un-vrai-secret-assez-long"
        with patch.dict(os.environ, {"UN_API_KEY": secret}, clear=False):
            sortie = sanitize_error(f"echec avec {secret}")
            self.assertNotIn(secret, sortie)
            self.assertIn("<secret>", sortie)
