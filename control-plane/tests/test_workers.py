"""Présence du worker PC : online, offline, labels, ping invalide."""

from __future__ import annotations

import io
import json
import unittest
from unittest.mock import patch

from forgepilot.process import PilotError
from forgepilot.workers import (
    format_workers,
    matching_online,
    parse_runners,
    refuse_if_absent,
    validate_ping,
    workers_snapshot,
)


def _runner(
    name: str = "pc-windows",
    status: str = "online",
    labels: list[object] | None = None,
    busy: bool = False,
) -> dict[str, object]:
    if labels is None:
        labels = [
            {"name": "self-hosted"},
            {"name": "windows"},
            {"name": "high-memory"},
            {"name": "unity"},
            {"name": "local-llm"},
        ]
    return {"name": name, "status": status, "busy": busy, "os": "Windows", "labels": labels}


def _payload(*runners: dict[str, object]) -> dict[str, object]:
    return {"total_count": len(runners), "runners": list(runners)}


class ParseRunnersTests(unittest.TestCase):
    def test_online_avec_labels(self):
        workers = parse_runners(_payload(_runner()))
        self.assertEqual(len(workers), 1)
        self.assertTrue(workers[0].online)
        self.assertIn("windows", workers[0].labels)
        self.assertIn("high-memory", workers[0].labels)

    def test_offline(self):
        workers = parse_runners(_payload(_runner(status="offline")))
        self.assertFalse(workers[0].online)
        self.assertEqual(matching_online(workers, ("windows",)), ())

    def test_liste_vide(self):
        self.assertEqual(parse_runners({"runners": []}), ())

    def test_payload_illisible_refuse(self):
        with self.assertRaises(PilotError):
            parse_runners("pas un objet")
        with self.assertRaises(PilotError):
            parse_runners({})
        with self.assertRaises(PilotError):
            parse_runners({"runners": [{"name": "x"}]})
        with self.assertRaises(PilotError):
            parse_runners({"runners": [{"name": "x", "labels": [None]}]})

    def test_identifiant_depot_invalide(self):
        from forgepilot.workers import fetch_runner_payload
        from pathlib import Path

        with self.assertRaises(PilotError):
            fetch_runner_payload(Path("."), "pas un depot")
        with self.assertRaises(PilotError):
            fetch_runner_payload(Path("."), "owner/repo; rm -rf /")

    def test_labels_incompatibles(self):
        workers = parse_runners(_payload(_runner(labels=[{"name": "windows"}])))
        self.assertEqual(matching_online(workers, ("unity",)), ())
        self.assertEqual(len(matching_online(workers, ("windows",))), 1)


class RefuseSiAbsentTests(unittest.TestCase):
    def test_online_accepte(self):
        snapshot = workers_snapshot(_payload(_runner()), ("windows", "high-memory"))
        refuse_if_absent(snapshot)
        self.assertEqual(snapshot["available"], ["pc-windows"])

    def test_offline_refuse(self):
        snapshot = workers_snapshot(_payload(_runner(status="offline")), ("windows",))
        with self.assertRaises(PilotError) as caught:
            refuse_if_absent(snapshot)
        self.assertIn("Worker absent", str(caught.exception))
        self.assertEqual(snapshot["workers"][0]["name"], "pc-windows")

    def test_aucun_runner_refuse(self):
        snapshot = workers_snapshot({"runners": []})
        with self.assertRaises(PilotError):
            refuse_if_absent(snapshot)

    def test_format_humain_montre_offline(self):
        snapshot = workers_snapshot(_payload(_runner(status="offline")))
        text = format_workers(snapshot)
        self.assertIn("pc-windows", text)
        self.assertIn("offline", text)


class PingSchemaTests(unittest.TestCase):
    def test_ping_valide(self):
        payload = {
            "schema_version": 1,
            "hostname": "ATELIER",
            "sha": "abcdef1234567",
            "capabilities": ["windows", "high-memory"],
        }
        self.assertEqual(validate_ping(payload)["hostname"], "ATELIER")

    def test_ping_invalide_refuse(self):
        with self.assertRaises(PilotError):
            validate_ping({})
        with self.assertRaises(PilotError):
            validate_ping(
                {
                    "schema_version": 1,
                    "hostname": "",
                    "sha": "abcdef1234567",
                    "capabilities": ["windows"],
                }
            )
        with self.assertRaises(PilotError):
            validate_ping(
                {
                    "schema_version": 1,
                    "hostname": "x",
                    "sha": "abc",
                    "capabilities": ["windows"],
                }
            )
        with self.assertRaises(PilotError):
            validate_ping(
                {
                    "schema_version": 1,
                    "hostname": "x",
                    "sha": "abcdef1234567",
                    "capabilities": [],
                }
            )


class WorkersCliTests(unittest.TestCase):
    def test_cli_online(self):
        from forgepilot.cli import main

        payload = _payload(_runner())
        buffer = io.StringIO()
        with (
            patch("forgepilot.cli.load_settings") as settings,
            patch("forgepilot.cli.fetch_runner_payload", return_value=payload),
            patch("sys.stdout", buffer),
        ):
            settings.return_value.engine_repository = "PLiagre/ForgeHistory"
            code = main(["workers", "--json"])
        self.assertEqual(code, 0)
        body = json.loads(buffer.getvalue())
        self.assertEqual(body["available"], ["pc-windows"])

    def test_cli_offline_refuse(self):
        from forgepilot.cli import main

        payload = _payload(_runner(status="offline"))
        err = io.StringIO()
        with (
            patch("forgepilot.cli.load_settings") as settings,
            patch("forgepilot.cli.fetch_runner_payload", return_value=payload),
            patch("sys.stderr", err),
        ):
            settings.return_value.engine_repository = "PLiagre/ForgeHistory"
            code = main(["workers", "--require", "windows"])
        self.assertEqual(code, 2)
        self.assertIn("Worker absent", err.getvalue())

    def test_cli_require_incompatible_refuse(self):
        from forgepilot.cli import main

        payload = _payload(_runner(labels=[{"name": "windows"}]))
        err = io.StringIO()
        with (
            patch("forgepilot.cli.load_settings") as settings,
            patch("forgepilot.cli.fetch_runner_payload", return_value=payload),
            patch("sys.stderr", err),
        ):
            settings.return_value.engine_repository = "PLiagre/ForgeHistory"
            code = main(["workers", "--require", "unity"])
        self.assertEqual(code, 2)
        self.assertIn("Worker absent", err.getvalue())


if __name__ == "__main__":
    unittest.main()
