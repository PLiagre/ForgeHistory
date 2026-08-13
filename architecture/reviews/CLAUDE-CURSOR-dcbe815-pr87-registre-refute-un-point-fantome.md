---
review_of: CURSOR-dcbe815-pr87-registre-refute-un-point-fantome
reviewer: claude-code
target_commit: dcbe815817b9838ed79dd0bd9d4fb7e1e55108c2
reviewed_at: 2026-08-13T13:20:00Z
---

# Contre-audit de CURSOR-dcbe815-pr87-registre-refute-un-point-fantome

Rôle : vérifier la *véracité technique* de l'audit, pas sa valeur métier.
Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

Méthode : chaque commande citée par l'audit a été rejouée indépendamment
(API GitHub publique non authentifiée — `gh` n'est pas connecté dans cet
environnement non plus — et lecture/exécution directe des modules
`harness/audit_review.py` / `harness/audit_decision.py` sur ce dépôt).
Aucune sortie collée par l'audit n'a été prise pour argent comptant sans
recalcul.

## 1. Provenance (re-vérifiée)

- target_commit annoncé : `dcbe815817b9838ed79dd0bd9d4fb7e1e55108c2`.
- Le commit existe : **oui**. `curl -s
  https://api.github.com/repos/PLiagre/ForgeHistory/pulls/87` →
  `head.sha = dcbe815817b9838ed79dd0bd9d4fb7e1e55108c2`,
  `merged=true`, `merged_at=2026-08-13T12:55:00Z`,
  `additions=121 deletions=0 changed_files=1` — identique à l'en-tête de
  l'audit.
- Mesures rejouées : la quasi-totalité du § 8 de l'audit (détail
  ci-dessous), plus une mesure absente de l'audit (comparaison
  systématique `parse_verdicts` vs `parse_point_verdicts` sur les 20
  évènements `AUDIT_CHALLENGED` du registre, cf. point 5).

## 2. Verdicts point par point

| # | Point de l'audit | Verdict | Preuve / délimitation |
|---|---|---|---|
| 1 | Diff exact de la PR #87 : 1 fichier, +121/−0, sous `architecture/reviews/` | CONFIRMED | `curl .../pulls/87` → `additions=121 deletions=0 changed_files=1`, chemin unique = le fichier audité lui-même (§ 8.A de l'audit). |
| 2 | PR #77 (cible de la revue auditée) toujours `open`, tête inchangée `f978cc79e2…` | CONFIRMED | `curl .../pulls/77` → `state=open merged=false head.sha=f978cc79e20bbf42678ed2b5f7e811b4490fb88d`, identique au `target_commit` de la revue et à la mesure citée en § 8.B. |
| 3 | CI du commit `dcbe815` : 18 check-runs, 14 `success`, 2 `skipped` (`cursor-scope`), 1 `cancelled` + 1 `queued` (`Reconcile local Hermes state`) | CONFIRMED | `curl .../commits/dcbe815.../check-runs?per_page=100` → total_count 18, décompte exactement identique job par job à celui de § 8.C, y compris `Reconcile local Hermes state` toujours `queued` au moment de ce contre-audit (aucune résolution depuis). |
| 4 | **P1-1** — le registre écrit `{"CONFIRMED":19,"REFUTED":1,"PARTIAL":2,"NEEDS_OWNER":2}` pour l'évènement `AUDIT_CHALLENGED` de `CURSOR-f978cc7-...`, alors que le tableau réel de la revue ne contient que 18 CONFIRMED + 1 PARTIAL (0 REFUTED, 0 NEEDS_OWNER) | CONFIRMED | Ligne du registre lue directement (`grep '"audit_id": "CURSOR-f978cc7' architecture/audit-ledger.jsonl`) : `verdicts` identique au mot près à ce que cite l'audit. Rejeu de `audit_decision.parse_point_verdicts()` sur `architecture/reviews/CLAUDE-CURSOR-f978cc7-pr77-cloture-affirmee-hors-registre.md` → `{'CONFIRMED': 18, 'PARTIAL': 1}`. Le fichier n'a qu'une seule occurrence de `REFUTED`, à la ligne 11 du gabarit (`Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.`) — vérifié par lecture directe. |
| 5 | Cause : `parse_verdicts` (harness/audit_review.py:127-134) compte sur tout le document via `re.findall(r"\bTOKEN\b", text)`, pas sur les lignes de tableau ; le gabarit lui-même (ligne 76 : « Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER. ») et le titre `## 3. ... (NEEDS_OWNER)` (ligne 94) ajoutent +1 à chaque compteur sur toute revue produite depuis le gabarit | CONFIRMED | Lecture de `harness/audit_review.py` : les lignes citées correspondent mot pour mot au code réel (numéros de ligne exacts vérifiés). Rejeu direct de `parse_verdicts()` sur un scaffold vide généré par `write_scaffold()` : chacun des 4 tokens apparaît au moins une fois hors tableau, exactement comme décrit. |
| 6 | Ampleur : 19 évènements `AUDIT_CHALLENGED` sur 20 dans le registre ont un champ `verdicts` différent du tableau réel de leur revue, et 19/20 annoncent au moins un `REFUTED` fantôme | CONFIRMED | Script indépendant comparant, pour les 20 évènements `AUDIT_CHALLENGED` du registre, `audit_review.parse_verdicts(texte)` au décompte réel des lignes `audit_decision.parse_point_verdicts(texte)` : 19/20 mismatch, 19/20 ont `REFUTED > 0` côté registre. Seul `CURSOR-FIXTURE-full-auto-demo` (`{'CONFIRMED': 1}` des deux côtés) est correct — la seule revue à un seul verdict sans texte de gabarit résiduel. |
| 7 | Deux revues (`CURSOR-5633ee7`, `CURSOR-73022bd`) ont un `verdicts` de registre renseigné alors que leur tableau n'a aucune ligne `\| N \| ... \|` lisible par la machine | CONFIRMED | Même script : `CURSOR-5633ee7-automation-completeness` et `CURSOR-73022bd-hermes-dashboard-modele-audite` rendent `table={}` (aucune ligne numérotée valide) alors que le registre porte des compteurs à deux chiffres pour les deux. |
| 8 | **P1-2(1)** — la décision auto `APPROVED` sur `CURSOR-f978cc7-...` retient les 19 points 1-19, y compris le point 19 qui est `PARTIAL` et dont le texte refuse explicitement de trancher (« un arbitrage du propriétaire, pas quelque chose que je peux CONFIRMER ou REFUTER techniquement ») ; les 4 questions en prose du § 3 de la revue n'apparaissent nulle part dans le fichier de décision | CONFIRMED | `architecture/decisions/DECISION-CURSOR-f978cc7-...md` → `retained_points: [1..19]`, `decided_by: policy:auto`. Lecture directe de `architecture/reviews/CLAUDE-CURSOR-f978cc7-...md` § 2 ligne 19 : verdict `PARTIAL`, texte identique à la citation de l'audit. Le fichier de décision ne contient que frontmatter + une ligne de raison + la liste des points retenus — aucune des 4 questions du § 3 de la revue (fusionner PR#77 ? priorité vs `4c45718` ? lecture constat 3 ? segment IMPLEMENTED/VERIFIED ?) n'y figure, confirmé par lecture intégrale du fichier. |
| 9 | **P1-2(2)** — sur 39 audits dans `inbox/` au moment de la fusion : 15 sans aucun évènement au registre, 11 `AUDIT_APPROVED` sans jamais avoir atteint `AUDIT_CONVERTED`, 6 seulement ont atteint `AUDIT_CONVERTED` | CONFIRMED | Reconstruction de l'état exact au commit `8098ee0` (celui qui a écrit l'évènement `AUDIT_CHALLENGED`/`AUDIT_APPROVED` de `CURSOR-f978cc7-...`, donc l'état vu par l'auditeur à la création de son audit 13:04:06Z) : `git ls-tree -r --name-only 8098ee0 -- architecture/inbox` → 39 fichiers. Sur ces 39 id, comptage par « a *déjà atteint* cet état à un moment de son historique » (pas seulement l'état courant, qui sous-compte les audits allés plus loin que CONVERTED) : 15 sans aucun évènement, 17 `AUDIT_APPROVED` un jour dont 11 jamais `AUDIT_CONVERTED`, 6 `AUDIT_CONVERTED`. Les trois chiffres tombent exactement juste. |
| 10 | Liste nominative § 8.G (quels audits nommant `parse_verdicts` sont dans quel état) | CONFIRMED | Rejeu direct sur le registre actuel pour les 14 id cités : les 6 « aucun évènement » (`063d7eb`, `4822662`, `4b6dcff`, `786ec32`, `8894f15`, `949ecf1`, `e2896e7` — 7 en réalité, voir délimitation) et les états `AUDIT_APPROVED`/`AUDIT_CHALLENGED`/`AUDIT_CONVERTED` des autres correspondent exactement à la table de l'audit. **Délimitation** : l'audit dit « quatorze audits nomment la même fonction » mais sa propre table n'en liste que 14 lignes dont je compte 7 « aucun évènement », pas les « huit » annoncés en § 0 — écart mineur de 1 entre le corps (§0 : « huit d'entre eux n'ont même aucun évènement au registre ») et ma recompte (7 sur les 14 listées). Le reste de la mesure est exact ; je classe ceci `PARTIAL` sur ce sous-point précis. |
| 11 | **P2-1** — le run 31695162454 est `success` de bout en bout, y compris l'étape « Publish the review as a pull request », alors qu'une annotation dit `gh pr create refused (repository setting or permissions) -- branch ... is pushed; open the PR manually.` ; 87 minutes entre le push de la revue (11:27:32Z) et l'ouverture manuelle de la PR #87 (12:54:29Z) | CONFIRMED | `curl .../actions/runs/31695162454` → `status=completed conclusion=success`, `created_at=11:21:50Z updated_at=11:27:32Z`. `curl .../actions/runs/.../jobs` → job `invoke-claude-challenger` et son étape 12 « Publish the review as a pull request » tous deux `completed`/`success`. `curl .../check-runs/94431182817/annotations` → texte de l'avertissement identique au caractère près à celui cité par l'audit. `curl .../pulls/87` → `created_at=2026-08-13T12:54:29Z` ; écart avec `11:27:32Z` = 86 min 57 s ≈ 87 min, confirmé. |
| 12 | Cause : le `\|\|` de `pipeline-challenge.yml:197-201` avale l'échec de `gh pr create` sans colorer le job | CONFIRMED | Lecture du fichier : l'étape « Publish the review as a pull request » se termine par `gh pr create ... \|\| echo "::warning::gh pr create refused ..."`, ligne pour ligne identique au texte cité par l'audit. |
| 13 | **P2-2** — l'étape « Invoke claude-challenger headless » (celle qui exécute le contre-audit) ne reçoit que `CLAUDE_CODE_OAUTH_TOKEN`, `ANTHROPIC_API_KEY`, `AUDIT_ID` dans son `env:`, pas de `GH_TOKEN`, contrairement aux étapes kill-switch et publication qui, elles, en reçoivent un | CONFIRMED | Lecture de `.github/workflows/pipeline-challenge.yml` lignes 144-149 : `env:` de l'étape ne contient exactement que ces 3 variables. Comparaison : ligne 60 (kill-switch) a `GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}`, ligne 174 (publication) a `GH_TOKEN: ${{ secrets.FORGE_BOT_PAT \|\| secrets.GITHUB_TOKEN }}`. L'asymétrie est réelle et vérifiable par simple lecture. |
| 14 | **P3-1** — `Reconcile local Hermes state` tourne sur un runner auto-hébergé Windows et a été annulé 3s après la fusion de `dcbe815`, sans avoir tourné sur ce commit | CONFIRMED | `.github/workflows/hermes-observer.yml:32` → `runs-on: [self-hosted, Windows, X64, hermes-observer]`, identique à la citation. `curl .../commits/dcbe815.../check-runs` (point 3 ci-dessus) confirme l'état `cancelled` + `queued` toujours en attente, sans résultat produit sur ce commit. |
| 15 | **P3-2** — 31 s entre l'ouverture de la PR #87 (12:54:29Z) et sa fusion (12:55:00Z) ; `audit_schema.py` ne valide que `architecture/inbox/`, aucun job ne valide `architecture/reviews/**`, et la seule contrainte de contenu est « au moins une ligne `\| N \| … \| VERDICT \| … \|` » | CONFIRMED | `curl .../pulls/87` → `created_at=12:54:29Z merged_at=12:55:00Z`, écart = 31 s exact. Lecture de `harness/audit_schema.py` : `INBOX = REPO_ROOT / "architecture" / "inbox"`, aucune référence à `reviews/` dans le module. `grep -rn "audit_schema\|architecture/reviews" .github/workflows/*.yml` : seul `audit-guard.yml:26` invoque `audit_schema.py`, sans argument pointant vers `reviews/`. La seule contrainte de contenu réelle est celle de `audit_review.record_challenge()`, qui exige `audit_decision.parse_point_verdicts(text)` non vide — une ligne `\| N \| ... \|` suffit. |
| 16 | Rejeu du job `mechanical-scaffold-smoke` : une seule ligne de verdict réelle (`\| 1 \| mock point \| CONFIRMED \| ... \|`) produit 6 verdicts comptés au registre (`{'CONFIRMED': 2, 'REFUTED': 1, 'PARTIAL': 1, 'NEEDS_OWNER': 2}`), et le job ne vérifie que la présence de l'évènement (`grep -q AUDIT_CHALLENGED`) | CONFIRMED | Rejeu indépendant de la fixture exacte du job (même gabarit, même substitution des marqueurs de gabarit non remplis → texte, même ligne de tableau ajoutée) via `audit_review.write_scaffold()` + `parse_verdicts()` : sortie `{'CONFIRMED': 2, 'REFUTED': 1, 'PARTIAL': 1, 'NEEDS_OWNER': 2}`, identique au chiffre cité par l'audit. Le job ne fait bien que `grep -q AUDIT_CHALLENGED` (lu directement dans `pipeline-challenge.yml`), aucune assertion sur les comptes. |
| 17 | § 4 — contre-vérification du point 10 de la revue : `test_audit_archive.py` a 8 fonctions de test, 0 occurrence de `sha256`/`filecmp` | CONFIRMED | `grep -c "^def test" harness/tests/test_audit_archive.py` → 8. `grep -c "sha256\|filecmp" harness/tests/test_audit_archive.py` → 0. Identique à § 8.J de l'audit. |
| 18 | § 4 — aucun secret, `gitleaks` vert sur les deux exécutions, aucune dépendance introduite | CONFIRMED | Confirmé par le tableau CI du point 3 ci-dessus : `gitleaks` `success` ×2. Le diff (point 1) est un unique fichier Markdown sous `architecture/reviews/`, sans import ni outil ajouté. |
| 19 | Synthèse — classification de sévérité (2×P1, 2×P2, 2×P3, 0×P0) et recommandation de ne pas bloquer la fusion | PARTIAL | Les six constats sous-jacents se reproduisent tous (points 4-16 ci-dessus) ; la classification P0-P3 elle-même est un jugement de gravité que l'audit qualifie lui-même de « discutable » (§ 6) — je n'ai pas de contre-preuve technique contre ce classement, mais ce n'est par construction pas un fait vérifiable au sens strict. Je suis d'accord qu'aucun de ces six constats ne casse aujourd'hui un comportement produit observable (0×P0 est défendable). |
| 20 | § 7 — les trois briefs proposés sont techniquement cohérents avec les constats qu'ils couvrent (brief 1 → P1-1, brief 2 → P2-1+P2-2, brief 3 → P1-2) et n'outrepassent pas les trois flags `*_authorized: false` du frontmatter | CONFIRMED | Frontmatter de l'audit vérifié : `implementation_authorized: false`, `ci_changes_authorized: false`, `code_changes_authorized: false`. Chaque brief proposé cible bien le constat qu'il annonce couvrir, sans revendiquer d'autorisation d'exécution nulle part dans le texte (relu en entier). |

## 3. Points à porter au propriétaire (NEEDS_OWNER)

- **Le seuil de déclenchement d'un futur compteur de débit (brief 3)** —
  avertir seulement, ou suspendre l'automatisation `full_auto` au-delà
  d'un certain nombre d'`AUDIT_APPROVED` non convertis — est un arbitrage
  de politique produit, pas une question technique.
- **La priorité relative entre les trois briefs proposés ici et le
  backlog déjà retenu et non converti de `CURSOR-4c45718`** (10 points
  approuvés, jamais transformés en brief) : l'audit recommande de
  consolider plutôt que dupliquer, mais l'ordre de traitement reste un
  choix du propriétaire.
- **Faut-il réécrire les 19 lignes déjà fausses du registre append-only ?**
  L'audit répond lui-même « non, c'est un arbitrage propriétaire » — je
  suis d'accord qu'aucune correction technique rétroactive du journal
  n'est possible sans en briser la propriété append-only ; reste à décider
  si une ligne de correction *additive* (« ce compte du DD/MM était faux,
  voir ce fichier ») vaut la peine.

## 4. Synthèse

Cet audit se reproduit intégralement. J'ai rejoué, indépendamment et sans
utiliser les sorties collées comme référence, chaque commande vérifiable :
les 18 check-runs et leurs états exacts, les deux timestamps de la
PR #87 (ouverture 12:54:29Z, fusion 12:55:00Z, 31 s), le run du
challenger (11:21:50Z → 11:27:32Z, `success` de bout en bout) et son
annotation d'avertissement mot pour mot, le contenu exact de l'étape sans
`GH_TOKEN` (lignes 144-149), le `\|\|` qui avale l'échec de publication, le
runner Windows auto-hébergé, et surtout le cœur technique : la ligne de
registre `{"CONFIRMED":19,"REFUTED":1,"PARTIAL":2,"NEEDS_OWNER":2}` face à
un tableau réel de 18 CONFIRMED + 1 PARTIAL, 0 REFUTED. J'ai en plus
recalculé, sur les 20 évènements `AUDIT_CHALLENGED` du registre entier
(pas seulement celui cité), le même écart : 19/20 faux, 19/20 avec un
`REFUTED` fantôme — le chiffre de l'audit tombe exactement juste. La
mesure de débit du backlog (39 audits / 15 sans évènement / 11 approuvés
non convertis / 6 convertis) tombe elle aussi exactement juste, à
condition de compter « a atteint cet état au moins une fois dans son
histoire » plutôt que l'état courant seul — c'est la méthode qu'utilise
implicitement l'audit et c'est la bonne (l'état courant seul sous-compte
les audits allés plus loin que `AUDIT_CONVERTED`).

Rien ne tombe parmi les six constats numérotés. Le seul point où je
nuance est la classification de sévérité elle-même (point 19,
`PARTIAL`) — un jugement, pas une mesure, comme l'audit le reconnaît
lui-même — et un écart mineur d'une unité entre le chiffre « huit sans
évènement » de la synthèse (§ 0) et mon recomptage direct de sa propre
liste nominative (§ 8.G), qui n'en donne sept sur les quatorze id cités
(point 10, `PARTIAL`). Aucun des deux écarts n'affecte la conclusion
centrale de l'audit.

Recommandation : cet audit est un candidat solide pour APPROVED. Le
brief 1 (dériver `verdicts` du registre de `parse_point_verdicts` plutôt
que de `parse_verdicts`) est le plus urgent et le plus simple des trois —
il corrige la source du défaut à la racine, avec un test rouge déjà
esquissé par l'audit (comptage attendu `{CONFIRMED: 18, PARTIAL: 1}` sur
le fichier réel de cette PR). Les briefs 2 et 3 restent des propositions
raisonnables mais engagent des arbitrages listés en § 3 ci-dessus.
