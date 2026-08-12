---
audit_id: CURSOR-65c3ac1-dashboard-hermes-modele-auditeur
auditor: cursor-cloud
target_branch: master
target_commit: 65c3ac1c85c24cc61265c7f9ec4989cc67a0b4f9
created_at: 2026-08-12T11:55:00Z
audit_type: architecture-and-qa
status: PROPOSED
implementation_authorized: false
ci_changes_authorized: false
code_changes_authorized: false
---

# 1. Résumé exécutif

**Commit audité** : `65c3ac1c85c24cc61265c7f9ec4989cc67a0b4f9` — fusion de la
PR #27 (`forge/hermes-dashboard-modele-auditeur-977d`) sur `master`, le
2026-08-12 à 13:42:04 +0200. Parents : `9ee112d` (master avant fusion) et
`73022bd` (tête de branche).

**Fraîcheur** : **CURRENT**. Le commit audité est la tête de `master` et
d'`origin/master` au moment de l'audit (preuve § 5.1).

**Ce que le commit livre** : trois choses distinctes, en un seul lot.
(a) un **tableau de bord** pour le propriétaire — `hermes/dashboard.py`
(364 lignes) génère `hermes/DASHBOARD.md` depuis les sources de vérité du
dépôt, avec son workflow `hermes-dashboard.yml` et 4 tests ;
(b) une **résolution de modèle** pour l'auditeur Cursor — le workflow
n'invente plus un identifiant, il interroge `GET /v1/models` et choisit un
modèle Opus ;
(c) un **garde anti-boucle** — une poussée sur `master` qui ne touche que
les artefacts de la boucle d'audit ne relance plus un auditeur.

**Volumétrie** : +801 / −13 lignes sur 8 fichiers.

**CI** : **verte**. Les 7 exécutions déclenchées par ce commit se terminent
en `success`, dont `harness-ci`, `security`, `audit-guard`, `pipeline-audit`
et `hermes-dashboard` (sortie complète § 5.2).

## Les trois constats qui portent

1. **P0 — l'invocation qui dépense réellement de l'argent est la seule qui
   n'est pas branchée au plafond budgétaire.** `pipeline-challenge.yml` et
   `pipeline-forge-run.yml` appellent tous deux `ci_budget_guard.py precheck`
   *et* `record`. `pipeline-audit.yml` n'appelle ni l'un ni l'autre — et
   c'est pourtant le seul workflow d'agent qui tourne aujourd'hui pour de
   vrai, désormais sur `claude-opus-5`, le modèle le plus cher du catalogue.
   Le ledger `ci-budget-ledger.jsonl` est **littéralement vide** (1 octet,
   un saut de ligne), donc le tableau de bord annonce au propriétaire
   « 0.0 USD mesurés sur 0 invocation(s) » alors que des agents Opus 5 ont
   déjà tourné le jour même.

2. **P1 — le garde anti-boucle exempte d'audit tout `hermes/**`, y compris
   du code exécutable.** Le motif d'exclusion contient `hermes/` sans
   restriction. Or ce commit vient précisément de placer 364 lignes de
   Python sous `hermes/`. Une future modification de `hermes/dashboard.py`
   fusionnée seule ne sera jamais critiquée par Cursor — exactement ce que
   le garde ne visait pas.

3. **P1 — le tableau de bord se réécrit à chaque exécution même quand rien
   n'a changé.** Son en-tête porte un horodatage à la minute, donc le garde
   « rien à pousser » du workflow est inatteignable : à raison d'un `cron`
   toutes les 6 h, c'est un commit de bot poussé **directement sur
   `master`**, sans PR ni critique, quatre fois par jour au minimum.

## Deux forces du changement

1. **La résolution de modèle ne devine plus.** Le tour précédent avait été
   refusé `invalid_model` sur un identifiant inventé ; ce commit interroge
   l'API, journalise la liste réelle, honore une variable de dépôt si elle
   correspond, et retombe sur une préférence explicite. Le log CI prouve
   que ça marche de bout en bout : `Modèle retenu: claude-opus-5` (§ 5.3).

2. **Le tableau de bord refuse d'inventer.** Quand les données GitHub ou
   Cursor manquent, la vue écrit « Non disponible dans cette génération »
   au lieu de combler le trou — et c'est *testé*
   (`test_donnees_optionnelles_absentes_disent_non_disponible`). Une ligne
   corrompue du ledger est ignorée sans abattre la génération, également
   testé. C'est la bonne discipline pour une vue dérivée : elle reste une
   vue, jamais une base parallèle (principe n°1 du projet).

# 2. Diff du merge et état du dépôt

## 2.1 Provenance

| | |
|---|---|
| Merge commit | `65c3ac1c85c24cc61265c7f9ec4989cc67a0b4f9` |
| Parents | `9ee112d` (master), `73022bd` (branche) |
| PR | #27 — `forge/hermes-dashboard-modele-auditeur-977d` |
| Auteur | Pierre-Edouard Liagre |
| Date | Wed Aug 12 13:42:04 2026 +0200 |
| Titre | Hermes : tableau de bord lisible + auditeur Cursor en Opus 5 + anti-boucle d'audits |

## 2.2 Fichiers touchés (8)

```
 .github/workflows/hermes-dashboard.yml | 103 ++++++++++   (nouveau)
 .github/workflows/pipeline-audit.yml   |  95 +++++++--
 CLAUDE.md                              |   1 +
 HANDOFF.md                             |  40 ++++
 harness/tests/test_hermes_dashboard.py | 122 +++++++++++   (nouveau)
 hermes/DASHBOARD.md                    |  74 +++++++      (nouveau, généré)
 hermes/README.md                       |  15 ++
 hermes/dashboard.py                    | 364 +++++++++++++ (nouveau)
 8 files changed, 801 insertions(+), 13 deletions(-)
```

Deux commits fonctionnels : `04b98b5` (tableau de bord + anti-boucle +
consigne d'ouverture de PR) et `73022bd` (résolution du modèle via
`GET /v1/models`).

## 2.3 Lecture par les six lentilles (`architecture/review-guidelines.md`)

| Lentille | Verdict |
|---|---|
| 1. Intention avant diff | **Lisible.** Le message de merge et les commentaires en tête de chaque fichier disent le problème résolu ; le commentaire anti-boucle date même l'incident réel (« constaté en réel le 2026-08-12 »). |
| 2. Preuve d'exécution | **Tenue pour le tableau de bord** (4 tests sur dépôt-fixture jetable), **absente pour les deux autres apports** : ni le garde anti-boucle ni la résolution de modèle n'ont de test — le shell des workflows n'est couvert par rien. Voir P2 § 3.4. |
| 3. Portes mécaniques d'abord | **Vertes.** 309 tests passent, 16 skippés (Unity/Windows), et les 7 workflows du commit sont en `success` (§ 5.2, § 5.5). |
| 4. Cadrage adverse | **Respecté au niveau des rôles** — l'auteur du commit et l'auditeur sont distincts. Mais voir P1 § 3.2 : le commit ouvre lui-même un chemin (`hermes/**`) qui échappe à ce cadrage. |
| 5. Taille et découpage | **Trois sujets indépendants dans un lot** (tableau de bord / modèle / anti-boucle). 801 lignes, au-delà du seuil ~400 lignes que le guide cite comme limite de la relecture honnête. Trois lots séparés auraient été relisibles ; signalé en P3 § 3.6, sans plus, parce que la CI est verte et le lot est déjà fusionné. |
| 6. Pièges du code généré par IA | **Un piège trouvé** : un filtre inerte (`grep -i thinking` sur une liste qui ne contient aucun « thinking ») — du code qui a l'air de faire respecter une exigence et qui, en réalité, ne peut jamais matcher. Détail P2 § 3.3. |

# 3. Risques par sévérité (P0–P3)

## 3.1 P0 — `pipeline-audit.yml` dépense sans plafond ni compteur, et la vue du propriétaire affiche zéro

**Constat.** Le plafond mensuel construit par le brief 009 (lot 009b,
`harness/pipeline/ci_budget_guard.py`) est câblé dans deux workflows sur
trois. Celui qui manque est celui qui tourne.

**Preuve — qui appelle le garde budgétaire :**

```
$ rg -n "ci_budget_guard" .github/workflows/
.github/workflows/pipeline-forge-run.yml:121:          python harness/pipeline/ci_budget_guard.py precheck
.github/workflows/pipeline-forge-run.yml:202:          python harness/pipeline/ci_budget_guard.py record \
.github/workflows/pipeline-challenge.yml:107:          python harness/pipeline/ci_budget_guard.py precheck
.github/workflows/pipeline-challenge.yml:157:          python harness/pipeline/ci_budget_guard.py record \

$ rg -n "TARGET_COMMIT|ci-budget-ledger|budget" .github/workflows/pipeline-audit.yml
122:          TARGET_COMMIT: ${{ github.event.inputs.target_commit || github.sha }}
172:          Audite le commit ${TARGET_COMMIT} fraîchement fusionné sur master: ...
178:              --arg ref "$TARGET_COMMIT" \
```

Aucune occurrence de `budget` dans `pipeline-audit.yml` : ni `precheck`
avant l'appel, ni `record` après.

**Preuve — le ledger est vide :**

```
$ wc -c harness/pipeline/ci-budget-ledger.jsonl
1 harness/pipeline/ci-budget-ledger.jsonl
$ od -c harness/pipeline/ci-budget-ledger.jsonl | head -3
0000000  \n
0000001
```

**Preuve — ce que le propriétaire lit dans sa vue :**

```
$ py -c "import sys; sys.path.insert(0,'hermes'); import dashboard; \
    print([l for l in dashboard.generer(Path('.')).splitlines() if 'Dépense CI' in l][0])"
- **Dépense CI ce mois-ci** : 0.0 USD mesurés sur 0 invocation(s), plafond 200 USD.
  En authentification par abonnement, ce chiffre est un équivalent estimé, pas une facture.
```

**Preuve — que des invocations Opus 5 ont bien eu lieu ce jour-là :** le log
du run `31593029671` (`pipeline-audit`, déclenché par ce commit même) se
termine par `Modèle retenu: claude-opus-5` puis poste l'agent (§ 5.3).

**Pourquoi ce n'est pas un doublon du brief 009.** Le brief 009 avait
*explicitement* mis ce câblage hors périmètre — ligne 375-378 :

> Lot 009c wires **only** `claude-challenger`/`pipeline-challenge.yml`.
> `pipeline-audit.yml`'s and `pipeline-forge-run.yml`'s own `TODO(operator`
> invocation bodies are **not** touched by this brief.

Le report était légitime tant que `pipeline-audit.yml` n'était qu'un stub.
Depuis `cdc683f` il invoque réellement, depuis `65c3ac1` il invoque sur le
modèle le plus cher — le report n'a jamais été refermé. Le brief 009
énonçait lui-même la raison d'être du plafond (ligne 210-212) :

> every merge touching `architecture/inbox/`, it spends real money with
> nobody approving each spend individually — the harness needs a genuine,
> pre-invocation-capable ceiling on that recurring cost.

C'est mot pour mot le trou constaté.

**Impact.** Trois effets qui se cumulent : (1) le kill-switch budgétaire ne
peut pas se déclencher sur la dépense réelle, puisqu'elle n'est pas
comptée ; (2) le plafond de 200 USD/mois est inopérant pour cette voie ;
(3) le chiffre affiché au propriétaire est faux dans le sens le plus
dangereux — il rassure. Une boucle d'audit emballée (chaque fusion d'audit
relançant un auditeur, l'incident que le garde anti-boucle vient justement
de corriger) dépenserait en Opus 5 sans qu'aucun compteur ne bouge.

**Comparaison à l'état de l'art** (sources S1, S2, S6 § 4). La distinction
établie en 2026 est nette : la *visibilité* de coût n'est pas le *contrôle*
de coût, et l'enforcement doit être **pré-appel** — un appel bloqué avant
émission coûte zéro, un appel constaté après coup a déjà été payé. Le dépôt
possède déjà le bon composant (`ci_budget_guard.py precheck` est exactement
la forme recommandée) ; il ne l'a simplement pas branché sur la bonne voie.

**Piste (non prescriptive).** Appeler `precheck` avant l'étape d'invocation
Cursor et `record` après, comme le font déjà les deux autres workflows.

## 3.2 P1 — le garde anti-boucle exempte d'audit tout `hermes/**`, code compris

**Constat.** Le motif de classement « poussée documentaire » exclut le
préfixe `hermes/` sans distinction entre document et code.

**Preuve — le motif** (`.github/workflows/pipeline-audit.yml`, étape
« Documentary push? (loop artefacts only -- no re-audit) ») :

```bash
hors_boucle="$(printf '%s\n' "$changed" \
  | grep -vE '^(architecture/(inbox|reviews|decisions|archive)/|architecture/audit-ledger\.jsonl$|hermes/)' || true)"
if [ -z "$hors_boucle" ]; then skip=true; ...
```

**Preuve — le motif rejoué sur des chemins types :**

```
$ for cas in hermes/dashboard.py hermes/DASHBOARD.md architecture/inbox/CURSOR-x.md \
             sim/engine.py architecture/agents/cursor-auditor.md; do ... done
SKIP (pas d'audit)   <- hermes/dashboard.py
SKIP (pas d'audit)   <- hermes/DASHBOARD.md
SKIP (pas d'audit)   <- architecture/inbox/CURSOR-x.md
AUDIT               <- sim/engine.py
AUDIT               <- architecture/agents/cursor-auditor.md
```

**Impact.** `hermes/dashboard.py` est du Python exécutable (364 lignes),
introduit par ce commit même, exécuté en CI avec `contents: write` et le
pouvoir de pousser sur `master`. C'est précisément le genre de fichier
qu'ADR-0010 veut voir critiqué (« Cursor est le maillon critique de chaque
PR »). Le garde, écrit pour empêcher une boucle documentaire, a créé au
passage une zone d'ombre pour du code privilégié. À noter que le garde vise
juste sur les autres chemins : `architecture/agents/**` reste audité, ce qui
est le bon choix.

**Nuance honnête.** Le déclencheur `pull_request` de `pipeline-audit.yml`
n'est pas concerné : l'étape porte `if: github.event_name == 'push'`, donc
une PR touchant `hermes/**` est bien critiquée. Le trou ne s'ouvre que sur
une poussée directe vers `master` — ce qui n'est pas théorique, puisque
`hermes-dashboard.yml` pousse justement directement sur `master` (§ 3.3).

**Piste (non prescriptive).** Restreindre l'exemption aux artefacts
documentaires produits par la boucle (`hermes/DASHBOARD.md`,
`hermes/reports/**`) plutôt qu'au préfixe entier.

## 3.3 P1 — le tableau de bord produit un commit sur `master` à chaque exécution, même sans changement de fond

**Constat.** Le workflow prévoit un garde « rien à pousser », mais
l'en-tête généré contient un horodatage à la minute : deux générations
successives diffèrent toujours, donc le garde ne peut jamais se déclencher.

**Preuve — le garde** (`.github/workflows/hermes-dashboard.yml`, l. 94-97) :

```bash
if git diff --quiet -- hermes/DASHBOARD.md; then
  echo "Tableau de bord inchangé -- rien à pousser."
  exit 0
fi
```

**Preuve — la ligne qui empêche le garde d'être atteint**
(`hermes/dashboard.py`, l. 208) :

```python
out.append(f"> Générée le {now.strftime('%Y-%m-%d %H:%M UTC')}.")
```

**Preuve — deux générations à 6 h d'écart (l'intervalle du `cron`) :**

```
$ py -c "... a = generer(now=12h00); b = generer(now=18h00); diff(a,b)"
identiques ? False
--- run 12h00
+++ run 18h00
@@ -5,7 +5,7 @@
-> Générée le 2026-08-12 12:00 UTC.
+> Générée le 2026-08-12 18:00 UTC.
lignes differentes: 1
```

Une seule ligne diffère — et elle suffit à déclencher le commit et la
poussée.

**Impact.** Le `cron` est `17 */6 * * *`, donc au minimum quatre commits de
bot par jour poussés **directement sur `master`**, sans PR, sans critique,
sans revue — plus un à chaque poussée sur `master`. Effets concrets :
(a) l'historique de `master` se remplit de commits vides de sens, ce qui
gêne `git blame`, `git log` et toute lecture de « qu'est-ce qui a changé
depuis hier » ; (b) la tête de `master` est souvent un commit de bot, si
bien qu'un audit lancé « sur le dernier merge de master » peut viser un
commit de tableau de bord ; (c) la poussée peut échouer en course avec une
autre (`git pull --rebase` puis `git push`, sans nouvelle tentative), ce qui
teintera la CI en rouge par intermittence, pour rien.

**Comparaison à l'état de l'art** (sources S3, S4 § 4). Le consensus 2026
sur les commits de bot est explicite : le bot ouvre une PR, il ne pousse pas
sur la branche protégée ; les listes de « ce qu'un agent ne doit jamais
faire » citent en premier « direct commits to protected branches ». Le
dépôt fait ici l'inverse — pour un fichier certes inoffensif, mais avec
`contents: write` sur `master` comme précédent. Deux atténuations réelles à
mettre au crédit du commit : les actions sont épinglées par SHA complet, et
les poussées faites avec le `GITHUB_TOKEN` par défaut ne redéclenchent pas
de workflow, ce qui borne la boucle indépendamment du `paths-ignore`.

**Piste (non prescriptive).** Soit retirer l'horodatage de la sortie (la
date du commit le porte déjà), soit ne pousser que si le contenu **hors
en-tête** a changé.

## 3.4 P2 — le modèle retenu n'est ni déterministe, ni conservé nulle part

**Constat.** Trois faiblesses dans la sélection, toutes visibles dans le
même bloc de `pipeline-audit.yml`.

**(a) Le filtre « thinking » est inerte.** Le code cherche d'abord un modèle
Opus *thinking* :

```bash
model="$(printf '%s\n' "$ids" | grep -i opus | grep -i thinking | head -1 || true)"
```

Or `$ids` n'agrège que des `id` et des `aliases`, et la liste réelle
renvoyée par l'API ne contient aucun « thinking » (§ 5.3) : la variante de
raisonnement se demande via `model.params`, que le workflow n'envoie jamais.
Ce premier filtre ne peut donc jamais matcher — c'est du code qui *a l'air*
d'appliquer l'exigence du propriétaire (« au moins Opus, pour la critique »)
sans rien appliquer. C'est le piège n°6 du guide de critique, forme
« correction hallucinée ».

**(b) Le choix dépend de l'ordre de la réponse API.** Le repli est
`grep -i opus | head -1`, appliqué à une liste où identifiants et alias sont
aplatis ensemble. La liste réelle du run (§ 5.3) contient quatre fois
l'alias nu `opus`, attaché successivement à `claude-opus-5`,
`claude-opus-4-8`, `claude-opus-4-6` et `claude-opus-4-5`. Aujourd'hui
`claude-opus-5` arrive en premier et le résultat est bon ; le jour où Cursor
réordonne sa réponse ou publie un `claude-opus-6`, le modèle change sans que
personne n'ait rien décidé — et si c'est un alias nu qui est capté, on ne
saura même plus quel modèle concret a tourné.

**(c) Rien ne conserve le modèle utilisé.** L'identifiant retenu n'apparaît
que dans le log GitHub Actions (rétention limitée). Il n'est ni dans le
frontmatter de l'audit produit, ni dans `architecture/audit-ledger.jsonl`.
Dans six mois, face à un audit faible, on ne pourra pas répondre à « quel
modèle l'a écrit ? ».

**Preuve — le run réel** (`31593029671`, extraits § 5.3) :

```
Modèles disponibles (GET /v1/models):
grok-4.5
claude-opus-5
opus-latest
opus
opus-5
claude-opus-4-8
opus-latest
opus
...
Modèle retenu: claude-opus-5
```

Aucune entrée « thinking » ; l'alias `opus` apparaît quatre fois.

**Comparaison à l'état de l'art** (sources S5, S6, S7 § 4). Le standard 2026
pour un pipeline agentique auditable est l'épinglage explicite de version de
modèle plus un enregistrement de provenance par exécution (identifiant de
modèle, empreinte du prompt, paramètres) : « model version — customers who
don't pin versions get surprised when their pipeline behavior shifts after a
routine model update ». Le dépôt a déjà la bonne infrastructure de traçage
(`audit-ledger.jsonl`, frontmatter typé, garde de schéma) ; il manque juste
le champ.

**Piste (non prescriptive).** Consigner le modèle effectivement retenu dans
le frontmatter de l'audit ou au ledger, et préférer une sélection sur
identifiant exact plutôt que sur `head -1` d'un `grep`.

## 3.5 P2 — les deux apports non testés sont ceux écrits en shell

**Constat.** Le tableau de bord arrive avec 4 tests ; le garde anti-boucle
et la résolution de modèle arrivent avec zéro. Or ce sont eux qui portent
les décisions à conséquence (auditer ou non ; quel modèle payer).

**Preuve.**

```
$ py -m pytest harness/tests/test_hermes_dashboard.py -q
....                                                                     [100%]
4 passed in 0.02s

$ rg -l "Documentary push|CURSOR_AUDITOR_MODEL|hors_boucle" harness/tests/
(aucun résultat)
```

**Impact.** Le motif d'exclusion du § 3.2 et le filtre inerte du § 3.4(a)
sont exactement le type de défaut qu'un test de motif, à trois lignes,
aurait attrapé avant fusion. Le dépôt a déjà le précédent :
`test_ci_budget_guard.py` teste la logique de budget extraite du shell vers
Python. La leçon (lentille n°2 du guide : « preuve d'exécution, pas
d'affirmation ») est appliquée au Python et pas au YAML.

## 3.6 P3 — trois sujets indépendants fusionnés en un lot

**Constat.** Tableau de bord, résolution de modèle et garde anti-boucle
n'ont aucune dépendance mutuelle et auraient pu être trois PR. 801 lignes,
au-delà du seuil (~400) que `review-guidelines.md` cite comme limite de la
relecture honnête (lentille n°5).

**Preuve.** `git show --stat 65c3ac1` (§ 2.2) — le seul recouvrement entre
les trois sujets est le commentaire anti-boucle de `hermes-dashboard.yml`
qui référence `pipeline-audit.yml`, soit une ligne de documentation.

**Impact.** Faible et déjà consommé : la CI est verte et le lot est fusionné.
Signalé pour la discipline `NEEDS_SPLIT` que le harnais applique déjà aux
briefs et qui gagnerait à s'appliquer aussi aux PR.

## 3.7 Récapitulatif — sévérité, job CI concerné

| # | Constat | Sévérité | Job / fichier concerné | Visible en CI aujourd'hui ? |
|---|---|---|---|---|
| 3.1 | Invocation Cursor hors plafond budgétaire, ledger vide, vue à 0 USD | **P0** | `pipeline-audit.yml` (étape *Invoke cursor-auditor*) | Non — aucun job ne l'évalue |
| 3.2 | `hermes/**` exempté d'audit, code compris | **P1** | `pipeline-audit.yml` (étape *Documentary push?*) | Non — le garde réussit toujours |
| 3.3 | Commit de bot sur `master` à chaque exécution | **P1** | `hermes-dashboard.yml` (étapes *Commit and push*) | Partiellement — visible comme succès répété |
| 3.4 | Modèle non déterministe, filtre inerte, non tracé | **P2** | `pipeline-audit.yml` (étape *Invoke cursor-auditor*) | Non — `::warning::` non bloquants |
| 3.5 | Shell des workflows non testé | **P2** | `harness-ci` | Non — rien à exécuter |
| 3.6 | Lot de trois sujets | **P3** | — | Non |

**État CI du commit audité** : verte, 7/7 en `success` (§ 5.2). Aucun des
constats ci-dessus ne rougit un job existant : tous portent sur des
comportements qu'aucune porte mécanique n'évalue aujourd'hui.

# 4. Sources externes

Recherches effectuées le **2026-08-12** sur les trois axes du contrat
`cursor-auditor` (« autonomous AI dev pipeline », « agent orchestration
CI », « token budget LLM agents »), plus un axe rendu nécessaire par le
constat § 3.3 (commits de bot en CI).

| # | Source | Date de publication | Consultée le |
|---|---|---|---|
| S1 | Waxell — *The $400M AI FinOps Gap: Why Cost Visibility Isn't the Same as Cost Control* — <https://waxell.ai/blog/ai-agent-finops-cost-enforcement> | 2026 | 2026-08-12 |
| S2 | Oracle AI & Data Science — *Runtime Budget Guardrails for Agentic AI* — <https://blogs.oracle.com/ai-and-datascience/runtime-budget-guardrails-agentic-ai> | 2026 | 2026-08-12 |
| S3 | vu1nz — *AI Agents GitHub Actions Security: How to Keep Autonomous CI/CD Workflows from Becoming a Supply Chain Liability* — <https://vu1nz.com/blog/ai-agents-github-actions-security> | 2026 | 2026-08-12 |
| S4 | CVE OptiBot — *GitHub Actions Security Hardening in 2026: Permissions, SLSA, Scoped Secrets* — <https://cve.optibot.re/blog/github-actions-security-hardening-2026> | 2026 | 2026-08-12 |
| S5 | Cogneris — *Audit Trails for Non-Deterministic AI Outputs* — <https://cogneris.ai/blog-audit-trail-non-deterministic.html> | 2026 | 2026-08-12 |
| S6 | Datastore.cloud — *Audit Trails for Autonomous Agents: Glass-Box Controls* — <https://datastore.cloud/audit-trails-for-autonomous-agents-engineering-controls-that> | 2026 | 2026-08-12 |
| S7 | GitHub — `shanemmattner/bernstein`, *Audit-grade multi-agent orchestration* (chaîne HMAC, lignée par artefact : producteur + prompt SHA + modèle + coût) — <https://github.com/shanemmattner/bernstein> | 2026 | 2026-08-12 |
| S8 | Cursor Docs — *Cloud Agent API — endpoints* (forme de réponse `GET /v1/models` : `items[].id`, `aliases`, `parameters`, `variants`) — <https://cursor.com/docs/cloud-agent/api/endpoints> | 2026 | 2026-08-12 |

## 4.1 Ce que ces sources disent, appliqué à ce dépôt

**Sur le coût (S1, S2).** La distinction structurante de 2026 est
*visibilité* ≠ *contrôle* : un tableau de bord qui récapitule la dépense
n'est pas un plafond, et l'enforcement doit être pré-appel (un appel bloqué
avant émission coûte zéro). S2 liste le minimum viable — plafond dur de
jetons par exécution, limites de boucle/relance, réservation de budget
avant une action coûteuse. Le dépôt possède déjà `precheck`/`record` et un
plafond mensuel ; le défaut § 3.1 n'est pas un manque de conception, c'est
un branchement manquant sur la seule voie active.

**Sur les commits de bot (S3, S4).** S3 place « direct commits to protected
branches » en tête de sa liste de ce qu'un agent CI ne doit pas faire, et
recommande de générer un correctif puis d'ouvrir une PR plutôt que de muter
la branche. S4 va dans le même sens côté protection de branche. Le dépôt
fait l'inverse en § 3.3 — sur un fichier inoffensif, mais le précédent
`contents: write` sur `master` est posé. À son crédit : SHA-pinning des
actions, et permissions déclarées explicitement plutôt qu'héritées.

**Sur la traçabilité de modèle (S5, S6, S7).** Le motif convergent est le
« reçu de décision » : par exécution, on conserve l'identifiant de modèle
épinglé, l'empreinte du prompt, les paramètres, l'horodatage. S7 en donne
une implémentation exigeante (lignée par artefact liant producteur, prompt
SHA, modèle et coût). Le dépôt a déjà le support (`audit-ledger.jsonl`,
frontmatter validé par `harness/audit_schema.py`) ; § 3.4(c) est un champ
manquant, pas une architecture à refaire.

**Sur la forme de l'API (S8).** La documentation confirme que
`GET /v1/models` renvoie `{items: [{id, displayName, aliases, parameters,
variants}]}` — donc le chemin `jq '.items[]'` du workflow est correct (ce
n'est pas un défaut), et confirme aussi que la variante « thinking » se
demande via `model.params`, jamais dans l'`id` : c'est la preuve
documentaire du filtre inerte § 3.4(a).

## 4.2 Comparaison repo vs état de l'art (volet `cursor-qa-scout`)

Axe retenu : **plafonds de coût** (l'un des trois axes prévus par le
contrat `cursor-qa-scout`, avec les merge queues GitHub Actions et les
boucles agentiques).

| Pratique 2026 (S1, S2) | État du dépôt |
|---|---|
| Enforcement **pré-appel**, jamais post-hoc | Présent — `ci_budget_guard.py precheck` est exactement cette forme |
| Budgets hiérarchiques (clé / équipe / session) | Partiel — un plafond mensuel global, pas de plafond par invocation ni par session |
| Appliqué sur **toutes** les voies de dépense | **Manquant** — 2 workflows sur 3 ; celui qui manque est le seul actif (§ 3.1) |
| Coupe-circuit sur boucle / relance | Présent sous une autre forme — label `pipeline/pause`, `mode: manual`, et depuis ce commit le garde anti-boucle |
| Dépense enregistrée en journal append-only | Présent en conception (`ci-budget-ledger.jsonl`), **vide en pratique** |

Lecture : le dépôt n'a pas un problème de conception du plafond — il a un
problème de **couverture** du plafond. C'est une entrée pour
`claude-challenger`, pas une instruction.

## 4.3 Vérification de non-doublon avec les briefs ouverts

Briefs examinés (`harness/queue/briefs/*/brief.md`, titre lu dans chaque
fichier) :

| Brief | Titre | Recouvre un constat de cet audit ? |
|---|---|---|
| 001 | ADR-0003 — the single spatial primary key | Non |
| 002 | Geo pipeline, G2 littoral 1400 | Non |
| 003 | Port VictoriaProject's Unity game | Non |
| 004 | Bounded visual polish | Non |
| 005 | Refonte visuelle carte | Non |
| 006 | Pipeline multi-agents full-auto | Non — pose les contrats de rôles, pas le câblage budgétaire |
| 007 | Geo pipeline, G3 cells + G4 adjacency | Non |
| 008 (contexte) | Right-sizing du contexte agent pour Opus 5 | Non — porte sur la taille du contexte, pas sur le choix ni la traçabilité du modèle |
| 008 (gaps) | Full-auto pipeline reliability gaps | Non — issu de l'audit CURSOR-5633ee7, antérieur au tableau de bord |
| 009 | Wire claude-challenger, split `full_auto`, plafond de dépense CI récurrente | **Adjacent, pas doublon** — il a *construit* le plafond et a mis `pipeline-audit.yml` hors périmètre par un Non-Goal explicite (l. 375-378). Le constat § 3.1 porte sur ce report jamais refermé, sur un workflow qui, depuis, invoque réellement et en Opus 5. |
| 010 | Couche contrat de la répartition des rôles | Non |

**Déclaration** : **aucun doublon avec un brief ouvert.** Le seul
recouvrement est avec le brief 009 (délivré) et il est de nature
complémentaire — un périmètre explicitement exclu à l'époque, devenu
critique depuis que la voie concernée dépense pour de vrai.

# 5. Commandes rejouées

## 5.1 Fraîcheur du commit

```bash
$ git rev-parse HEAD
65c3ac1c85c24cc61265c7f9ec4989cc67a0b4f9

$ git log --oneline -3
65c3ac1 Merge pull request #27 from PLiagre/forge/hermes-dashboard-modele-auditeur-977d
9ee112d pipeline-orchestrate: review_recorded
dbd315c Merge pull request #26 from PLiagre/forge-bot/review-CURSOR-cdc683f-...

$ git branch -a --contains 65c3ac1
* cursor/audit-commit-master-ebee
  remotes/origin/HEAD -> origin/master
  remotes/origin/master
```

Le commit audité est bien la tête d'`origin/master` : **CURRENT**.

## 5.2 État de la CI du commit audité

```bash
$ gh run list --commit 65c3ac1c85c24cc61265c7f9ec4989cc67a0b4f9 \
    --json name,event,status,conclusion --limit 30
```

| workflow | déclencheur | statut | conclusion |
|---|---|---|---|
| `hermes-observer` | `workflow_run` | completed | **success** |
| `hermes-observer` | `workflow_run` | completed | **success** |
| `security` | `push` | completed | **success** |
| `pipeline-audit` | `push` | completed | **success** |
| `hermes-dashboard` | `push` | completed | **success** |
| `audit-guard` | `push` | completed | **success** |
| `harness-ci` | `push` | completed | **success** |

**CI verte, 7/7.** Aucun job rouge, aucun job annulé.

## 5.3 Résolution du modèle — log réel du run `31593029671`

```bash
$ gh run view 31593029671 --log | grep -E "Modèles disponibles|Modèle retenu|opus|grok"
Modèles disponibles (GET /v1/models):
grok-4.5
claude-opus-5
opus-latest
opus
opus-5
claude-opus-4-8
opus-latest
opus
opus-4.8
opus-4-8
claude-opus-4-7
opus-4.7
opus-4-7
claude-opus-4-6
opus
opus-4.6
opus-4-6
claude-opus-4-5
opus
opus-4.5
opus-4-5
Modèle retenu: claude-opus-5
```

Trois lectures : (1) le mécanisme fonctionne, l'exigence « au moins Opus »
est satisfaite aujourd'hui ; (2) aucune entrée ne contient « thinking »,
donc le premier filtre est inerte (§ 3.4a) ; (3) l'alias nu `opus`
apparaît quatre fois, rattaché à quatre modèles différents (§ 3.4b).

## 5.4 Filtre anti-boucle rejoué

```bash
$ filtre() { printf '%s\n' "$1" | grep -vE '^(architecture/(inbox|reviews|decisions|archive)/|architecture/audit-ledger\.jsonl$|hermes/)' || true; }
$ for cas in hermes/dashboard.py hermes/DASHBOARD.md architecture/inbox/CURSOR-x.md sim/engine.py architecture/agents/cursor-auditor.md; do ...; done
SKIP (pas d'audit)   <- hermes/dashboard.py
SKIP (pas d'audit)   <- hermes/DASHBOARD.md
SKIP (pas d'audit)   <- architecture/inbox/CURSOR-x.md
AUDIT               <- sim/engine.py
AUDIT               <- architecture/agents/cursor-auditor.md
```

## 5.5 Suite de tests du harnais

```bash
$ .venv/bin/python -m pytest harness/tests/test_hermes_dashboard.py -q
....                                                                     [100%]
4 passed in 0.02s

$ .venv/bin/python -m pytest harness/tests/ -q
........................................................................ [ 22%]
........................................................................ [ 44%]
........................................................................ [ 66%]
......................ssssssssssssssss.................................. [ 88%]
.....................................                                    [100%]
309 passed, 16 skipped in 16.94s
```

Les 16 `skip` sont les cas `test_run_unity.py`, qui exigent Unity et
PowerShell — comportement attendu sur un runner Linux (`AGENTS.md`), pas un
échec.

## 5.6 Couverture budgétaire des workflows

```bash
$ rg -n "ci_budget_guard" .github/workflows/
.github/workflows/pipeline-forge-run.yml:121:          python harness/pipeline/ci_budget_guard.py precheck
.github/workflows/pipeline-forge-run.yml:202:          python harness/pipeline/ci_budget_guard.py record \
.github/workflows/pipeline-challenge.yml:107:          python harness/pipeline/ci_budget_guard.py precheck
.github/workflows/pipeline-challenge.yml:157:          python harness/pipeline/ci_budget_guard.py record \

$ rg -c "budget" .github/workflows/pipeline-audit.yml
(aucune occurrence)

$ wc -c harness/pipeline/ci-budget-ledger.jsonl
1 harness/pipeline/ci-budget-ledger.jsonl
```

## 5.7 Churn du tableau de bord

```bash
$ py -c "generer(now=2026-08-12T12:00) vs generer(now=2026-08-12T18:00)"
identiques ? False
-> Générée le 2026-08-12 12:00 UTC.
+> Générée le 2026-08-12 18:00 UTC.
lignes differentes: 1
```

# 6. Briefs proposés (≤ 3)

Trois propositions, atomiques, indépendantes l'une de l'autre. Ce sont des
**entrées** pour la boucle (`claude-challenger` puis le propriétaire) ;
aucune n'est une instruction, et aucune n'autorise quoi que ce soit.

## Brief proposé 1 — brancher `pipeline-audit.yml` sur le plafond budgétaire existant

**Ce qui ne va pas** : constat § 3.1 (P0). La seule voie d'invocation
active aujourd'hui dépense en Opus 5 sans passer par le garde que le brief
009 a construit, et le ledger vide fait afficher « 0 USD » au propriétaire.

**Ce que refermerait le brief** : `pipeline-audit.yml` appellerait
`ci_budget_guard.py precheck` avant l'invocation et `record` après, comme
le font déjà `pipeline-challenge.yml` et `pipeline-forge-run.yml` ; le
plafond mensuel couvrirait alors les trois voies au lieu de deux, et le
chiffre du tableau de bord deviendrait vrai.

**Preuve attendue en fin de brief** : un test montrant qu'une invocation
refusée par `precheck` n'atteint pas l'appel API, et une ligne réelle
apparue dans `ci-budget-ledger.jsonl` après une exécution.

**Périmètre pressenti** : `.github/workflows/pipeline-audit.yml`,
`harness/tests/`. Aucun changement au module de budget lui-même (il existe
déjà et est testé).

**Non-doublon** : le brief 009 avait explicitement exclu ce fichier de son
périmètre (l. 375-378) ; cette proposition referme le report, elle ne le
refait pas.

## Brief proposé 2 — restreindre l'exemption anti-boucle aux vrais artefacts documentaires

**Ce qui ne va pas** : constat § 3.2 (P1). Le préfixe `hermes/` exempte
d'audit du code exécutable et privilégié (`hermes/dashboard.py`, qui tourne
en CI avec `contents: write` sur `master`).

**Ce que refermerait le brief** : le motif ne viserait plus que les
artefacts produits par la boucle (par exemple `hermes/DASHBOARD.md` et
`hermes/reports/**`), en laissant tout code sous `hermes/` soumis à la
critique voulue par ADR-0010.

**Preuve attendue en fin de brief** : la logique de classement extraite du
YAML vers un module testable (le dépôt a déjà ce précédent avec
`ci_budget_guard.py`), avec un test rouge-puis-vert prouvant que
`hermes/dashboard.py` déclenche un audit et que `hermes/DASHBOARD.md` n'en
déclenche pas. Cela refermerait aussi une partie du constat § 3.5.

**Périmètre pressenti** : `.github/workflows/pipeline-audit.yml`, un module
sous `harness/pipeline/`, `harness/tests/`.

## Brief proposé 3 — rendre le modèle de l'auditeur déterministe et traçable

**Ce qui ne va pas** : constat § 3.4 (P2), trois volets — filtre « thinking »
qui ne peut jamais matcher, sélection dépendante de l'ordre de la réponse
API avec alias ambigus, et aucune trace durable du modèle utilisé.

**Ce que refermerait le brief** : une sélection sur identifiant exact avec
ordre de préférence explicite (plutôt qu'un `head -1` de `grep`), le
paramètre de raisonnement demandé via `model.params` si le propriétaire le
veut, et le modèle effectivement retenu consigné là où il survivra à la
rétention des logs — frontmatter de l'audit ou `audit-ledger.jsonl`.

**Preuve attendue en fin de brief** : un test sur une réponse API figée
montrant que l'ordre de la liste ne change pas le modèle choisi, et un
audit produit portant le modèle en clair.

**Périmètre pressenti** : `.github/workflows/pipeline-audit.yml`,
`architecture/README.md` (schéma du frontmatter), `harness/audit_schema.py`,
`harness/tests/`.

**Alignement externe** : S5, S6, S7 (§ 4) — l'épinglage de modèle et le
« reçu de décision » par exécution sont le standard 2026 pour un pipeline
agentique auditable.

**Constats laissés sans brief, volontairement** : § 3.3 (churn du tableau de
bord) et § 3.6 (lot de trois sujets). Le premier est une décision de
conception qui appartient au propriétaire — accepter le bruit sur `master`
ou retirer l'horodatage — et se règle en une ligne ; le second est déjà
consommé et n'appelle qu'une discipline de découpage, pas un travail. Le
contrat plafonne à trois briefs et les trois retenus sont ceux qui portent
un risque non couvert par une porte mécanique.

# 7. Conclusion

Le commit `65c3ac1` est solide sur ce qu'il livre : le tableau de bord est
une vraie vue dérivée (jamais une base parallèle), il refuse d'inventer
quand une source manque, et c'est testé ; la résolution de modèle corrige
pour de bon l'échec `invalid_model` du tour précédent, avec la preuve d'un
run réel ; le garde anti-boucle répond à un incident daté et constaté. La
CI est verte, 7 jobs sur 7.

Le risque n'est pas dans ce qui a été écrit, il est dans **ce que ces trois
apports laissent ouvert**. Le commit fait passer l'auditeur sur le modèle le
plus cher du catalogue, et c'est justement le seul workflow d'agent qui ne
compte pas ce qu'il dépense — pendant que la nouvelle vue affiche « 0 USD »
au propriétaire. Le garde anti-boucle, en excluant `hermes/` en bloc, ouvre
une zone d'ombre là même où le commit vient de déposer du code privilégié.
Et le tableau de bord, conçu pour être régénéré sans bruit, se réécrit à
chaque exécution à cause d'un horodatage.

Ces trois choses ont un point commun : ce sont des effets de bord de bonnes
intentions, invisibles pour toutes les portes mécaniques existantes. C'est
exactement le domaine où une critique indépendante a de la valeur — et c'est
tout ce que cet audit prétend être : une entrée pour `claude-challenger` et
pour le propriétaire, jamais une autorisation d'exécuter quoi que ce soit.
