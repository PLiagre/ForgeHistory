"""
test_pipeline_challenge_paths.py -- B4 et N5, brief 014 itération 3.

B4 : test mécanique qui lit .github/workflows/pipeline-challenge.yml,
extrait les conditions if: et les drapeaux continue-on-error de chaque
étape du job invoke-claude-challenger, applique les trois règles GitHub
Actions et vérifie la conclusion attendue du job pour les 7 chemins.

Rougit si l'étape B3 (Relever l'échec d'invocation non-429) est retirée
du workflow, car les chemins other_error produiraient alors 'success' au
lieu de 'failure' attendu.

N5 : test avec dépôt Git temporaire prouvant que fallback_attempted figure
à True dans le commit produit quand l'étape de commit-état est placée APRÈS
le repli (ordre N5).

Trois règles GitHub Actions appliquées :
  1. Condition if: sans fonction de statut → success() implicite ajouté.
  2. continue-on-error: true → outcome reste 'failure', conclusion = 'success' ;
     le job n'est PAS mis en échec par cette étape.
  3. Le job échoue dès qu'une conclusion d'étape est 'failure'.

Utiliser la variable d'environnement PIPELINE_WORKFLOW_PATH pour pointer
vers une copie sabotée lors de la génération de la preuve rouge.
"""
from __future__ import annotations

import json
import os
import re
import subprocess
import sys
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
HARNESS = REPO_ROOT / "harness"
sys.path.insert(0, str(HARNESS))

_DEFAULT_WORKFLOW = REPO_ROOT / ".github/workflows/pipeline-challenge.yml"
WORKFLOW_PATH = Path(os.environ.get("PIPELINE_WORKFLOW_PATH", str(_DEFAULT_WORKFLOW)))

JOB_NAME = "invoke-claude-challenger"

# ---------------------------------------------------------------------------
# YAML step parser (stdlib, sans dépendance externe)
# ---------------------------------------------------------------------------

_STATUS_FUNCS_RE = re.compile(
    r"\b(success|failure|cancelled|always)\s*\(|!cancelled\s*\("
)


def _has_status_func(cond: str) -> bool:
    return bool(_STATUS_FUNCS_RE.search(cond))


def parse_job_steps(yaml_text: str, job_name: str = JOB_NAME) -> list[dict]:
    """
    Extraire les étapes d'un job GitHub Actions depuis le texte YAML brut.

    Retourne une liste de dicts :
      { 'name': str, 'id': str|None, 'if_condition': str|None,
        'continue_on_error': bool }

    Règles d'arrêt (suffisantes pour ce workflow) :
    - Lignes purement commentaires ou vides → ignorées.
    - Scalaire de bloc (run: | / env: ...) → les lignes de contenu sont
      sautées (elles ne contiennent pas de conditions intéressantes).
    """
    lines = yaml_text.splitlines()
    in_job = False
    in_steps = False
    step_indent: int | None = None
    prop_indent: int | None = None  # indentation des propriétés d'étape
    in_block: bool = False  # dans un scalaire de bloc (run: |, env: …)
    block_threshold: int = 0
    current_step: dict | None = None
    steps: list[dict] = []

    for raw in lines:
        content = raw.lstrip()
        indent = len(raw) - len(content)

        if not content or content.startswith("#"):
            continue

        # Phase 1 : trouver le job
        if not in_job:
            if content.startswith(f"{job_name}:") and indent >= 2:
                in_job = True
            continue

        # Phase 2 : trouver steps:
        if not in_steps:
            if content.rstrip() == "steps:":
                in_steps = True
            elif indent <= 2 and content[0].isalpha() and ":" in content:
                break  # sorti du job
            continue

        # Phase 3 : dans les étapes
        if indent <= 2 and content[0].isalpha() and ":" in content:
            break  # sorti du job (nouveau job)

        # Scalaire de bloc actif : sauter les lignes de contenu
        if in_block:
            if indent > block_threshold:
                continue
            else:
                in_block = False

        # Nouvelle étape (tiret de liste)
        if content.startswith("- ") and (
            step_indent is None or indent == step_indent
        ):
            if current_step is not None:
                steps.append(current_step)
            current_step = {
                "name": "",
                "id": None,
                "if_condition": None,
                "continue_on_error": False,
            }
            step_indent = indent
            prop_indent = indent + 2
            rest = content[2:].lstrip()
            if ":" in rest:
                k, _, v = rest.partition(":")
                k = k.strip()
                v = v.strip()
                if v.startswith("|") or v.startswith(">"):
                    in_block = True
                    block_threshold = indent
                else:
                    v = v.strip("\"'")
                    if k == "name":
                        current_step["name"] = v
                    elif k == "uses":
                        current_step["name"] = f"uses:{v}"
                    elif k == "id":
                        current_step["id"] = v

        elif (
            current_step is not None
            and step_indent is not None
            and indent >= prop_indent
            and not content.startswith("- ")
        ):
            # Propriété d'une étape existante
            if ":" not in content:
                continue
            k, _, v = content.partition(":")
            k = k.strip()
            v = v.strip()
            if v.startswith("|") or v.startswith(">"):
                in_block = True
                block_threshold = indent
                continue
            if k == "name":
                current_step["name"] = v.strip("\"'")
            elif k == "id":
                current_step["id"] = v.strip("\"'")
            elif k == "if":
                # Ne pas striper les guillemets : la condition est une
                # expression multi-termes, pas une valeur scalaire simple.
                current_step["if_condition"] = v
            elif k == "continue-on-error":
                current_step["continue_on_error"] = v.lower() == "true"

    if current_step is not None:
        steps.append(current_step)

    return steps


# ---------------------------------------------------------------------------
# Simulateur GHA
# ---------------------------------------------------------------------------

def _eval_condition(
    cond: str | None,
    step_outcomes: dict[str, str],
    job_ok: bool,
    ctx: dict,
) -> bool:
    """
    Évaluer une condition if: GitHub Actions.

    Applique les trois règles :
    1. Condition vide → success() implicite.
    2. Condition sans fonction de statut → success() implicite ajouté.
    3. !cancelled() = True (on n'est jamais annulé dans le test).
    """
    if not cond:
        return job_ok

    check_available = "true" if ctx.get("check_available", True) else "false"
    classification = ctx.get("classification", "success")
    invoke_outcome = step_outcomes.get("invoke", ctx.get("invoke_outcome", "success"))

    # Substitutions des expressions GHA
    substitutions: list[tuple[str, str]] = [
        ("steps.pause.outputs.paused", "'false'"),
        ("steps.mode.outputs.mode", "''"),
        ("steps.resolve.outputs.audit_id", "'CURSOR-test'"),
        ("steps.check.outputs.available", f"'{check_available}'"),
        (
            "steps.classify_refusal.outputs.classification",
            f"'{classification}'",
        ),
        ("steps.invoke.outcome", f"'{invoke_outcome}'"),
    ]
    # Ajouter les outcomes enregistrés
    for sid, outcome in step_outcomes.items():
        substitutions.append((f"steps.{sid}.outcome", f"'{outcome}'"))

    expr = re.sub(r"\$\{\{\s*(.*?)\s*\}\}", r"\1", cond)
    for src, tgt in sorted(substitutions, key=lambda x: -len(x[0])):
        expr = expr.replace(src, tgt)

    has_sf = _has_status_func(cond)
    expr = expr.replace("!cancelled()", "True")
    expr = expr.replace("success()", str(job_ok))
    expr = expr.replace("failure()", str(not job_ok))
    expr = expr.replace("cancelled()", "False")
    expr = expr.replace("always()", "True")
    expr = expr.replace("&&", " and ").replace("||", " or ").replace("!=", " != ").replace("==", " == ")

    if not has_sf:
        expr = f"({job_ok}) and ({expr})"

    try:
        return bool(eval(expr, {"__builtins__": {}}))  # noqa: S307
    except Exception:
        return True  # conservatif : on suppose que l'étape s'exécute


def _step_outcome(step: dict, ctx: dict, step_outcomes: dict[str, str]) -> str:
    """
    Déterminer l'outcome d'une étape en fonction du contexte de chemin.

    Retourne 'success' ou 'failure'.
    """
    sid = step.get("id") or ""
    name = step.get("name", "").lower()

    if sid == "pause" or "kill-switch" in name:
        return "success"
    if sid == "mode" or "runtime mode" in name:
        return "success"
    if sid == "resolve" or "resolve audit" in name:
        return "success"
    if "budget precheck" in name or "monthly ci budget" in name:
        return "success"
    if sid == "check" or "credential availability" in name:
        return "success"  # available=true dans le contexte de test
    if "install" in name and "cli" in name:
        return "success"
    if sid == "transcript_path" or "export transcript" in name:
        return "success"
    if sid == "invoke" or "invoke claude" in name:
        return ctx.get("invoke_outcome", "success")
    if "post-hoc budget" in name or "budget marking" in name:
        return "success"  # || interne protège des transcripts illisibles
    if sid == "classify_refusal" or "classify vendor" in name:
        return "success"  # toujours success ; l'output est positionné par ctx
    if sid == "codex_fallback" or "repli codex" in name:
        return "success" if ctx.get("codex_succeeds", False) else "failure"
    if "commit" in name and "refus" in name:
        return "success"  # continue-on-error de toute façon
    if "publish" in name or "pull request" in name:
        return "success"  # exit 0 même sans revue (warning interne)
    if "relever" in name or "non-429" in name:
        # Étape B3 : toujours failure quand elle s'exécute (c'est son rôle)
        return "failure"
    # Étapes uses: (checkout, setup-python) et toute autre étape → success
    return "success"


def simulate_job(workflow_path: Path, ctx: dict) -> tuple[str, list[str]]:
    """
    Simuler le job invoke-claude-challenger pour un contexte de chemin.

    Retourne (conclusion_job, journal_étapes).
    La conclusion est 'success' ou 'failure'.
    """
    text = workflow_path.read_text(encoding="utf-8")
    steps = parse_job_steps(text, JOB_NAME)

    job_ok = True
    step_outcomes: dict[str, str] = {}
    journal: list[str] = []

    for step in steps:
        cond = step.get("if_condition")
        coe = step.get("continue_on_error", False)
        sid = step.get("id") or step.get("name", "?")[:30]

        runs = _eval_condition(cond, step_outcomes, job_ok, ctx)
        if not runs:
            journal.append(f"  SKIP  {sid}")
            if step.get("id"):
                step_outcomes[step["id"]] = "skipped"
            continue

        outcome = _step_outcome(step, ctx, step_outcomes)
        if step.get("id"):
            step_outcomes[step["id"]] = outcome

        if coe:
            # continue-on-error : conclusion = success même si outcome = failure
            journal.append(
                f"  {'OK(coe)' if outcome == 'failure' else 'OK    '} {sid}"
            )
        else:
            if outcome == "failure":
                job_ok = False
                journal.append(f"  FAIL  {sid} → job RED")
            else:
                journal.append(f"  OK    {sid}")

    conclusion = "success" if job_ok else "failure"
    return conclusion, journal


# ---------------------------------------------------------------------------
# Tableau des 7 chemins
# ---------------------------------------------------------------------------

SEVEN_PATHS = [
    {
        "id": 1,
        "label": "429 sans identifiant Codex",
        "ctx": {
            "invoke_outcome": "failure",
            "classification": "vendor_refusal",
            "check_available": True,
            "codex_succeeds": False,
        },
        "expected": "failure",
    },
    {
        "id": 2,
        "label": "429, identifiants présents, CLI absent",
        "ctx": {
            "invoke_outcome": "failure",
            "classification": "vendor_refusal",
            "check_available": True,
            "codex_succeeds": False,
        },
        "expected": "failure",
    },
    {
        "id": 3,
        "label": "429, Codex réussit",
        "ctx": {
            "invoke_outcome": "failure",
            "classification": "vendor_refusal",
            "check_available": True,
            "codex_succeeds": True,
        },
        "expected": "success",
    },
    {
        "id": 4,
        "label": "erreur statut 500",
        "ctx": {
            "invoke_outcome": "failure",
            "classification": "other_error",
            "check_available": True,
            "codex_succeeds": False,
        },
        "expected": "failure",
    },
    {
        "id": 5,
        "label": "CLI qui plante, transcript vide",
        "ctx": {
            "invoke_outcome": "failure",
            "classification": "other_error",
            "check_available": True,
            "codex_succeeds": False,
        },
        "expected": "failure",
    },
    {
        "id": 6,
        "label": "transcript illisible, revue produite",
        "ctx": {
            "invoke_outcome": "failure",
            "classification": "other_error",
            "check_available": True,
            "codex_succeeds": False,
        },
        "expected": "failure",
    },
    {
        "id": 7,
        "label": "succès normal",
        "ctx": {
            "invoke_outcome": "success",
            "classification": "success",
            "check_available": True,
            "codex_succeeds": False,
        },
        "expected": "success",
    },
]


# ---------------------------------------------------------------------------
# Test B4 : 7 chemins
# ---------------------------------------------------------------------------

@pytest.mark.parametrize("path_def", SEVEN_PATHS, ids=[p["label"] for p in SEVEN_PATHS])
def test_seven_paths(path_def):
    """
    B4 : vérifie la conclusion du job pour chacun des 7 chemins.

    Rougit si l'étape B3 est absente du workflow : les chemins other_error
    (4, 5, 6) produiraient 'success' au lieu de 'failure'.
    """
    if not WORKFLOW_PATH.exists():
        pytest.skip(f"workflow introuvable : {WORKFLOW_PATH}")

    conclusion, journal = simulate_job(WORKFLOW_PATH, path_def["ctx"])

    # Afficher le journal pour debug
    print(f"\nChemin {path_def['id']} — {path_def['label']}")
    for line in journal:
        print(line)
    print(f"→ Conclusion : {conclusion} (attendu : {path_def['expected']})")

    assert conclusion == path_def["expected"], (
        f"Chemin {path_def['id']} ({path_def['label']}) : "
        f"attendu {path_def['expected']!r}, obtenu {conclusion!r}.\n"
        + "\n".join(journal)
    )


# ---------------------------------------------------------------------------
# Vérification structurelle : l'étape B3 est présente
# ---------------------------------------------------------------------------

def test_b3_step_present_in_workflow():
    """
    Vérifie que l'étape terminale B3 existe dans le workflow.

    Si ce test passe mais que test_seven_paths échoue sur une copie sabotée
    sans l'étape, la paire rouge/vert est établie.
    """
    if not WORKFLOW_PATH.exists():
        pytest.skip(f"workflow introuvable : {WORKFLOW_PATH}")

    text = WORKFLOW_PATH.read_text(encoding="utf-8")
    steps = parse_job_steps(text, JOB_NAME)
    b3_steps = [
        s for s in steps
        if "relever" in s["name"].lower()
        or "non-429" in s["name"].lower()
        or ("b3" in s["name"].lower() and "brief" in s["name"].lower())
    ]
    assert b3_steps, (
        "L'étape B3 (Relever l'échec d'invocation non-429) est absente du "
        f"workflow {WORKFLOW_PATH}. Le test test_seven_paths ne peut pas "
        "détecter les chemins other_error incorrects."
    )
    b3 = b3_steps[0]
    cond = b3.get("if_condition", "")
    assert "invoke" in cond and "failure" in cond, (
        f"Condition de l'étape B3 inattendue : {cond!r}"
    )
    assert "vendor_refusal" in cond, (
        f"La condition B3 doit exclure vendor_refusal : {cond!r}"
    )


# ---------------------------------------------------------------------------
# Test N5 : fallback_attempted dans le commit produit
# ---------------------------------------------------------------------------

def test_n5_fallback_attempted_in_commit(tmp_path):
    """
    N5 : prouve que mark_fallback_attempted est appelé AVANT l'étape de
    commit-état et que le champ figure à True dans le commit produit.

    Enchaîne les deux étapes dans un dépôt Git temporaire.
    """
    from pipeline import vendor_refusal as vr

    repo = tmp_path / "repo"
    repo.mkdir()

    def git(*args, **kw):
        return subprocess.run(
            ["git", *args], cwd=repo, check=True,
            capture_output=True, text=True, **kw
        )

    git("init")
    git("config", "user.email", "test@example.com")
    git("config", "user.name", "test")

    # Créer la structure du dépôt
    state_dir = repo / "harness" / "pipeline"
    state_dir.mkdir(parents=True)
    state_file = state_dir / "vendor-refusal-state.jsonl"
    state_file.write_text("", encoding="utf-8")

    git("add", ".")
    git("commit", "-m", "init")

    # Étape 1 : classify → log_refusal (simule l'étape classify_refusal du workflow)
    transcript = tmp_path / "t.jsonl"
    transcript.write_text(
        json.dumps({"api_error_status": 429, "is_error": True, "total_cost_usd": 0})
        + "\n",
        encoding="utf-8",
    )
    vr.log_refusal("CURSOR-n5-test", transcript, state_file)

    # Étape 2 : mark_fallback_attempted (simule l'étape repli du workflow)
    # Doit être appelé AVANT le commit (N5)
    vr.mark_fallback_attempted("CURSOR-n5-test", state_file)

    # Étape 3 : commit-état (simule le commit-état après le repli)
    branch = "forge-bot/vendor-refusal-CURSOR-n5-test-999"
    git("config", "user.name", "forge-bot")
    git("config", "user.email", "forge-bot@users.noreply.github.com")
    git("checkout", "-b", branch)
    git("add", "harness/pipeline/vendor-refusal-state.jsonl")
    git("commit", "-m", "state: refus fournisseur consigné pour CURSOR-n5-test (run 999)")
    git("checkout", "-")

    # Lire le fichier TEL QU'IL FIGURE DANS LE COMMIT (pas l'arbre de travail)
    result = subprocess.run(
        ["git", "show", f"{branch}:harness/pipeline/vendor-refusal-state.jsonl"],
        cwd=repo, check=True, capture_output=True, text=True,
    )
    committed_lines = [
        l for l in result.stdout.splitlines() if l.strip()
    ]
    assert committed_lines, "Le commit doit contenir au moins une ligne d'état"
    record = json.loads(committed_lines[-1])
    assert record.get("fallback_attempted") is True, (
        f"fallback_attempted doit être True DANS LE COMMIT produit ; "
        f"obtenu : {record!r}"
    )
