#!/usr/bin/env python3
"""Génère ``hermes/DASHBOARD.md``, une vue factuelle et facultative.

Le tableau de bord dérive des fichiers du dépôt et ne devient jamais une base
parallèle. Il n'attribue aucun rôle et ne conditionne aucun changement.

Sources :

  - hermes/propositions/PROPOSITION-* → seulement status OPEN
  - hermes/requests/DEMANDE-*         → seulement status OPEN

Deux fichiers JSON optionnels (PR ouvertes, exécutions GitHub récentes)
enrichissent la vue quand on les fournit ; leur absence ne fait jamais
échouer la génération — le tableau dit alors « non disponible » au lieu
d'inventer.

Usage :
  python3 hermes/dashboard.py
  python3 hermes/dashboard.py --runs-json runs.json --prs-json prs.json
"""
from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent


def _read_optional_json(path: Path | None) -> object | None:
    if path is None or not path.exists():
        return None
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return None


def _frontmatter(path: Path) -> dict[str, str]:
    """Lit l'en-tête `---` ... `---` d'un document Hermes."""
    lignes = path.read_text(encoding="utf-8", errors="ignore").splitlines()
    if not lignes or lignes[0].strip() != "---":
        return {}
    champs: dict[str, str] = {}
    for ligne in lignes[1:]:
        if ligne.strip() == "---":
            break
        if ":" in ligne:
            cle, _, valeur = ligne.partition(":")
            champs[cle.strip()] = valeur.strip()
    return champs


def _titre(path: Path) -> str:
    for ligne in path.read_text(encoding="utf-8", errors="ignore").splitlines():
        if ligne.startswith("# "):
            return ligne[2:].strip()
    return path.stem


def items_hermes_open(directory: Path, prefix: str) -> list[dict]:
    """Les documents Hermes encore OPEN, les seuls qui attendent une décision."""
    if not directory.is_dir():
        return []
    items = []
    for path in sorted(directory.glob(f"{prefix}*.md")):
        if _frontmatter(path).get("status", "").upper() != "OPEN":
            continue
        items.append({"path": str(path.relative_to(REPO_ROOT)), "titre": _titre(path)})
    return items


def _table(rows: list[list[str]], headers: list[str]) -> str:
    out = ["| " + " | ".join(headers) + " |",
           "|" + "|".join(["---"] * len(headers)) + "|"]
    for row in rows:
        out.append("| " + " | ".join(str(c) for c in row) + " |")
    return "\n".join(out)


def generer(
    repo_root: Path,
    *,
    runs_json: Path | None = None,
    prs_json: Path | None = None,
    now: datetime | None = None,
) -> str:
    now = now or datetime.now(timezone.utc)

    propositions = items_hermes_open(repo_root / "hermes" / "propositions", "PROPOSITION-")
    demandes = items_hermes_open(repo_root / "hermes" / "requests", "DEMANDE-")

    runs = _read_optional_json(runs_json)
    prs = _read_optional_json(prs_json)

    out: list[str] = []
    out.append("# Tableau de bord — ForgeHistory")
    out.append("")
    out.append("> Vue facultative générée par `hermes/dashboard.py`.")
    out.append("> Régénérer avec `python3 hermes/dashboard.py` lorsque cette vue est utile.")
    out.append(">")
    out.append(f"> Générée le {now.strftime('%Y-%m-%d %H:%M UTC')}.")
    out.append("")

    out.append("## En bref")
    out.append("")
    out.append("- **Produit** : `sim/` (`python3 -m sim`) + `viewer/` mince.")
    out.append("- **Prochain pas produit** : un seul, dans [ROADMAP.md](../ROADMAP.md).")
    out.append(f"- **Éléments ouverts** : {len(demandes)} demande(s), "
               f"{len(propositions)} proposition(s).")
    out.append("- **Contribution** : toute personne ou tout agent autorisé peut planifier,")
    out.append("  modifier, tester, documenter et relire.")
    out.append("")

    out.append("## Points ouverts")
    out.append("")
    attentes: list[str] = []
    if isinstance(prs, list):
        for pr in prs:
            numero = pr.get("number", "?")
            attentes.append(
                f"- Examiner la PR #{numero} — « {pr.get('title', '?')} » "
                f"(branche `{pr.get('headRefName', '?')}`)."
            )
    for item in propositions:
        attentes.append(f"- Examiner la proposition `{item['path']}` — {item['titre']}.")
    for item in demandes:
        attentes.append(f"- Examiner la demande `{item['path']}` — {item['titre']}.")
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

    out.append("## Comment lire ce tableau")
    out.append("")
    out.append("Lire [ROADMAP.md](../ROADMAP.md), puis mesurer le produit avec")
    out.append("`python3 -m sim --ticks 0 --json`. Cette vue, les briefs et")
    out.append("ForgePilot sont des aides facultatives.")
    out.append("")
    return "\n".join(out)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path, default=REPO_ROOT)
    parser.add_argument("--output", type=Path, default=None,
                        help="défaut : <repo-root>/hermes/DASHBOARD.md")
    parser.add_argument("--runs-json", type=Path, default=None)
    parser.add_argument("--prs-json", type=Path, default=None)
    args = parser.parse_args(argv)

    contenu = generer(args.repo_root, runs_json=args.runs_json, prs_json=args.prs_json)
    output = args.output or args.repo_root / "hermes" / "DASHBOARD.md"
    output.write_text(contenu, encoding="utf-8", newline="\n")
    print(f"OK: {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
