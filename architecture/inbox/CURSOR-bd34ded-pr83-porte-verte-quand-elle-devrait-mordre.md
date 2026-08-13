---
audit_id:                CURSOR-bd34ded-pr83-porte-verte-quand-elle-devrait-mordre
auditor:                 cursor-cloud
target_branch:           master
target_commit:           bd34dedbb713863d7f9bfa8f9341975aa01291d6
created_at:              2026-08-13T12:55:00Z
audit_type:              pr-critique
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---

# Critique de la pull request #83 — « Brief 014 : le contre-audit comme porte observable, le refus fournisseur comme état explicite avec repli »

<https://github.com/PLiagre/ForgeHistory/pull/83>

Cet audit est une **proposition**. Il n'instruit rien, n'autorise rien et ne
décide rien : la décision appartient à la boucle (`architecture/README.md`,
ADR-0005/0006). Les trois drapeaux `*_authorized` du frontmatter sont à
`false`.

---

## 0. Ce qu'il faut retenir en trois phrases

1. La PR ajoute une garde de sécurité à la CI et **casse au passage une porte
   mécanique de sécurité existante** : `actionlint` était vert sur `master`,
   il est rouge sur cette PR parce que le nouveau job interpole
   `${{ github.head_ref }}` — une valeur contrôlée par l'auteur d'une PR —
   directement dans un script shell.
2. La porte annoncée (« un audit non adjugé qui cible la PR rend le contrôle
   rouge ») est **verte précisément dans la fenêtre où elle devrait mordre** :
   l'appariement se fait sur le SHA de tête exact, donc un seul commit de plus
   sur la PR efface l'appariement — et la PR #83 elle-même passe son propre
   contrôle en vert pendant que cette critique est écrite.
3. Le « repli Codex » du volet B **ne peut jamais aboutir** : le CLI `codex`
   n'est installé nulle part dans `pipeline-challenge.yml`, contrairement à
   `pipeline-forge-run.yml` qui, lui, l'installe et amorce son
   `auth.json` ; la seule preuve produite pour ce volet est un simulateur de
   GitHub Actions écrit par le même acteur, pas une exécution.

---

## Provenance et périmètre audité

| élément | valeur |
|---|---|
| PR | #83, `forge/014-pipeline-contre-audit-porte-e180` → `master` |
| SHA de tête audité | `bd34dedbb713863d7f9bfa8f9341975aa01291d6` |
| base | `e0dcb4fb69e83e72f339295c296cd96241dfe7d7` |
| diff | 22 fichiers, +4691 / −26 |
| état à l'audit | ouverte, non brouillon, `mergeStateStatus: BLOCKED` |
| commits | 7 (Planificateur, 3× Générateur, 3× Évaluateur) |

Toutes les commandes de cet audit ont été rejouées sur un arbre de travail
positionné sur `bd34ded` (`git worktree add /tmp/pr83
refs/remotes/origin/forge/014`), avec `.venv/bin/python`. Les sorties sont
collées telles quelles.

---

## 1. Lentille 1 — Intention avant diff

L'intention est lisible et sourcée : le brief `014-pipeline-contre-audit-porte`
naît de l'audit `CURSOR-a600532-fusion-sans-contre-audit`, points P0-1 et P1-1.
Le corps de la PR annonce deux volets clairement séparés :

- **Volet A** : « pour une PR donnée […] détecte les audits de
  `architecture/inbox/` qui la ciblent et vérifie leur adjudication au ledger ;
  code de sortie non nul si un audit non adjugé cible la PR ».
- **Volet B** : « sur 429, l'échec d'invocation ne tue plus le job […] ; repli
  `codex exec` sous les mêmes gardes […] ; jamais de succès simulé ».

Le diff **livre du code** pour ces deux intentions. Ce que la suite de cet
audit montre, c'est que les deux mécanismes livrés ne produisent pas l'effet
annoncé dans les conditions réelles où ils sont censés s'exercer (§ 3 et § 5).
La critique ne porte donc pas sur l'absence d'intention — elle est claire — mais
sur l'écart entre l'intention écrite et le comportement mesuré.

Un point d'intention manque néanmoins : le brief définit l'appariement
audit ↔ PR (« `--head-branch ${{ github.head_ref }}` », brief.md:85) sans
jamais énoncer **la propriété que cet appariement doit garantir**. La rubrique
d'évaluation reprend la même formulation (« doit appeler `pr_audit_guard.py
check` avec `github.head_ref` et `github.event.pull_request.head.sha` — pas de
valeurs en dur », eval-rubric.md:93). Elle vérifie donc *que les bons arguments
sont passés*, jamais *que la porte devient rouge quand un audit non adjugé
existe*. C'est la racine de § 3 : la rubrique mesure la forme, pas l'effet.

---

## 2. Lentille 3 — Portes mécaniques : classification de la CI du commit audité

`gh pr checks 83` sur `bd34ded` :

```
actionlint            fail   10s  .../runs/31701645228/job/94451975789
actionlint            fail   12s  .../runs/31701797271/job/94452481502
audit-check           skipping 0  .../runs/31701645204/job/94452003294   (push)
audit-check           pass   13s  .../runs/31701797307/job/94452481777   (pull_request)
schema                pass    9s
cursor-scope          skipping 0
gitleaks              pass   13s
tests                 pass   25s
sim-tests             pass   20s
f0-demo               pass   11s
invoke-cursor-auditor pass   17s
```

**La CI du commit audité est rouge.** Le workflow `security` échoue sur les
deux runs de ce commit (push `31701645228`, pull_request `31701797271`), sur le
job `actionlint`. Les autres portes (`tests`, `sim-tests`, `f0-demo`,
`schema`, `gitleaks`) sont vertes.

Le même workflow `security` est **vert sur `master`** :

```
completed  success  hermes: tableau de bord régénéré                    security  master  push  31702071883
completed  success  challenge: revue CLAUDE-CURSOR-827d54e-...          security  master  push  31702047319
completed  success  hermes: tableau de bord régénéré                    security  master  push  31701957497
completed  success  pipeline-orchestrate: review_recorded               security  master  push  31701934399
```

La régression est donc **introduite par cette PR**, et elle l'est dès son
premier push (`31700710653` échoue déjà). Détail au § 3.

Ni le `verdict.md` (943 lignes), ni le `generator-log.md`, ni les deux
feedbacks ne mentionnent `actionlint`, `injection` ou la rougeur du workflow
`security` :

```
$ rg -n -i "actionlint|injection|head_ref" harness/queue/briefs/014-pipeline-contre-audit-porte/
verdict.md:121:      … appelle la garde avec `${{ github.head_ref }}` et `${{ github.event.pull_request.head.sha }}` …
brief.md:85:          - `--head-branch ${{ github.head_ref }}`
generator-log.md:92:  … Il appelle `pr_audit_guard.py check` avec `github.head_ref` et `github.event.pull_request.head.sha`.
eval-rubric.md:93:    … doit appeler `pr_audit_guard.py check` avec `github.head_ref` et …
```

Autrement dit : l'Évaluateur a explicitement **contrôlé la présence de
`${{ github.head_ref }}` dans un `run:` et l'a comptée comme PASS** (SC2), là
où c'est précisément le motif que le linter de sécurité du dépôt interdit.
C'est un cas net de la lentille 3 : du jugement (agent) a été dépensé à
re-vérifier une forme, pendant qu'une porte mécanique déjà en place disait
« non » sans être lue.

---

## 3. P0-1 — La PR introduit une injection de commande shell dans un workflow, et rend rouge la porte de sécurité du dépôt

### Le fait

`.github/workflows/audit-guard.yml`, job `audit-check` ajouté par cette PR
(lignes 51-65 du fichier au SHA audité) :

```yaml
      - name: Vérifier les audits non adjugés ciblant cette PR
        run: |
          set -euo pipefail
          python harness/pipeline/pr_audit_guard.py check \
            --head-branch "${{ github.head_ref }}" \
            --head-commit "${{ github.event.pull_request.head.sha }}"
```

Sortie exacte du job `actionlint` (run `31701797271`, `gh run view … --log-failed`) :

```
.github/workflows/audit-guard.yml:61:103: "github.head_ref" is potentially
untrusted. avoid using it directly in inline scripts. instead, pass it through
an environment variable. see https://docs.github.com/en/actions/reference/
security/secure-use#good-practices-for-mitigating-script-injection-attacks for
more details [expression]
##[error]Process completed with exit code 1.
```

### Pourquoi c'est un défaut de fond et pas du bruit de linter

`${{ … }}` est **substitué textuellement dans le script shell avant son
exécution** (documentation GitHub, S6). `github.head_ref` est le nom de la
branche de tête d'une PR : une valeur que **n'importe qui ouvrant une PR
choisit librement**. Un nom de branche contenant un guillemet suivi d'un
`;` referme la chaîne et exécute la suite sur le runner. `github.head_ref`
figure nommément dans la liste des entrées non fiables de GitHub (S6) et dans
`BuiltinUntrustedInputs` d'actionlint (S7).

Le déclencheur du workflow est `pull_request` (et non `pull_request_target`),
donc le `GITHUB_TOKEN` d'une PR issue d'un fork est en lecture seule et les
secrets ne sont pas exposés : le rayon d'action est réduit. Il reste
l'exécution de code arbitraire sur le runner, dans un dépôt dont
`audit-guard.yml` est justement *le fichier qui garde la frontière de
confiance* entre auditeur et développeur. C'est le seul endroit du dépôt où ce
motif est acceptable en dernier.

### Élément aggravant : le motif correct est déjà dans le même fichier

Trente lignes plus haut, le job `cursor-scope` fait exactement ce qu'il faut :

```yaml
      - name: A Cursor PR may only touch architecture/inbox/
        env:
          BASE_REF: ${{ github.base_ref }}
        run: |
          set -euo pipefail
          git fetch --no-tags origin "$BASE_REF"
```

La valeur passe par `env:` et le script ne lit qu'une variable shell. Le
nouveau job n'a pas repris la convention déjà écrite juste au-dessus de lui.
Le même fichier contient donc, désormais, le motif sûr et le motif dangereux.

**Sévérité : P0 — bloque la fusion.** Une porte mécanique du dépôt est rouge
sur le commit à fusionner, et la cause est une vulnérabilité d'injection
introduite par la PR, dans un fichier de CI.

---

## 4. P0-2 — La porte `audit-check` est verte exactement dans la fenêtre où elle devrait mordre

### Ce que la porte est censée faire

Corps de la PR : « pour une PR donnée (branche de tête, commit de tête),
détecte les audits de `architecture/inbox/` qui la ciblent et vérifie leur
adjudication au ledger ; code de sortie non nul si un audit non adjugé cible
la PR ».

### La règle d'appariement, telle qu'elle est codée

`harness/pipeline/pr_audit_guard.py`, `_targets_pr` :

```python
def _targets_pr(frontmatter: dict, head_branch: str, head_commit: str) -> bool:
    target_branch = frontmatter.get("target_branch", "")
    if target_branch and target_branch == head_branch:
        return True
    target_commit = frontmatter.get("target_commit", "")
    if target_commit and head_commit and target_commit[:7] == head_commit[:7]:
        return True
    return False
```

Deux règles, et **les deux sont inopérantes pour le cas d'usage visé** :

**(a) La règle « branche » est morte pour les PR.** Tous les audits de PR déjà
déposés portent `target_branch: master` — y compris ceux qui critiquent
explicitement une PR :

```
$ rg -N "^target_branch:" architecture/inbox/*.md | sort | uniq -c
      1 CURSOR-9e35764-pr63-contre-audit-jamais-enregistre.md:target_branch:  master
      1 CURSOR-e2896e7-pr44-challenge-bb8fe11.md:target_branch:               master
      1 CURSOR-cd1dcd2-forge-bot-pat-boucle-jetons.md:target_branch:          master
      … (18 sur 20 à `master`)
      1 CURSOR-bb8fe11-hermes-console-adr-0011.md:target_branch: forge/hermes-decision-adr-0011-c2dd
      1 CURSOR-ab0e7f0-pr62-verdicts-perimes-a-la-fusion.md:target_branch: forge-bot/review-…-31673848038
```

Or `github.head_ref` d'une PR ne vaut jamais `master`. La règle (a) ne peut
donc pas se déclencher sur le corpus existant. (Symétriquement, si une PR
partait d'une branche nommée `master` — un fork —, *tous* les audits du dépôt
s'apparieraient d'un coup.)

**(b) La règle « commit » s'efface au premier commit suivant.** Elle compare
les 7 premiers caractères du `target_commit` de l'audit au SHA de tête
**courant** de la PR. Or le cycle normal est : audit déposé sur le SHA n →
l'auteur corrige → SHA n+1. Démonstration, sur une inbox et un ledger de test
isolés (donc rejouable sans toucher au dépôt) :

```
$ .venv/bin/python harness/pipeline/pr_audit_guard.py check \
    --head-branch forge/ma-pr \
    --head-commit abcdef0123456789012345678901234567890123 \
    --inbox /tmp/demo83/inbox --ledger /tmp/demo83/ledger.jsonl
ERREUR : audits ciblant cette PR, non adjugés :
  CURSOR-abcdef0-demo: PROPOSED (aucune ligne au ledger)
1 audit(s) non adjugé(s) cible(nt) cette PR — la décision doit être prise avant la fusion (contrôle rouge).
exit=1

$ .venv/bin/python harness/pipeline/pr_audit_guard.py check \
    --head-branch forge/ma-pr \
    --head-commit 9999999999999999999999999999999999999999 \
    --inbox /tmp/demo83/inbox --ledger /tmp/demo83/ledger.jsonl
Aucun audit ne cible cette PR — contrôle vert.
exit=0
```

Même PR, même audit toujours `PROPOSED`, **un commit de plus** : rouge → vert.
La porte se désarme d'elle-même au premier geste de correction — c'est-à-dire
au moment exact où l'audit vient d'être publié et où la décision n'a pas encore
été prise.

### Une troisième cause, indépendante : l'audit n'est pas dans l'arbre que la porte lit

Le job `audit-check` fait `actions/checkout` sur l'événement `pull_request`,
donc lit `architecture/inbox/` **du merge-ref de la PR auditée**. Or un audit
`cursor-auditor` est déposé dans une **PR `cursor/*` distincte**, non
fusionnée. Tant que cette PR d'audit n'est pas dans `master`, l'inbox vue par
la porte ne contient pas l'audit.

Preuve *in situ*, sur la PR #83 elle-même :

```
$ .venv/bin/python harness/pipeline/pr_audit_guard.py check \
    --head-branch forge/014-pipeline-contre-audit-porte-e180 \
    --head-commit bd34dedbb713863d7f9bfa8f9341975aa01291d6
Aucun audit ne cible cette PR — contrôle vert.
exit=0
```

et la CI le confirme : `audit-check   pass   13s` sur la PR #83. **La PR qui
installe la porte passe sa propre porte en vert, pendant que la critique qui la
cible est en train d'être écrite.** C'est la démonstration la plus économique
que le mécanisme ne capte pas son cas nominal.

### Ce qui n'est pas reproché

La porte n'est pas *inutile* : elle fonctionne pour un audit `post-merge`
déposé sur un SHA de `master` déjà fusionné, ou pour une PR figée après
l'audit. Et elle a été correctement câblée sur le plan des événements
(`skipping` sur `push`, `pass` sur `pull_request`). Le reproche est que le
scénario majoritaire — critiquer une PR vivante — n'est pas couvert.

**Sévérité : P0.** L'affirmation centrale du volet A (« le contre-audit comme
porte ») est démentie sur son cas nominal, et l'écart n'est signalé nulle part
dans le verdict. Le brief documente une limite (« observable, pas
contraignante » — protection de branche indisponible) ; cette limite-là est
honnête et différente : elle dit que le rouge n'empêche pas la fusion, pas que
le rouge ne se produit pas.

---

## 5. P1-1 — Le repli Codex ne peut pas aboutir : le CLI n'est installé nulle part dans ce workflow

### Le fait

`pipeline-challenge.yml` ajoute (lignes 218-267) une étape « Repli Codex si
refus fournisseur » dont le cœur est :

```yaml
          if codex exec "/forge-audit-review ${AUDIT_ID}" 2>&1; then
```

Or les étapes d'installation de ce même job ne posent que Claude :

```
$ git show refs/remotes/origin/forge/014:.github/workflows/pipeline-challenge.yml | rg -n "npm install"
141:          npm install -g @anthropic-ai/claude-code
```

Aucune occurrence de `@openai/codex` dans ce fichier, et aucune étape
d'amorçage de `~/.codex/auth.json` :

```
$ git grep -n -i "codex" refs/remotes/origin/forge/014 -- .github/workflows/pipeline-challenge.yml
… seules les lignes 218-271 de l'étape de repli ; aucune install, aucun auth.json
```

Le dépôt sait pourtant faire : `pipeline-forge-run.yml` installe le CLI **et**
amorce son fichier d'authentification :

```yaml
      - name: Install Claude Code and Codex CLIs
        run: npm install -g @anthropic-ai/claude-code @openai/codex
             claude --version
             codex --version

      - name: Bootstrap Codex subscription auth (auth.json)
        env:
          CODEX_AUTH_JSON: ${{ secrets.CODEX_AUTH_JSON }}
        run: |
          printf '%s' "$CODEX_AUTH_JSON" > "$HOME/.codex/auth.json"
          codex login status
```

La nouvelle étape, elle, se contente d'exporter `CODEX_AUTH_JSON` en variable
d'environnement — ce n'est pas le canal d'authentification du CLI Codex, qui
lit `~/.codex/auth.json`.

### Le chemin réel, déroulé

1. Claude retourne 429 → `classify_refusal` = `vendor_refusal`. ✔
2. L'étape de repli entre. La garde d'identifiants passe, car
   `secrets.CODEX_AUTH_JSON` **est bien renseigné** dans ce dépôt (c'est ce
   secret qu'utilise `pipeline-forge-run.yml`). Le message
   `::warning::Repli Codex indisponible — identifiants absents` **n'est donc
   pas émis**.
3. `ci_budget_guard.py precheck` passe.
4. `codex exec …` → `command not found`, code 127. `set -euo pipefail` ne
   s'applique pas à la condition d'un `if`, la branche `else` est prise :
   `::warning::Repli Codex échoué (ou absent) …` puis `exit 1`.

Résultat : le job est rouge, l'état 429 est consigné, mais **aucun contre-audit
n'est produit** — c'est-à-dire exactement l'état d'avant la PR, plus une ligne
de journal. Le corps de la PR annonce pourtant « repli `codex exec` sous les
mêmes gardes (kill-switch, mode, budget) » comme un livrable du lot.

### Ce que le Générateur savait

`deliverables/generator-log.md:161` :

> Dérogation : `codex --version` n'est pas disponible sur ce runner (exit 127).
> Le workflow émettra `::warning::Repli Codex indisponible — identifiants
> absents` en CI.

La première phrase est le constat exact. La seconde est un **non-séquitur** :
elle confond « binaire absent » et « identifiants absents », qui sont deux
branches différentes du script — et la seconde n'est prise que si les secrets
sont vides, ce qu'ils ne sont pas. Le brief avait pourtant listé cette
impossibilité comme dérogation recevable (brief.md:281, colonne « erreur
attendue : la sortie contient `exit:127` ») — la dérogation portait sur la
machine de développement, pas sur le runner CI, et personne n'a fait le pas
suivant : *si le binaire manque en développement, qui l'installe en CI ?*

**Sévérité : P1** (à corriger avant fusion sauf dérogation). Le mécanisme ne
crée pas de risque nouveau — il échoue en rouge, pas en faux vert — mais le
livrable annoncé est inerte, et c'est le motif « correction hallucinée » de la
lentille 6 : succès affirmé, jamais mesuré.

---

## 6. P1-2 — L'état du refus fournisseur n'atteint jamais `master`, et le test qui le « prouve » regarde volontairement ailleurs

### L'exigence

Brief 014, § SC3 : le fichier `harness/pipeline/vendor-refusal-state.jsonl` est
« suivi par git […] — la preuve d'un refus doit être consultable depuis un
clone, pas seulement dans le log du run ».

### Ce que fait le workflow

Étape « Commit état du refus fournisseur » (lignes 269-298) :

```bash
          branch="forge-bot/vendor-refusal-${AUDIT_ID}-${GITHUB_RUN_ID}"
          git checkout -b "$branch"
          git add harness/pipeline/vendor-refusal-state.jsonl
          git commit -m "state: refus fournisseur consigné pour ${AUDIT_ID} …"
          git push -u origin "$branch" || echo "::warning::push … échoué …"
          git checkout -
```

Puis l'étape « Publish the review », plus bas, ajoute :

```bash
          git add architecture/reviews
          git add harness/pipeline/vendor-refusal-state.jsonl     # ← ajouté par cette PR
```

Le `git checkout -` de la première étape **restaure le fichier à l'état de
`HEAD`** : la modification vit désormais uniquement sur la branche
`forge-bot/vendor-refusal-*`. Le `git add` de l'étape de publication est donc
un no-op. Bac à sable rejouable :

```
$ echo '{"audit_id":"X","error_type":"vendor_refusal"}' >> state.jsonl
$ git status --porcelain
 M state.jsonl
$ git checkout -b forge-bot/vendor-refusal-X-123 && git add state.jsonl && git commit -qm state && git checkout -q -
$ git status --porcelain
(vide)
$ cat state.jsonl
(fichier vide — la ligne n'est plus là)
```

Conséquences :

- la ligne d'état **n'entre pas** dans la PR de revue ;
- la branche `forge-bot/vendor-refusal-*` n'est **jamais transformée en PR**
  (aucun `gh pr create` dans cette étape, contrairement à l'étape de
  publication) et n'est jamais fusionnée ;
- donc l'état n'arrive jamais sur `master`. Il est « consultable depuis un
  clone » au sens littéral (la branche existe sur le remote) mais reste hors du
  seul lieu que quiconque, humain ou machine, consulte.

C'est le motif que la boucle d'audit a déjà rencontré à plusieurs reprises
(`CURSOR-7e5244b-ledger-post-fusion-poussee-master`,
`CURSOR-827d54e-contre-audit-paye-jamais-publie`) : **une branche poussée n'est
pas un livrable**. L'élément neuf ici est le chemin précis (`git checkout -`
qui annule l'ajout suivant), pas le motif.

### Le test censé couvrir ce point mesure explicitement autre chose

`harness/tests/test_pipeline_challenge_paths.py::test_n5_fallback_attempted_in_commit`
rejoue la séquence dans un dépôt temporaire, puis :

```python
    git("checkout", "-")

    # Lire le fichier TEL QU'IL FIGURE DANS LE COMMIT (pas l'arbre de travail)
    result = subprocess.run(
        ["git", "show", f"{branch}:harness/pipeline/vendor-refusal-state.jsonl"], …)
```

Le commentaire dit à voix haute que le test contourne l'arbre de travail. Il
constate donc que le commit sur la branche jetable contient bien
`fallback_attempted: true` — ce qui est vrai — et **ne peut pas** détecter que
cette même opération vide l'arbre de travail pour les étapes suivantes. Le test
est vert, et le défaut qu'il aurait dû attraper est à une ligne de son
assertion.

**Sévérité : P1.**

---

## 7. P1-3 — La preuve centrale du volet B est un simulateur écrit par l'acteur lui-même, et un de ses sept chemins est irréalisable

`harness/tests/test_pipeline_challenge_paths.py` (550 lignes) lit le vrai YAML
puis **ré-implémente les règles de GitHub Actions** pour prédire la conclusion
du job :

```python
#   2. continue-on-error: true → outcome reste 'failure', conclusion = 'success' ;
#   3. Le job échoue dès qu'une conclusion d'étape est 'failure'.
def _has_status_func(cond: str) -> bool: …
def _eval_condition(cond, step_outcomes, job_ok, ctx) -> bool: …
def simulate_job(workflow_path, ctx) -> tuple[str, list[str]]: …
```

C'est une vraie amélioration par rapport à une simple lecture de YAML, et elle
attrape réellement le défaut B3 (le test rougit si l'étape terminale est
retirée — la paire rouge/verte est committée dans `proof_red/`). Deux réserves
tout de même, au titre de la lentille 2 (« preuve d'exécution, pas
d'affirmation ») :

1. **Le modèle et le code testé ont le même auteur.** Si la sémantique
   d'Actions est mal modélisée (par exemple le `success()` implicite, ou
   l'interaction `continue-on-error` × `!cancelled()`), le test est vert et le
   workflow reste faux. Aucun run réel de `pipeline-challenge` n'est cité comme
   validation du modèle : les trois runs cités par le corps de la PR
   ([31694643198](https://github.com/PLiagre/ForgeHistory/actions/runs/31694643198),
   [31694909507](https://github.com/PLiagre/ForgeHistory/actions/runs/31694909507),
   [31694993448](https://github.com/PLiagre/ForgeHistory/actions/runs/31694993448))
   sont antérieurs au correctif et servent à établir le 429, pas à valider les
   nouveaux chemins.
2. **Un des sept chemins n'existe pas.** Le chemin « repli réussi → vert » est
   piloté par un booléen de scénario :

   ```python
   if sid == "codex_fallback" or "repli codex" in name:
       return "success" if ctx.get("codex_succeeds", False) else "failure"
   ```

   Or `codex_succeeds=True` est impossible en CI tant que le binaire n'est pas
   installé (§ 5). Le tableau des chemins déclare donc vert un état que le
   système ne peut pas atteindre — ce qui donne l'impression d'une couverture
   complète alors que la moitié du volet B n'a jamais tourné.

**Sévérité : P1.**

---

## 8. Constats P2

### P2-1 — Taille du lot : deux sujets indépendants dans une seule PR

22 fichiers, +4691/−26. Le volet A (porte d'audit sur PR : `pr_audit_guard.py`,
`audit-guard.yml`, un test) et le volet B (refus fournisseur :
`vendor_refusal.py`, `pipeline-challenge.yml`, deux tests) n'ont **aucune
dépendance technique** entre eux ; ils partagent seulement un audit d'origine.

La lentille 5 fixe le seuil à « ~5 fichiers ou quelques centaines de lignes ».
Même en retirant les livrables de harnais (brief, rubrique, verdict, feedbacks,
journal — 2713 lignes à eux seuls, non exécutables), le code + CI + tests pèse
encore ~1800 lignes réparties sur 11 fichiers. C'est la discipline
`NEEDS_SPLIT` que le harnais applique déjà côté briefs, non appliquée ici.

Effet concret et mesurable de ce non-découpage : la revue a dû connecter à la
fois une garde de CI, un classificateur de transcript, un simulateur d'Actions
et une chorégraphie git à trois branches — et le défaut le plus simple de tous
(§ 3, une interpolation dans un `run:`) est passé au travers de trois
itérations Générateur ↔ Évaluateur.

**Sévérité : P2.**

### P2-2 — Le lecteur de frontmatter casse sur le format documenté par `architecture/README.md`

`_parse_frontmatter` coupe sur le premier `:` et ne retire ni guillemets ni
commentaire en ligne. Or l'exemple de référence du schéma
(`architecture/README.md`, lignes 66-79) porte des commentaires en ligne :

```
$ .venv/bin/python -c "…" <<< "<l'exemple exact du README>"
target_branch analyse : 'master                              # branche auditée'
created_at   analyse : '2026-08-03T18:44:03Z                # ISO 8601 UTC'
appariement branche master : False
```

Un audit rédigé exactement comme le README le documente ne serait donc pas
apparié par la règle « branche ». L'appariement par commit survivrait (la
comparaison porte sur les 7 premiers caractères), mais silencieusement : la
garde ne signale jamais un frontmatter qu'elle n'a pas su lire. Le dépôt a
déjà un lecteur de frontmatter validé, `harness/audit_schema.py`, non réutilisé.

**Sévérité : P2.**

### P2-3 — `audit_ledger` importé par manipulation de `sys.path`

`pr_audit_guard.py` fait `sys.path.insert(0, str(HARNESS))` puis
`import audit_ledger` au niveau module. Cela fonctionne (34 tests verts) mais
place `harness/` en tête du chemin d'import pour tout le processus appelant, y
compris sous pytest où les tests des deux paquets coexistent. Le brief
anticipait la difficulté (dérogation « `audit_ledger` n'est pas importable
depuis `harness/pipeline/` », brief.md) sans trancher la forme. Aucun défaut
observé aujourd'hui ; à surveiller si `harness/pipeline/` gagne un module
homonyme d'un module de `harness/`.

**Sévérité : P2.**

---

## 9. Ce qui tient (P3 — information, pas constat)

Ces points ont été rejoués et **tiennent** ; ils sont consignés pour que la
critique ne soit pas lue comme un rejet global.

- **Les 34 tests nouveaux passent**, en 0,14 s, sans réseau :

  ```
  $ .venv/bin/python -m pytest harness/tests/test_pr_audit_guard.py \
      harness/tests/test_vendor_refusal.py \
      harness/tests/test_pipeline_challenge_paths.py -q
  34 passed in 0.14s
  ```

- **La boucle à trois rôles a réellement mordu.** Deux verdicts REJECT
  documentés avant le PASS, avec des motifs techniques justes et non
  cosmétiques : B1 (étapes inatteignables à cause du `success()` implicite des
  conditions `if:`), B3 (un échec non-429 rendait le job vert sans revue). Ces
  deux défauts étaient réels et sont corrigés. La paire rouge/verte de B3 est
  committée dans `harness/pipeline/proof_red/`.

- **La classification 429 est correcte** sur les fixtures, et le cas « fichier
  vide ou inexistant » retourne `other_error` — pas de succès simulé.

- **Le câblage événementiel de `audit-check` est juste** : `skipping` sur
  `push`, `pass` sur `pull_request`, conforme à `if: github.event_name ==
  'pull_request'`. Aucun fichier `pipeline-*.yml` nouveau n'a été créé,
  conformément à la contrainte du brief.

- **Aucune régression sur les autres portes** : `tests`, `sim-tests`,
  `f0-demo`, `schema`, `gitleaks` sont verts sur `bd34ded`.

- **Les incidents de processus sont déclarés**, pas dissimulés : le corps de la
  PR signale que le Planificateur et le Générateur ont committé sur des
  branches parasites `cursor/*` malgré l'interdiction. Ce point est déjà
  consigné comme Non-Goal différé (`CURSOR-3b47ffe`, points 1 et 7) ; le
  répéter serait du bruit au sens de la § « pas de rubber-stamping inverse ».

- **`AUDIT_STALE` compté comme non adjugé** est un choix défendable (un audit
  périmé n'est pas une décision) ; combiné à la dérive de SHA du § 4 il ne peut
  de toute façon pas produire de rouge permanent.

---

## 10. Risques par sévérité

| # | sévérité | constat | preuve |
|---|---|---|---|
| P0-1 | **P0** | Injection de commande shell via `${{ github.head_ref }}` dans un `run:` ; porte `actionlint` rouge sur la PR, verte sur `master` | `audit-guard.yml` job `audit-check` ; run [31701797271](https://github.com/PLiagre/ForgeHistory/actions/runs/31701797271) ; § 3 |
| P0-2 | **P0** | La porte `audit-check` devient verte au premier commit suivant l'audit, et ne voit pas les audits encore en PR `cursor/*` ; la PR #83 passe sa propre porte | `pr_audit_guard.py::_targets_pr` ; démonstration A/B § 4 ; `audit-check pass` sur PR #83 |
| P1-1 | **P1** | Le repli Codex ne peut aboutir : `codex` n'est pas installé et `auth.json` n'est pas amorcé dans `pipeline-challenge.yml` | `rg "npm install"` → une seule ligne, Claude uniquement ; comparaison à `pipeline-forge-run.yml:163-183` ; § 5 |
| P1-2 | **P1** | L'état du refus fournisseur reste sur une branche jamais fusionnée ; le `git add` de l'étape de publication est un no-op après `git checkout -` | bac à sable git § 6 ; `test_n5_…` lit le commit « pas l'arbre de travail » |
| P1-3 | **P1** | La preuve du volet B est un simulateur d'Actions du même auteur ; un des sept chemins (`codex_succeeds=True`) est irréalisable | `test_pipeline_challenge_paths.py::_step_outcome` ; § 7 |
| P2-1 | P2 | 22 fichiers / +4691 lignes, deux volets sans dépendance dans un même lot | diffstat ; § 8 |
| P2-2 | P2 | Le lecteur de frontmatter casse sur le format documenté par `architecture/README.md` | exécution § 8 ; `README.md:66-79` |
| P2-3 | P2 | `audit_ledger` importé par `sys.path.insert` au niveau module | `pr_audit_guard.py` lignes 40-42 |
| P3 | P3 | 34 tests verts, boucle à trois rôles réellement mordante, câblage événementiel juste, aucune régression sur les autres portes | § 9 |

---

## 11. Briefs atomiques proposés (3 au maximum — proposition, pas instruction)

Ces propositions n'autorisent rien : elles n'entrent en vigueur que si le
propriétaire les convertit en briefs (`architecture/README.md`, cycle de vie).

**B-1 — Refermer l'injection et remettre `actionlint` au vert (ferme P0-1).**
Faire passer `github.head_ref` par un bloc `env:` dans le job `audit-check`, à
l'image du job `cursor-scope` du même fichier. Preuve exigée : `actionlint`
vert sur le commit, et un test qui rougit si une expression `${{ github.* }}`
non fiable réapparaît dans un `run:` d'un workflow du dépôt.

**B-2 — Rendre l'appariement audit ↔ PR stable dans le temps (ferme P0-2).**
Apparier sur une clé qui ne bouge pas quand la PR reçoit un commit (numéro de
PR, ou branche de tête portée explicitement par le frontmatter de l'audit), et
lire l'inbox depuis `master` plutôt que depuis le merge-ref de la PR auditée.
Preuve exigée : un test qui rougit si le contrôle passe au vert après
l'ajout d'un commit à une PR dont un audit non adjugé est ouvert — c'est-à-dire
exactement la démonstration A/B du § 4, inversée en garde.

**B-3 — Trancher le repli fournisseur : l'exécuter ou le retirer (ferme P1-1,
P1-2).** Soit installer `@openai/codex` et amorcer `~/.codex/auth.json` dans
`pipeline-challenge.yml` comme le fait déjà `pipeline-forge-run.yml`, avec un
`codex --version` de fumée ; soit retirer l'étape de repli et assumer que le
seul livrable du 429 est l'état consigné. Dans les deux cas, corriger le
chemin de l'état pour qu'il atteigne `master` (une PR, ou l'ajout au commit de
revue avant tout `git checkout -`).

---

## 12. Points à porter au propriétaire (gouvernance — hors compétence d'un audit)

- **La rubrique d'évaluation a validé le motif d'injection.** SC2 exigeait la
  présence littérale de `${{ github.head_ref }}` dans le job ; l'Évaluateur l'a
  constatée et a conclu PASS. Le problème n'est pas l'Évaluateur : la rubrique
  demandait une forme dangereuse. La question ouverte est de savoir si les
  rubriques doivent, à l'avenir, être elles-mêmes passées au linter de sécurité
  quand elles prescrivent du YAML de CI.
- **Aucun rôle du harnais ne lit la CI du commit qu'il juge.** Le workflow
  `security` était rouge à chacun des trois pushes du Générateur ; ni le
  Générateur ni l'Évaluateur ne le mentionnent. C'est la même classe de
  problème que celle que le brief 014 traite pour Claude/429 : un signal
  existe, personne ne le consomme.

---

## Sources externes

| # | source | consulté le |
|---|---|---|
| S1 | Augment Code — *From Assisted to Autonomous: How Far Can the Engineering Loop Close?* (état de juillet 2026) — <https://www.augmentcode.com/guides/autonomous-engineering-loop> | 2026-08-13 |
| S2 | DEV Community — *Evidence Gates for AI Coding Agents in CI — Recoverable Merge over Mean Time to Green* — <https://dev.to/lo_an_e746e473b842ff53cf9/evidence-gates-for-ai-coding-agents-in-ci-recoverable-merge-over-mean-time-to-green-2a8h> | 2026-08-13 |
| S3 | zolty.systems — *The autonomy ladder in practice: letting agents commit, then merge* (2026-07-24) — <https://blog.zolty.systems/posts/2026-07-24-autonomy-ladder-in-practice/> | 2026-08-13 |
| S4 | niteagent — *Managing Rate Limits and Token Budgets in Production AI Agents* (2026-07-03) — <https://niteagent.com/blog/2026-07-03-agent-rate-limit-quota-management-guide/> | 2026-08-13 |
| S5 | TrueFoundry — *Rate Limiting AI Agents: Preventing LLM API Exhaustion with a 3-Layer Gateway* — <https://www.truefoundry.com/blog/rate-limiting-ai-agents-preventing-llm-api-exhaustion> | 2026-08-13 |
| S6 | GitHub Docs — *Script injections* (contextes non fiables ; `head_ref` nommément listé) — <https://docs.github.com/en/actions/concepts/security/script-injections> | 2026-08-13 |
| S7 | rhysd/actionlint — `expr_insecure.go`, `BuiltinUntrustedInputs` (`github.head_ref`) — <https://github.com/rhysd/actionlint/blob/main/expr_insecure.go> | 2026-08-13 |

Rattachement des sources aux constats :

- S6, S7 fondent **P0-1** : `head_ref` est une entrée non fiable, et
  l'interpolation directe dans un `run:` est le motif d'injection documenté.
- S2 et S3 fondent la lecture de **P0-2** et **P1-3** : une porte n'a de valeur
  que si elle produit une preuve rejouable au moment de la fusion, et les
  règles déterministes doivent tourner au runner, pas être simulées ailleurs.
  S3 insiste en particulier sur le fait qu'une porte non-négociable ne doit pas
  pouvoir être neutralisée par `continue-on-error`.
- S1 fonde le § 12 : la frontière de fusion reste le point d'approbation
  humain ; une porte silencieusement verte déplace cette frontière sans que
  personne l'ait décidé.
- S4, S5 fondent **P1-1** : la réponse standard à un 429 est une chaîne de
  repli *déclarée et testée* (circuit breaker → fournisseur secondaire →
  échec explicite). Une chaîne de repli dont le second maillon n'est pas
  installé n'est pas une chaîne de repli ; elle produit le même échec qu'avant,
  avec plus de code.

---

## Commandes rejouées (récapitulatif)

```bash
# état de la PR et de la CI
gh pr view 83 -R PLiagre/ForgeHistory --json …
gh pr checks 83 -R PLiagre/ForgeHistory
gh run list  -R PLiagre/ForgeHistory -w security -b master -L 5
gh run view 31701797271 -R PLiagre/ForgeHistory --log-failed

# arbre de travail au SHA audité
git worktree add /tmp/pr83 refs/remotes/origin/forge/014
git diff --stat refs/remotes/origin/master...refs/remotes/origin/forge/014

# tests du lot
.venv/bin/python -m pytest harness/tests/test_pr_audit_guard.py \
  harness/tests/test_vendor_refusal.py \
  harness/tests/test_pipeline_challenge_paths.py -q          # 34 passed

# porte, sur la PR #83 elle-même
.venv/bin/python harness/pipeline/pr_audit_guard.py check \
  --head-branch forge/014-pipeline-contre-audit-porte-e180 \
  --head-commit bd34dedbb713863d7f9bfa8f9341975aa01291d6     # exit 0

# dérive de SHA (inbox + ledger isolés)
.venv/bin/python harness/pipeline/pr_audit_guard.py check \
  --head-branch forge/ma-pr --head-commit abcdef01…          # exit 1
.venv/bin/python harness/pipeline/pr_audit_guard.py check \
  --head-branch forge/ma-pr --head-commit 99999999…          # exit 0

# installation Codex dans le workflow modifié
git show refs/remotes/origin/forge/014:.github/workflows/pipeline-challenge.yml | rg -n "npm install"

# perte de l'arbre de travail après `git checkout -`
git checkout -b forge-bot/vendor-refusal-X-123 && git add state.jsonl \
  && git commit -qm state && git checkout -q - && git status --porcelain
```
