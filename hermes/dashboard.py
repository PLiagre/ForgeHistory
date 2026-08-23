#!/usr/bin/env python3
"""hermes/dashboard.py — génère hermes/DASHBOARD.md, la vue d'ensemble lisible.

Rôle (ADR-0010) : Hermes est le chef de projet ; ce script produit son
tableau de bord. C'est une VUE générée depuis les sources de vérité du
dépôt — jamais une base parallèle (principe n°1 du projet) et jamais un
fichier édité à la main.

Depuis le ménage de pilotage (2026-08-23), la vue ne liste plus la
boucle d'audit Cursor : cette file est historique (ADR-0012). Hermes au
boot ne lit que ce qui est OPEN et actionnable, plus ROADMAP / HANDOFF.

Sources :

  - harness/pipeline/config.yaml     → mode du pipeline
  - harness/pipeline/ci-budget-ledger.jsonl → dépense CI du mois
  - harness/queue/cost-ledger.jsonl  → utilisation des backends Générateur
  - hermes/propositions/PROPOSITION-* → seulement status OPEN
  - hermes/requests/DEMANDE-*         → seulement status OPEN

Trois fichiers JSON optionnels, produits par le workflow
.github/workflows/hermes-dashboard.yml avec des données GitHub/Cursor
vivantes, enrichissent la vue ; leur absence ne fait jamais échouer la
génération — le tableau dit alors « non disponible » au lieu d'inventer.

Usage :
  .venv/bin/python hermes/dashboard.py
  .venv/bin/python hermes/dashboard.py --runs-json runs.json --prs-json prs.json \
      --agents-json agents.json
"""
from __future__ import annotations

import argparse
import json
import sys
from datetime import datetime, timezone
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(REPO_ROOT / "harness" / "pipeline"))


def _read_jsonl(path: Path) -> list[dict]:
    if not path.exists():
        return []
    entries = []
    for raw in path.read_text(encoding="utf-8").splitlines():
        raw = raw.strip()
        if not raw:
            continue
        try:
            entry = json.loads(raw)
        except json.JSONDecodeError:
            continue
        if isinstance(entry, dict):
            entries.append(entry)
    return entries


def _read_optional_json(path: Path | None) -> object | None:
    if path is None or not path.exists():
        return None
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return None


def _frontmatter(path: Path) -> dict[str, str]:
    text = path.read_text(encoding="utf-8", errors="ignore")
    if not text.startswith("---"):
        return {}
    end = text.find("\n---", 3)
    if end < 0:
        return {}
    fields: dict[str, str] = {}
    for line in text[3:end].splitlines():
        if ":" not in line:
            continue
        key, value = line.split(":", 1)
        fields[key.strip()] = value.strip()
    return fields


def _titre(path: Path) -> str:
    for line in path.read_text(encoding="utf-8", errors="ignore").splitlines():
        if line.startswith("# "):
            return line[2:].strip()
    return path.stem


def items_hermes_open(directory: Path, prefix: str) -> list[dict]:
    """Fichiers Hermes encore OPEN. Tout autre statut est hors vue de boot."""
    if not directory.exists():
        return []
    ouverts = []
    for path in sorted(directory.glob(f"{prefix}*.md")):
        if _frontmatter(path).get("status") == "OPEN":
            ouverts.append({"path": path.name, "titre": _titre(path)})
    return ouverts


def budget_du_mois(ledger_path: Path, now: datetime | None = None) -> dict:
    now = now or datetime.now(timezone.utc)
    total = 0.0
    lignes = 0
    for entry in _read_jsonl(ledger_path):
        ts = str(entry.get("timestamp", ""))
        try:
            parsed = datetime.fromisoformat(ts.replace("Z", "+00:00"))
        except ValueError:
            continue
        if (parsed.year, parsed.month) == (now.year, now.month):
            try:
                total += float(entry.get("usd", 0))
            except (TypeError, ValueError):
                continue
            lignes += 1
    return {"total_usd": round(total, 4), "invocations": lignes}


def usage_backends(cost_ledger_path: Path) -> list[dict]:
    compte: dict[str, dict] = {}
    for entry in _read_jsonl(cost_ledger_path):
        backend = str(entry.get("backend", "?"))
        info = compte.setdefault(backend, {"backend": backend, "runs": 0, "dernier": ""})
        info["runs"] += 1
        info["dernier"] = max(info["dernier"], str(entry.get("timestamp", "")))
    return sorted(compte.values(), key=lambda i: -i["runs"])


def _table(rows: list[list[str]], headers: list[str]) -> str:
    lines = ["| " + " | ".join(headers) + " |", "|" + "---|" * len(headers)]
    lines += ["| " + " | ".join(str(c) for c in row) + " |" for row in rows]
    return "\n".join(lines)


def generer(
    repo_root: Path,
    runs_json: Path | None = None,
    prs_json: Path | None = None,
    agents_json: Path | None = None,
    monthly_cap_usd: float = 200.0,
    now: datetime | None = None,
) -> str:
    now = now or datetime.now(timezone.utc)

    import policy_loader  # noqa: E402 (dépôt sans PyYAML, chargeur maison)

    config = policy_loader.load_flat_yaml(repo_root / "harness" / "pipeline" / "config.yaml")
    budget = budget_du_mois(repo_root / "harness" / "pipeline" / "ci-budget-ledger.jsonl", now=now)
    backends = usage_backends(repo_root / "harness" / "queue" / "cost-ledger.jsonl")
    propositions = items_hermes_open(repo_root / "hermes" / "propositions", "PROPOSITION-")
    demandes = items_hermes_open(repo_root / "hermes" / "requests", "DEMANDE-")

    runs = _read_optional_json(runs_json)
    prs = _read_optional_json(prs_json)
    agents = _read_optional_json(agents_json)

    mode = config.get("mode", "?")

    out: list[str] = []
    out.append("# Tableau de bord — ForgeHistory")
    out.append("")
    out.append("> Vue générée par `hermes/dashboard.py` (rôle Hermes, ADR-0010) —")
    out.append("> **ne jamais l'éditer à la main**. Régénérer avec")
    out.append("> `.venv/bin/python hermes/dashboard.py` ou le workflow")
    out.append("> `hermes-dashboard.yml`. Une vue périmée reste périmée")
    out.append("> tant que personne ne la régénère.")
    out.append(">")
    out.append(f"> Générée le {now.strftime('%Y-%m-%d %H:%M UTC')}.")
    out.append("")

    out.append("## En bref")
    out.append("")
    out.append("- **Produit** : `sim/` + `viewer/` mince. Unity en veille.")
    out.append("- **Prochain pas produit** : un seul, dans [ROADMAP.md](../ROADMAP.md).")
    out.append(f"- **Mode du pipeline** : `{mode}`"
               + (" — la boucle tourne sans intervention humaine (hors fusion finale)." if mode == "full_auto" else
                  " — `docs/rules/full-auto-pipeline.md`."))
    out.append(f"- **Pilotage ouvert** : {len(demandes)} demande(s), "
               f"{len(propositions)} proposition(s).")
    out.append(f"- **Dépense CI ce mois-ci** : {budget['total_usd']} USD mesurés sur "
               f"{budget['invocations']} invocation(s), plafond {monthly_cap_usd:.0f} USD. "
               "En authentification par abonnement, ce chiffre est un équivalent estimé, pas une facture.")
    out.append("- **Boucle d'audit** : historique (jalons seulement, ADR-0012). "
               "Hermes ne la parcourt plus au boot. "
               "Voir [architecture/README.md](../architecture/README.md).")
    out.append("")

    out.append("## Ce qui attend le propriétaire")
    out.append("")
    attentes: list[str] = []
    if isinstance(prs, list):
        for pr in prs:
            numero = pr.get("number", "?")
            attentes.append(
                f"- Fusionner (ou refuser) la PR #{numero} — « {pr.get('title', '?')} » "
                f"(branche `{pr.get('headRefName', '?')}`)."
            )
    for item in propositions:
        attentes.append(f"- Trancher la proposition `{item['path']}` — {item['titre']}.")
    for item in demandes:
        attentes.append(f"- Trancher la demande `{item['path']}` — {item['titre']}.")
    if not attentes:
        attentes.append("- Rien n'attend.")
    out.extend(attentes)
    out.append("")

    out.append("## Activité GitHub récente")
    out.append("")
    if isinstance(runs, list) and runs:
        rows = []
        for run in runs[:15]:
            heure = str(run.get("createdAt", "?")).replace("T", " ").replace("Z", "")
            resultat = run.get("conclusion") or run.get("status") or "?"
            rows.append([heure, run.get("name", "?"), run.get("event", "?"),
                         run.get("headBranch", "?"), resultat])
        out.append(_table(rows, ["quand (UTC)", "workflow", "déclencheur", "branche", "résultat"]))
    else:
        out.append("Non disponible dans cette génération (données GitHub non fournies au script).")
    out.append("")

    out.append("## Agents lancés récemment (Cursor Cloud)")
    out.append("")
    liste_agents = agents.get("agents") if isinstance(agents, dict) else agents
    if isinstance(liste_agents, list) and liste_agents:
        rows = []
        for agent in liste_agents[:10]:
            rows.append([
                agent.get("name", "?"),
                agent.get("status", "?"),
                (agent.get("source") or "?"),
                agent.get("branchName") or "—",
            ])
        out.append(_table(rows, ["agent", "statut", "lancé par", "branche"]))
        out.append("")
        out.append("`api` = lancé automatiquement par la CI ; `web` = lancé à la main.")
    else:
        out.append("Non disponible dans cette génération (API Cursor non interrogée).")
    out.append("")

    out.append("## Utilisation des backends Générateur")
    out.append("")
    if backends:
        rows = [[b["backend"], b["runs"], (b["dernier"][:16].replace("T", " ") or "?")] for b in backends]
        out.append(_table(rows, ["backend", "runs cumulés", "dernier run (UTC)"]))
    else:
        out.append("Aucun run de Générateur enregistré.")
    out.append("")

    out.append("## Comment lire ce tableau")
    out.append("")
    out.append("Boot Hermes : cette vue, les propositions OPEN,")
    out.append("[ROADMAP.md](../ROADMAP.md), [HANDOFF.md](../HANDOFF.md),")
    out.append("`forgepilot doctor`, puis `.venv/bin/python -m sim --ticks 0 --json`.")
    out.append("Les briefs 001–029 et `architecture/` ne se lisent pas au démarrage.")
    out.append("")
    return "\n".join(out)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path, default=REPO_ROOT)
    parser.add_argument("--output", type=Path, default=None,
                        help="défaut : <repo-root>/hermes/DASHBOARD.md")
    parser.add_argument("--runs-json", type=Path, default=None)
    parser.add_argument("--prs-json", type=Path, default=None)
    parser.add_argument("--agents-json", type=Path, default=None)
    parser.add_argument("--monthly-cap-usd", type=float, default=200.0)
    args = parser.parse_args(argv)

    contenu = generer(
        args.repo_root,
        runs_json=args.runs_json,
        prs_json=args.prs_json,
        agents_json=args.agents_json,
        monthly_cap_usd=args.monthly_cap_usd,
    )
    output = args.output or args.repo_root / "hermes" / "DASHBOARD.md"
    output.write_text(contenu, encoding="utf-8", newline="\n")
    print(f"OK: {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
