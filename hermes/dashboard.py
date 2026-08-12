#!/usr/bin/env python3
"""hermes/dashboard.py — génère hermes/DASHBOARD.md, la vue d'ensemble lisible.

Rôle (ADR-0010) : Hermes est le chef de projet ; ce script produit son
tableau de bord. C'est une VUE générée depuis les sources de vérité du
dépôt — jamais une base parallèle (principe n°1 du projet) et jamais un
fichier édité à la main :

  - architecture/audit-ledger.jsonl  → où en est chaque audit de la boucle
  - harness/pipeline/config.yaml     → mode du pipeline et réglages
  - harness/pipeline/ci-budget-ledger.jsonl → dépense CI du mois
  - harness/queue/cost-ledger.jsonl  → utilisation des backends Générateur
  - harness/queue/briefs/*/          → état apparent de chaque brief

Trois fichiers JSON optionnels, produits par le workflow
.github/workflows/hermes-dashboard.yml avec des données GitHub/Cursor
vivantes (runs récents, PR ouvertes, agents lancés), enrichissent la vue ;
leur absence ne fait jamais échouer la génération — le tableau dit alors
« non disponible » au lieu d'inventer.

Usage :
  py hermes/dashboard.py                        # vue locale
  py hermes/dashboard.py --runs-json runs.json --prs-json prs.json \
      --agents-json agents.json                 # vue enrichie (CI)
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(REPO_ROOT / "harness" / "pipeline"))

# Libellés humains des états de la machine à états des audits
# (harness/audit_ledger.py TRANSITIONS) : le propriétaire lit une phrase,
# pas un code.
ETATS_HUMAINS = {
    "AUDIT_PROPOSED": "déposé — attend le contre-audit de Claude",
    "AUDIT_CHALLENGED": "contre-audit rendu — attend la décision",
    "AUDIT_APPROVED": "retenu — à convertir en brief",
    "AUDIT_REJECTED": "rejeté — à archiver",
    "AUDIT_CONVERTED": "converti en brief — travail à produire",
    "AUDIT_IMPLEMENTED": "travail livré — attend vérification",
    "AUDIT_VERIFIED": "vérifié — à archiver",
    "AUDIT_STALE": "obsolète — à archiver",
    "AUDIT_ARCHIVED": "boucle close",
}

ETATS_EN_ATTENTE = {
    "AUDIT_PROPOSED", "AUDIT_CHALLENGED", "AUDIT_APPROVED",
    "AUDIT_CONVERTED", "AUDIT_IMPLEMENTED", "AUDIT_VERIFIED", "AUDIT_STALE",
    "AUDIT_REJECTED",
}


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
            continue  # une ligne corrompue n'invalide pas la vue, elle est ignorée
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


def etat_des_audits(ledger_path: Path, inbox_dir: Path | None = None) -> list[dict]:
    """Dernier événement par audit_id, ordre d'apparition conservé.

    Un audit présent dans l'inbox mais absent du ledger est un
    ``AUDIT_PROPOSED`` implicite (convention du dépôt : l'événement
    PROPOSED au ledger est optionnel, la présence du fichier fait foi) —
    il est listé, jamais passé sous silence.
    """
    dernier: dict[str, dict] = {}
    for entry in _read_jsonl(ledger_path):
        audit_id = str(entry.get("audit_id", "")).strip()
        if audit_id:
            dernier[audit_id] = entry
    if inbox_dir is not None and inbox_dir.exists():
        for audit_file in sorted(inbox_dir.glob("*.md")):
            audit_id = audit_file.stem
            if audit_id not in dernier:
                dernier[audit_id] = {"event": "AUDIT_PROPOSED", "timestamp": "— (fichier inbox, pas encore au ledger)"}
    return [
        {
            "audit_id": audit_id,
            "event": entry.get("event", "?"),
            "humain": ETATS_HUMAINS.get(entry.get("event", ""), entry.get("event", "?")),
            "timestamp": entry.get("timestamp", "?"),
        }
        for audit_id, entry in dernier.items()
    ]


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


def etat_des_briefs(briefs_dir: Path) -> list[dict]:
    """État apparent : dernier `VERDICT: ACCEPT|REJECT` tracé dans verdict.md.

    C'est une lecture, pas un jugement — le fichier verdict.md de
    l'Évaluateur reste la seule autorité ; « aucun tracé » signifie
    simplement que ce motif n'apparaît pas, pas que le brief est en échec.
    """
    verdict_re = re.compile(r"VERDICT:\s*(ACCEPT|REJECT)")
    briefs = []
    if not briefs_dir.exists():
        return briefs
    for brief in sorted(p for p in briefs_dir.iterdir() if p.is_dir()):
        verdict_path = brief / "verdict.md"
        if not verdict_path.exists():
            statut = "pas encore de verdict"
        else:
            matches = verdict_re.findall(verdict_path.read_text(encoding="utf-8", errors="ignore"))
            statut = f"dernier verdict tracé : {matches[-1]}" if matches else "verdict rendu (voir le fichier)"
        briefs.append({"brief": brief.name, "statut": statut})
    return briefs


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
    audits = etat_des_audits(
        repo_root / "architecture" / "audit-ledger.jsonl",
        inbox_dir=repo_root / "architecture" / "inbox",
    )
    budget = budget_du_mois(repo_root / "harness" / "pipeline" / "ci-budget-ledger.jsonl", now=now)
    backends = usage_backends(repo_root / "harness" / "queue" / "cost-ledger.jsonl")
    briefs = etat_des_briefs(repo_root / "harness" / "queue" / "briefs")

    runs = _read_optional_json(runs_json)
    prs = _read_optional_json(prs_json)
    agents = _read_optional_json(agents_json)

    audits_en_cours = [a for a in audits if a["event"] != "AUDIT_ARCHIVED"]
    mode = config.get("mode", "?")

    out: list[str] = []
    out.append("# Tableau de bord — ForgeHistory")
    out.append("")
    out.append("> Vue générée par `hermes/dashboard.py` (rôle Hermes, ADR-0010) —")
    out.append("> **ne jamais l'éditer à la main**, elle est réécrite à chaque")
    out.append("> poussée sur `master` et toutes les 6 heures par")
    out.append("> `.github/workflows/hermes-dashboard.yml`.")
    out.append(">")
    out.append(f"> Générée le {now.strftime('%Y-%m-%d %H:%M UTC')}.")
    out.append("")

    # -- En bref -----------------------------------------------------------
    out.append("## En bref")
    out.append("")
    out.append(f"- **Mode du pipeline** : `{mode}`"
               + (" — la boucle tourne sans intervention humaine (hors fusion finale)." if mode == "full_auto" else
                  " — voir `docs/rules/full-auto-pipeline.md`."))
    out.append(f"- **Dépense CI ce mois-ci** : {budget['total_usd']} USD mesurés sur "
               f"{budget['invocations']} invocation(s), plafond {monthly_cap_usd:.0f} USD. "
               "En authentification par abonnement, ce chiffre est un équivalent estimé, pas une facture.")
    out.append(f"- **Audits en cours** : {len(audits_en_cours)} — boucles closes : "
               f"{len(audits) - len(audits_en_cours)}.")
    out.append("")

    # -- Ce qui attend le propriétaire --------------------------------------
    out.append("## Ce qui attend le propriétaire")
    out.append("")
    attentes: list[str] = []
    if isinstance(prs, list):
        for pr in prs:
            numero = pr.get("number", "?")
            attentes.append(f"- Fusionner (ou refuser) la PR #{numero} — « {pr.get('title', '?')} » "
                            f"(branche `{pr.get('headRefName', '?')}`). L'auto-fusion GitHub est "
                            "indisponible sur ce plan : le clic final est humain.")
    for audit in audits_en_cours:
        if audit["event"] in ("AUDIT_APPROVED",):
            attentes.append(f"- Convertir l'audit retenu `{audit['audit_id']}` en brief (`/forge-audit-convert`).")
    if not attentes:
        attentes.append("- Rien : aucune PR ouverte connue, aucun audit en attente de décision.")
    out.extend(attentes)
    out.append("")

    # -- Activité GitHub récente --------------------------------------------
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

    # -- Agents lancés récemment ---------------------------------------------
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

    # -- La boucle d'audit ----------------------------------------------------
    out.append("## La boucle d'audit, audit par audit")
    out.append("")
    if audits:
        rows = [
            [a["audit_id"], a["humain"],
             str(a["timestamp"]) if str(a["timestamp"]).startswith("—")
             else str(a["timestamp"])[:16].replace("T", " ")]
            for a in audits_en_cours
        ]
        if rows:
            out.append(_table(rows, ["audit", "où il en est", "dernier événement (UTC)"]))
        else:
            out.append("Toutes les boucles d'audit sont closes.")
        out.append("")
        out.append(f"({len(audits) - len(audits_en_cours)} boucle(s) close(s) non listée(s) — "
                   "détail : `architecture/audit-ledger.jsonl`.)")
    else:
        out.append("Aucun audit enregistré au ledger.")
    out.append("")

    # -- Briefs ---------------------------------------------------------------
    out.append("## Briefs (les commandes de travail)")
    out.append("")
    if briefs:
        out.append(_table([[b["brief"], b["statut"]] for b in briefs], ["brief", "état apparent"]))
        out.append("")
        out.append("« État apparent » = dernière mention `VERDICT:` tracée dans le "
                   "`verdict.md` du brief ; l'autorité reste le fichier lui-même et "
                   "`HANDOFF.md` pour le contexte.")
    else:
        out.append("Aucun brief dans la file.")
    out.append("")

    # -- Utilisation ------------------------------------------------------------
    out.append("## Utilisation des backends Générateur")
    out.append("")
    if backends:
        rows = [[b["backend"], b["runs"], (b["dernier"][:16].replace("T", " ") or "?")] for b in backends]
        out.append(_table(rows, ["backend", "runs cumulés", "dernier run (UTC)"]))
    else:
        out.append("Aucun run de Générateur enregistré.")
    out.append("")

    # -- Légende -----------------------------------------------------------------
    out.append("## Comment lire ce tableau")
    out.append("")
    out.append("La chaîne nominale (ADR-0010) : une **demande** entre par Hermes")
    out.append("(`hermes/requests/`) → le propriétaire tranche → `ROADMAP.md` est mise")
    out.append("à jour → Claude (CTO) écrit un **brief** → Codex produit → le gate")
    out.append("mécanique juge → Claude ouvre la **PR** → Cursor la **critique**")
    out.append("(audit) → Claude **contre-audite** l'audit → décision → la boucle se")
    out.append("clôt (`AUDIT_ARCHIVED`).")
    out.append("")
    out.append("- Direction et étapes suivantes : [ROADMAP.md](../ROADMAP.md)")
    out.append("- Dernier état de session détaillé : [HANDOFF.md](../HANDOFF.md)")
    out.append("- Marche/arrêt de la boucle : `docs/rules/full-auto-pipeline.md`")
    out.append("  (arrêt d'urgence : label `pipeline/pause`, ou `mode: manual`).")
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
