# Verdict — Brief `014` : Le contre-audit comme porte observable et le refus fournisseur comme état explicite

**Authored**: 2026-08-13T11:52:00Z
**Author**: forge-evaluateur

---

## Note de transparence

Le rôle déclaré en en-tête (`forge-evaluateur`) est le rôle natif du harnais,
conformément à la convention du dépôt appliquée aux lots `011`, `012` et `013`.
L'acteur réel est un sous-agent Cursor, orchestré par un agent Cursor Cloud qui
remplace le CTO pour cette session. Cette session est **distincte** de celle du
Planificateur (qui a écrit `brief.md` et `eval-rubric.md`) et de celle du
Générateur (qui a produit les livrables) : aucun fichier de travail, aucun
raisonnement, aucun état ne sont partagés entre les trois.

Je n'ai modifié **aucune ligne** du dépôt en dehors des deux fichiers de
jugement que j'écris moi-même (`verdict.md` et `feedback/feedback-001.md`).
Toutes mes contre-preuves ont été montées dans des répertoires temporaires hors
dépôt, sous `/tmp/eval014_*/`. Je n'ai ni committé, ni poussé, ni créé de
branche. Je n'ai suggéré aucun correctif au Générateur pendant sa production :
je n'évalue donc pas mon propre travail.

*Vocabulaire employé plus bas, expliqué une fois.* **Adjugé** : un audit dont
une décision a été enregistrée au registre (`architecture/audit-ledger.jsonl`).
**Porte observable** : un contrôle mécanique rouge/vert visible dans la CI d'une
pull request, sans pouvoir bloquer la fusion côté GitHub. **Refus fournisseur** :
le CLI d'Anthropic refuse l'appel parce que le plafond de dépense mensuel de
l'organisation est atteint (code HTTP `429`). **Étape ignorée (« skipped »)** :
dans GitHub Actions, une étape dont la condition `if:` est fausse n'est pas
exécutée du tout — elle ne produit ni sortie, ni message, ni effet de bord.

---

## Périmètre jugé

Je juge exclusivement le commit `1bd1bd9` (« generateur: lot `014` — porte
observable des audits de PR … et refus fournisseur comme état »), sur la branche
`forge/014-pipeline-contre-audit-porte-e180`.

Contrôle du périmètre : `git diff --name-status HEAD~1 HEAD` ne montre que les
fichiers autorisés par le § Périmètre autorisé du brief — deux modules sous
`harness/pipeline/`, le fichier d'état, quatre preuves rouges, deux tests, les
deux workflows `.github/workflows/audit-guard.yml` et
`.github/workflows/pipeline-challenge.yml`, le registre de coût et les deux
livrables du lot. Aucun fichier des archives des briefs `001` à `013` n'est
touché (`git diff HEAD~1 HEAD` sur leurs répertoires est vide). Aucun fichier
`.github/workflows/pipeline-*.yml` n'est créé ni supprimé.

---

## 1. Gate mécanique rejoué

Commande rejouée :
`.venv/bin/python harness/verdict_audit.py harness/queue/briefs/014-pipeline-contre-audit-porte`

Code de sortie : `1`. Ligne finale : `VERDICT: REJECT`.

Les deux seuls contrôles en échec sont `verdict_numbers_traceable`
(« verdict.md missing ») et `verdict_is_not_self_authored` (« Author
frontmatter missing on generator-log.md or verdict.md »). Les deux ont la même
cause unique : le présent fichier n'existait pas encore au moment de la relance.
C'est l'état attendu et normal à ce stade — le Générateur n'a pas le droit
d'écrire `verdict.md`, donc le gate ne peut pas être vert avant que je l'écrive.
Je le cite tel quel, sans le passer outre.

Les huit autres contrôles sont au vert, dont `captures_differ_when_should` (les
deux paires rouge/vert diffèrent réellement), `no_empty_sample_pass` (aucun
compteur à échantillon vide) et `no_bare_python_alias`.

**Second passage, après écriture du présent fichier** : la même commande rejouée
donne code de sortie `0` et `VERDICT: ACCEPT`, les dix contrôles au vert, dont
`verdict_is_not_self_authored` qui compare les acteurs (`forge-generateur` d'un
côté, `forge-evaluateur` de l'autre) et non les chaînes de rôles.

**Avertissement de lecture, repris de la rubrique** : le gate juge la *forme* du
lot, pas sa substance. Un lot peut obtenir `ACCEPT` du gate et `REJECT` de
l'Évaluateur — **c'est exactement le cas ici**. Mon verdict de fond a sa propre
cause, indépendante du gate, exposée en § 4.

---

## 2. Reconstruction indépendante des compteurs

Chaque valeur ci-dessous a été remesurée par moi, avec mes propres fixtures
(inbox et registre synthétiques créés sous `/tmp/eval014_*/`), sans réutiliser
un seul fichier du Générateur et sans lire son manifeste avant de mesurer.

| compteur | valeur du manifeste | ma remesure | verdict |
|---|---|---|---|
| `audits_ciblant_pr` | `2` sur `3` | `2` audits sur `3` fichiers de l'inbox synthétique du test ciblent bien `feature-branch` | identique |
| `audits_non_adjuges_ciblant_pr` | `1` sur `2` | `1` seul des `2` ciblants est non adjugé (l'autre est `AUDIT_APPROVED`) | identique |
| `code_sortie_guard_pr_avec_audit_non_adjuge` | `1` | ma fixture « audit `AUDIT_CHALLENGED` ciblant la PR » → code de sortie `1` | identique |
| `code_sortie_guard_pr_sans_audit` | `0` | ma fixture « aucun audit ciblant » → code de sortie `0` | identique |
| `pipeline_workflows_count` | `5` | `5` avant le lot (arbre du commit `d1ed1f6`) et `5` après | identique |
| `audit_guard_job_count` | `3` | le one-liner de la rubrique liste `audit-check`, `cursor-scope`, `schema` — soit `3` | identique |
| `classification_transcript_429` | `vendor_refusal` | mon transcript synthétique `is_error` vrai + statut `429` → `vendor_refusal` | identique |
| `classification_transcript_succes` | `success` | mon transcript synthétique avec un résultat et `is_error` faux → `success` | identique |
| `classification_transcript_autre` | `other_error` | mon transcript synthétique avec statut d'erreur `500` → `other_error` | identique |
| `lignes_etat_refus_apres_log` | `1` | après mon appel à `log_refusal`, mon fichier d'état contient `1` ligne JSON valide, champs `timestamp`, `audit_id`, `error_type`, `api_error_status`, `fallback_attempted` tous présents | identique |
| `repli_codex_marque_acteur_reel` | `1` | mes deux fichiers de revue fictifs (avec et sans frontmatter) contiennent `forge-challenger-codex` après `mark_fallback_actor` | identique |
| `ci_pr_guard_collectes_014` | `11` | `11` tests collectés | identique |
| `ci_vendor_refusal_collectes_014` | `12` | `12` tests collectés | identique |

Deux cas non demandés au manifeste, que j'ai mesurés en plus : un transcript
**vide** et un transcript **inexistant** rendent tous deux `other_error` — pas de
succès simulé, conformément au brief.

Aucun compteur n'est mesuré sur un échantillon vide : le test
`test_counters_audits_ciblant_pr` monte trois fichiers d'audit réels dans son
inbox temporaire (non-goal n°`9` respecté).

---

## 3. Verdict condition par condition

| Condition de succès | Verdict | Preuve rejouée par moi |
|---|---|---|
| **SC1** — module `pr_audit_guard.py`, détection des audits non adjugés | **PASS** | Module suivi par git. `--help` documente bien `--head-branch`, `--head-commit`, `--inbox`, `--ledger`. La reconstruction exigée par la rubrique donne exactement la séquence attendue : audit ciblant par branche avec registre vide → sortie `1` ; après ajout d'`AUDIT_CHALLENGED` → sortie `1` ; après ajout d'`AUDIT_APPROVED` → sortie `0`. Ciblage par les sept premiers caractères du commit → sortie `1`. Aucun ciblage → sortie `0`. Les huit scénarios de SC1 sont tous couverts par des tests nommés. Contrôle du faux positif exigé par la rubrique : sur l'inbox et le registre **réels** du dépôt (`34` audits), une branche de PR ordinaire donne « Aucun audit ne cible cette PR » et sortie `0` ; une branche réellement ciblée par deux audits `AUDIT_ARCHIVED` donne aussi sortie `0`. La garde n'est donc ni aveugle ni bloquante par défaut. |
| **SC2** — job `audit-check` dans `audit-guard.yml` | **PASS** | Aucun fichier `pipeline-*.yml` créé (comptage identique avant/après ; `git diff --name-status` ne montre que deux `M`, aucun `A` sur ce motif). Le job `audit-check` est présent, porte `if: github.event_name == 'pull_request'`, et appelle la garde avec `${{ github.head_ref }}` et `${{ github.event.pull_request.head.sha }}` — les deux expressions exigées par l'amendement de la rubrique, aucune valeur en dur. Le job est ajouté à `audit-guard.yml`, pas ailleurs. |
| **SC3** — module `vendor_refusal.py`, classification et consignation | **PASS** (voir réserve N`1`) | Le module et `harness/pipeline/vendor-refusal-state.jsonl` sont tous deux suivis par git ; `git check-ignore` ne les exclut pas ; le fichier d'état est bien committé vide. Les trois fonctions exigées par la rubrique (`classify`, `log_refusal`, `mark_fallback_actor`) existent avec les signatures décrites. `classify` lit le fichier ligne par ligne, ne fait aucun appel réseau, n'importe que la bibliothèque standard. Mes trois classifications reconstruites correspondent. `mark_fallback_actor` place l'encart juste après le frontmatter quand il y en a un, en tête sinon — vérifié sur mes deux fichiers fictifs. Les six scénarios de SC3 sont couverts. |
| **SC4** — repli Codex dans `pipeline-challenge.yml` | **FAIL** | Les étapes existent, mais **la chaîne causale promise ne se déclenche pas dans le cas mesuré**. Détail complet en § 4 (échecs B`1` et B`2`). |
| **SC5** — deux paires de preuves rouges | **PASS** | Les quatre fichiers `.txt` existent et sont suivis par git. Le fichier rouge de la paire A contient bien une ligne `FAILED` sur `test_exits_1_when_audit_challenged`, le vert seulement `PASSED` ; idem pour la paire B sur `test_classify_429_returns_vendor_refusal`. **Contre-preuves montées par moi**, dans mes propres copies hors dépôt : sabotage A (la fonction principale rend `0` sans consulter le registre) → le test rougit avec `assert 0 == 1` ; sabotage B (`classify` rend toujours `other_error`) → le test rougit avec `'other_error' != 'vendor_refusal'`. Les deux sabotages se reproduisent exactement. |
| **SC6** — suite complète verte | **PASS** | Rejouées par moi : `harness/tests/` → `337` passés et `16` ignorés (tests Unity sur Linux, attendus) ; `sim/tests/` → `35` passés. Aucun `FAILED`. Les deux sorties figurent bien dans le journal du Générateur (`6` occurrences du mot « passed », au-delà du seuil de `2` de la rubrique). Archives des briefs `001` à `013` intactes. |
| **SC7** — registre de coût | **PASS** | Le rapport du registre affiche `cursor=1` pour le lot `014`. La dernière ligne de `harness/queue/cost-ledger.jsonl` porte le backend `cursor`, l'événement `generator-run` (avec tiret, pas tiret bas), un chemin de brief contenant `014` et l'identifiant d'audit `CURSOR-a600532-fusion-sans-contre-audit`. |

---

## 4. Échecs bloquants

### B`1` — SC4 : dans le cas mesuré (refus `429`), aucune des étapes ajoutées ne s'exécute

C'est l'échec décisif. Le lot ajoute la mécanique de détection, de consignation
et de repli, mais la place derrière une condition qui la rend inatteignable
précisément dans la situation pour laquelle elle a été écrite.

**Le fait mesuré, tel que le brief le pose** (§ Volet B) et tel que l'audit source
le documente (§ 5.3 de `architecture/inbox/CURSOR-a600532-fusion-sans-contre-audit.md`) :
le CLI rend une ligne portant `"api_error_status":429` puis **l'étape se termine
en erreur** — l'audit montre la ligne `##[error]Process completed with exit code 1.`

**Ma reproduction locale.** J'ai placé dans mon `PATH` un faux CLI qui imite ce
cas (il écrit la ligne `429` puis se termine en erreur), puis j'ai exécuté le
corps exact de l'étape « Invoke claude-challenger headless », `set -euo pipefail`
et redirection par `tee` compris. Résultat : le code de sortie de l'étape est `1`.
J'ai vérifié séparément que le transcript ainsi écrit serait bien classé
`vendor_refusal` par le module — **le module fonctionne, ce n'est pas lui qui est
en cause.**

**La sémantique GitHub Actions, étape par étape.** La documentation officielle de
GitHub est explicite : *« A default status check of `success()` is applied unless
you include one of these functions »*, et, pour le cas d'un prédécesseur en
échec : *« you must still include `failure()` to override the default status
check of `success()` that is automatically applied to `if` conditions that don't
contain a status check function »*. Autrement dit, une condition `if:` qui ne
contient ni `success()`, ni `failure()`, ni `always()`, ni `cancelled()` est
implicitement transformée en « ET l'étape précédente a réussi ».

Or aucune des étapes ajoutées ne contient de fonction de statut :

| étape | condition écrite | condition réellement évaluée | ce qui se passe après le refus `429` |
|---|---|---|---|
| Invoke claude-challenger headless | `steps.check.outputs.available == 'true'` | (inchangée par le lot) | **échoue**, code `1` |
| Post-hoc budget marking | `steps.check.outputs.available == 'true'` | `success() && …` | **ignorée** |
| Classify vendor refusal | `steps.check.outputs.available == 'true'` | `success() && …` | **ignorée** — `classify` n'est jamais appelée, `log_refusal` non plus |
| Repli Codex si refus fournisseur | `… && steps.classify_refusal.outputs.classification == 'vendor_refusal'` | `success() && …` | **ignorée** — doublement : le statut est en échec, et la sortie de l'étape précédente ignorée est vide |
| Publish the review | `… && (classification == 'success' \|\| codex_success == 'true')` | `success() && …` | **ignorée** |

Conséquence, dans le seul scénario que le Volet B existe pour traiter : le job
est rouge, aucune ligne n'est écrite dans `harness/pipeline/vendor-refusal-state.jsonl`,
aucun repli n'est tenté, aucun `::warning::` n'est émis. C'est **mot pour mot le
comportement d'avant le lot**, tel que le brief le décrit : « Aucune revue n'est
produite, aucun état n'est consigné, aucun repli n'est tenté. La chaîne causale
est rompue sans trace. » Le changement causal exigé n'est pas livré.

Ce n'est pas une réserve de style. La rubrique demande en SC4 que « l'absence de
repli aboutisse à un exit `1` **documenté** » : ici l'exit `1` vient de l'étape
d'invocation, et rien n'est documenté, puisque l'étape qui porte le message n'est
jamais atteinte. C'est aussi l'application directe de la règle durement acquise
n°`7` : un livrable présent n'est pas un livrable qui fonctionne.

**Comment le corriger, et à quoi je le re-vérifierai.** Fichier concerné :
`.github/workflows/pipeline-challenge.yml`. Il faut que l'échec de l'étape
d'invocation cesse d'interrompre la suite du job — par exemple en marquant cette
étape `continue-on-error: true` (son échec est alors une information, plus un
arrêt), et en donnant aux étapes « Classify vendor refusal » et « Repli Codex »
une condition qui contient une fonction de statut, du type
`if: ${{ !cancelled() && … }}`. Toute autre construction équivalente convient.
Critère de re-vérification, à fournir dans le journal de l'itération `2` : le
déroulé étape par étape du cas `429`, montrant (a) que « Classify vendor refusal »
s'exécute, (b) qu'une ligne apparaît dans le fichier d'état, (c) que l'étape de
repli s'exécute et se termine en erreur avec son `::warning::` quand les
identifiants Codex sont absents, et (d) que le job reste **rouge** au total —
jamais vert sans revue produite. Le mieux serait de matérialiser ce déroulé par
un test mécanique (un faux CLI qui rend le cas `429`, comme ma reproduction) plutôt
que par un raisonnement en prose.

### B`2` — SC4 : la condition de l'étape de publication existante a été resserrée, ce qui crée un chemin silencieux

Le brief autorise, sur ce fichier, l'ajout d'étapes après l'invocation, et
l'ajout de `harness/pipeline/vendor-refusal-state.jsonl` au `git add` de l'étape
de publication. Ce second point est correctement fait. Mais le lot a aussi
**modifié la condition** de l'étape existante « Publish the review », qui passe de
`steps.check.outputs.available == 'true'` à cette même condition **et**
« la classification vaut `success` **ou** le repli Codex a réussi ».

Ce resserrement introduit un mode d'échec silencieux qui n'existait pas :
si l'appel réussit mais que le transcript est illisible ou absent, `classify`
rend `other_error`, l'étape de publication est **ignorée**, le job se termine
**vert**, et la revue produite est perdue sans le moindre message. Ce n'est pas
un risque théorique : le fichier se défend déjà explicitement contre cette
dérive de format, à l'étape « Post-hoc budget marking », par un
`::warning::post-hoc cost marking refused (unreadable transcript format)`.
Le code d'avant le lot, lui, entrait toujours dans l'étape et émettait
`::warning::the invocation left no review` quand il n'y avait rien à publier.
Le lot remplace donc un avertissement par un silence, dans le fichier même que
l'audit source critique pour « rompre la chaîne causale sans trace ».

**Comment le corriger, et à quoi je le re-vérifierai.** Fichier concerné :
`.github/workflows/pipeline-challenge.yml`. Deux voies acceptables : rétablir la
condition d'origine de l'étape de publication et ne garder que la ligne `git add`
ajoutée ; ou conserver la condition resserrée mais ajouter une étape explicite qui,
lorsque la publication est sautée alors qu'une revue existe dans
`architecture/reviews/`, émet un `::warning::` et se termine en erreur. Critère de
re-vérification : montrer qu'il n'existe aucun chemin où le job finit vert alors
qu'une revue a été produite et n'a pas été publiée.

---

## 5. Réserves non bloquantes

Ces points n'empêchent pas à eux seuls la recevabilité, mais ils affaiblissent le
lot et devraient être traités.

**N`1` — la fonction `mark_fallback_attempted` n'existe nulle part.** Le brief la
nomme en SC3 comme étant la fonction qui met à jour le champ `fallback_attempted`.
Une recherche sur tout `harness/` ne la trouve que dans le texte du brief. Le
champ est donc écrit à « faux » et ne change jamais. Conséquence concrète : le
fichier d'état persistant ne pourra jamais dire si un repli a été tenté, ce qui
retire une part de sa raison d'être à l'« état explicite » du Volet B. La rubrique
n'exigeant que trois fonctions, je ne bloque pas là-dessus. Fichier concerné :
`harness/pipeline/vendor_refusal.py`. Re-vérification : soit la fonction existe et
un test montre qu'elle fait passer le champ à « vrai », soit le champ disparaît
du format plutôt que de mentir par défaut.

**N`2` — le repli Codex se déclare réussi avant d'avoir posé le marqueur
d'acteur.** L'étape écrit `codex_success=true` puis cherche le fichier de revue
par un motif qui n'est en fait pas un motif : `architecture/reviews/CLAUDE-<audit_id>.md`,
un chemin exact. Si Codex nomme sa revue autrement, la boucle ne trouve rien,
aucun message n'est émis, et la revue est publiée **sans** l'encart « Acteur
réel » — donc attribuée implicitement à Claude, ce qui est exactement le
travers de traçabilité d'acteur que le dépôt cherche à fermer. Fichier concerné :
`.github/workflows/pipeline-challenge.yml`. Re-vérification : l'absence de fichier
de revue trouvé doit produire un `::warning::` et empêcher `codex_success=true`.

**N`3` — dans la seule configuration qui existe aujourd'hui, la preuve du refus
n'est jamais committée.** `log_refusal` écrit sa ligne dans la copie de travail du
runner ; le seul `git add` de ce fichier se trouve dans l'étape de publication,
qui exige que le repli Codex ait réussi. Or les identifiants Codex sont absents et
le CLI `codex` n'est pas disponible (dérogation dûment consignée au manifeste, que
j'ai revérifiée : la commande rend « command not found »). Donc, en l'état, la
ligne de refus disparaît avec le runner, contrairement à l'exigence SC3 « la preuve
d'un refus doit être consultable depuis un clone ». Je classe ce point en réserve
et non en échec bloquant parce qu'il découle du dessin du brief lui-même (SC4 point
`1`.e lie le commit à la branche de revue) et non d'un écart du Générateur : c'est
au Planificateur d'arbitrer. Re-vérification : un chemin de consignation qui ne
dépend pas de la réussite du repli.

**N`4` — le chemin du transcript est réécrit à la main dans chaque étape.**
L'étape d'invocation, l'étape de marquage budgétaire et les deux étapes ajoutées
recomposent chacune de leur côté le même nom de fichier sous le répertoire
temporaire du runner. Une sortie d'étape unique, réutilisée par les suivantes,
supprimerait le risque de divergence. Fichier concerné :
`.github/workflows/pipeline-challenge.yml`.

---

## 6. Violations de périmètre

Aucune. Je note explicitement, parce que ce sont des interdictions qui ont déjà
été violées sur ce dépôt :

- Aucun fichier `.github/workflows/pipeline-*.yml` n'a été créé (non-goal n°`10`).
- Aucun des gardes existants de `pipeline-challenge.yml` n'a été supprimé ni
  modifié : le kill-switch par label, la lecture du mode à l'exécution, le
  pré-contrôle budgétaire et le plafond natif par appel apparaissent inchangés
  dans le diff, qui ne contient aucune suppression de ligne les concernant.
  Seule la condition de l'étape de publication a bougé — voir B`2`, qui n'est pas
  l'un des quatre gardes nommés.
- Le Générateur n'a modifié ni `brief.md`, ni `eval-rubric.md`, ni `verdict.md`.
- Aucun condensé SHA256 recopié en clair dans les livrables du lot.
- Aucun fichier hors du périmètre autorisé n'est touché ; les archives des briefs
  `001` à `013` sont intactes.

---

## 7. Ce qui est réellement bien fait

Je le note parce que la sévérité n'est pas une négativité de principe, et parce
que l'itération `2` ne doit pas défaire ce qui tient :

- Les deux modules sont sobres, réellement autonomes de la bibliothèque standard,
  et testables sans contexte GitHub Actions — comme demandé. Mes reconstructions
  passent toutes du premier coup, sans ajustement.
- Le choix conservateur sur l'état `AUDIT_STALE` (traité comme non adjugé) est
  correctement implémenté et correspond au raisonnement du brief.
- Le contrôle du faux positif est réussi : sur l'inbox réelle, la garde reste
  verte pour une PR ordinaire. C'est le point qui aurait pu rendre le lot
  inutilisable et il est propre.
- Les deux paires de preuves rouges sont honnêtes : mes propres sabotages,
  écrits sans lire ceux du Générateur, produisent la même rougeur au même
  endroit.
- Le journal recopie ses sorties réelles au lieu de les résumer, et déclare sa
  dérogation budgétaire au bon endroit.

---

## Verdict final : **REJECT**

Motif unique et suffisant : **SC4 n'est pas satisfaite**. Le Volet B du brief
demande un changement causal — « le refus fournisseur devient un état distinct
(consigné, persistant, lisible depuis un clone), et le workflow tente le repli
Codex au lieu d'échouer sec à l'identique ». Dans le cas mesuré, le workflow livré
échoue sec à l'identique : les étapes qui consignent et qui replient ne
s'exécutent pas (B`1`), et une nouvelle voie silencieuse a été ouverte sur l'étape
de publication (B`2`).

SC1, SC2, SC3, SC5, SC6 et SC7 sont satisfaites et vérifiées par mes propres
mesures. Le Volet A (la porte observable) est, lui, livré et fonctionnel : c'est
un vrai acquis, à conserver tel quel. L'itération `2` ne devrait toucher que
`.github/workflows/pipeline-challenge.yml` et, pour la réserve N`1`,
`harness/pipeline/vendor_refusal.py`.

Détail actionnable pour le Générateur : `feedback/feedback-001.md`.

---
---

# Verdict — itération 2

**Authored**: 2026-08-13T12:12:00Z
**Author**: forge-evaluateur

---

## Note de transparence

Même convention qu'à l'itération `1` : rôle natif `forge-evaluateur` en en-tête,
acteur réel = sous-agent Cursor orchestré par un agent Cursor Cloud remplaçant le
CTO, session distincte de celle du Planificateur et de celle du Générateur.
Je n'ai écrit que `verdict.md` (cette section ajoutée, sans toucher un mot de ma
section d'itération `1`) et `feedback/feedback-002.md`. Mes contre-preuves sont
montées hors dépôt, sous `/tmp/eval014_it2/`. Aucun commit, aucun push, aucune
branche.

Périmètre jugé : le commit `cbd1e1f`, qui modifie exactement les cinq fichiers
annoncés (`git diff --name-status HEAD~1 HEAD`) : le workflow
`.github/workflows/pipeline-challenge.yml`, `harness/pipeline/vendor_refusal.py`,
`harness/tests/test_vendor_refusal.py`, et les deux livrables. Aucune archive des
briefs `001` à `013` touchée.

---

## 1. Gate mécanique rejoué

Commande rejouée :
`.venv/bin/python harness/verdict_audit.py harness/queue/briefs/014-pipeline-contre-audit-porte`

Code de sortie : `0`. Ligne finale : `VERDICT: ACCEPT`. Les dix contrôles au vert.
Comme à l'itération `1`, ce vert porte sur la **forme** du lot. Mon verdict de
fond a sa propre cause.

Compteurs remesurés par moi après modification : `ci_vendor_refusal_collectes_014`
vaut bien `14` (le manifeste a été mis à jour, il annonçait `11` plus trois
au tour précédent), `ci_pr_guard_collectes_014` reste à `11`. Les onze autres
compteurs sont inchangés depuis mes reconstructions d'itération `1`, que je ne
rejoue pas ici puisque ni les modules ni les fixtures concernés n'ont bougé —
sauf `vendor_refusal.py`, dont j'ai remesuré les trois classifications : elles
sont inchangées.

Suites rejouées par moi : `harness/tests/` → `339` passés et `16` ignorés ;
`sim/tests/` → `35` passés. Aucun `FAILED`.

---

## 2. Ce qui est réellement corrigé

Je le dis d'abord, parce que c'est vrai et que l'itération `3` ne doit pas le
défaire.

**B`2` est corrigé, et bien corrigé.** La condition d'origine de l'étape de
publication est rétablie. Mon critère de re-vérification était : « démontrer
qu'il n'existe aucun chemin où le job se termine vert alors qu'une revue a été
produite et n'a pas été publiée ». Mon scénario F (transcript illisible, revue
produite) montre que l'étape de publication entre bien et publie. Le trou est
fermé.

**Le chemin `429` de B`1` fonctionne désormais.** C'est vérifié, pas concédé :
sur mon scénario A (refus `429`, aucun identifiant Codex — la réalité actuelle),
l'étape de classification s'exécute, la ligne d'état est écrite, l'étape de repli
s'exécute, émet ses deux `::warning::` et se termine en erreur, et le job est
**rouge**. Les points `1`, `2` et `3` de mon critère B`1` sont satisfaits.

**N`2`, N`3` et N`4` sont corrigés.** Le succès Codex n'est déclaré qu'après
insertion effective du marqueur, et l'absence de fichier de revue produit
maintenant un `::warning::` suivi d'un échec ; l'état du refus est commis sur une
branche dédiée sans dépendre de la réussite du repli ; le chemin du transcript
est exporté une seule fois et réutilisé partout.

**N`1` : la fonction demandée existe et son test passe.** Ma reconstruction
indépendante confirme qu'elle ne met à jour que la **dernière** ligne portant
l'identifiant visé et laisse les autres intactes. Voir toutefois la réserve N`5`.

---

## 3. Reconstruction indépendante : déroulé de tous les chemins du job

C'est le cœur de cette itération. Mon critère B`1`.`4` disait : « le job **rouge**
au total, jamais vert sans revue produite ». Le journal du Générateur n'énumère
que deux chemins (succès, et `429` sans repli). J'ai déroulé **tous** les chemins.

Méthode, entièrement reproductible depuis `/tmp/eval014_it2/simulate_job.py` : mon
script **extrait du vrai fichier de workflow** les métadonnées de chaque étape du
job (nom, identifiant, condition `if:`, présence de `continue-on-error`) — il ne
recopie pas ma lecture du YAML, il la lit — puis déroule le job en appliquant les
trois règles documentées de GitHub Actions :

1. une condition `if:` qui ne contient aucune fonction de statut reçoit
   implicitement « ET l'étape précédente a réussi » (doc GitHub, section
   « Status check functions ») ;
2. `continue-on-error: true` laisse à l'étape son **résultat brut** (`outcome`)
   en échec mais rend sa **conclusion** (`conclusion`) réussie, donc le statut du
   job **n'est pas** dégradé ;
3. la conclusion du job est un échec dès qu'une étape a une conclusion en échec.

Le corps réel de l'étape d'invocation est exécuté avec un faux CLI par scénario,
et la classification est obtenue en appelant le module livré — pas en la
supposant.

| scénario | classification | revue produite | conclusion du job | conforme ? |
|---|---|---|---|---|
| A. refus `429`, aucun identifiant Codex (réalité actuelle) | `vendor_refusal` | non | **rouge** | oui |
| B. refus `429`, identifiants Codex présents mais CLI absent | `vendor_refusal` | non | **rouge** | oui |
| C. refus `429`, Codex réussit et produit une revue | `vendor_refusal` | oui | vert, revue publiée | oui |
| D. erreur non-`429` (statut `500`), aucune revue | `other_error` | non | **vert** | **NON** |
| E. le CLI plante avant d'écrire quoi que ce soit | `other_error` | non | **vert** | **NON** |
| F. transcript illisible mais revue produite | `other_error` | oui | vert, revue publiée | oui pour B`2` |
| G. succès normal | `success` | oui | vert, revue publiée | oui |

Les scénarios D et E sont l'objet de l'échec bloquant B`3`.

---

## 4. Échecs bloquants

### B`3` — un échec d'invocation non-`429` rend désormais le job **vert** sans qu'aucune revue soit produite

**Fichier** : `.github/workflows/pipeline-challenge.yml`

Avant ce lot, l'étape d'invocation n'avait pas de `continue-on-error` : n'importe
quel échec du CLI rendait le job rouge. L'itération `2` ajoute
`continue-on-error: true` pour rendre les étapes suivantes atteignables — l'idée
est bonne — mais **rien ne relève l'échec** quand la classification n'est pas
`vendor_refusal`. Dans ce cas, l'étape de consignation et l'étape de repli sont
toutes deux ignorées (leur condition exige `vendor_refusal`), et l'étape de
publication entre, ne trouve rien à publier, émet son `::warning::` et se termine
avec un code `0`. Conclusion du job : **vert**.

Mes scénarios D (statut `500`) et E (CLI qui plante, transcript vide) le
démontrent tous deux.

Pourquoi c'est bloquant, sur trois fondements indépendants :

1. **Le brief l'interdit nommément.** SC4 point `2` : « Si la classification est
   `other_error` ou si le transcript est absent : comportement inchangé (`exit 1`
   existant, le job échoue). » Le job ne échoue plus.
2. **Mon critère B`1`.`4` est violé mot pour mot** : « le job rouge au total,
   **jamais vert sans revue produite** ». Les scénarios D et E sont exactement
   cela.
3. **L'échec devient invisible à l'escalade.** J'ai relu
   `.github/workflows/pipeline-failure-escalate.yml` : son job porte
   `if: github.event.workflow_run.conclusion == 'failure'`. Un run vert ne
   déclenche donc aucune escalade. Le lot transforme une panne fournisseur non-`429`
   en run vert, c'est-à-dire en panne dont personne n'est prévenu — le mode
   d'échec que l'audit source décrit comme « la chaîne causale rompue sans
   trace », déplacé plutôt que fermé.

**Correction attendue** : après la classification, relever explicitement l'échec
de l'invocation quand la classification n'est pas `vendor_refusal` — par exemple
une étape terminale conditionnée sur « le résultat brut de l'invocation est en
échec ET la classification n'est pas `vendor_refusal` » qui émette un
`::warning::` et se termine en erreur. Toute construction équivalente convient.

**Critère de re-vérification** : le tableau des sept chemins ci-dessus, refait par
le Générateur, avec la colonne « conclusion du job » à **rouge** pour D, E et F,
et à vert seulement pour C et G.

### B`4` — la preuve mécanique exigée par le critère B`1` n'a pas été produite

**Fichier** : `harness/tests/test_vendor_refusal.py`

Mon feedback disait, sans ambiguïté : « Je préférerai de loin une preuve mécanique
à un raisonnement en prose : un test qui monte un faux CLI rendant le cas `429`
(comme ma reproduction) et qui vérifie la séquence, plutôt qu'une relecture du
YAML. Une prose de plus ne se distingue pas d'une prose fausse. »

Le test livré, `test_sequence_429_complete`, appelle successivement `classify`,
`log_refusal` et `mark_fallback_attempted` sur un fichier temporaire. Il ne
contient **aucune** référence au workflow, **aucun** sous-processus, **aucun** faux
CLI, **aucune** notion de `continue-on-error` ni de condition `if:` — je l'ai
vérifié par recherche de motifs : zéro occurrence. Il rejoue donc, sous un nouveau
nom, des fonctions qui étaient déjà toutes vertes à l'itération `1` et dont j'avais
écrit noir sur blanc « ce n'est pas le module qui est en cause ».

Le seul élément qui prétend établir le déroulé du job reste donc de la prose dans
le journal — prose qui, précisément, omet les chemins D, E et F et conclut à tort
« jamais vert sans revue produite ». C'est la démonstration en acte que la prose
ne suffit pas : celle-ci est fausse, et rien de mécanique ne l'a contredite.

**Correction attendue** : un test qui exerce la **logique de décision du
workflow**, pas les fonctions du module. Deux formes acceptables : un test qui lit
`.github/workflows/pipeline-challenge.yml`, en extrait les conditions et les
drapeaux `continue-on-error`, et vérifie la conclusion du job attendue pour chacun
des sept chemins (c'est ce que fait mon script, il est reproductible) ; ou un test
qui exécute les corps d'étapes avec de faux CLI et vérifie les codes de sortie.

**Critère de re-vérification** : le test doit **rougir** si l'on retire l'étape
qui relève l'échec de B`3`. Fournis cette preuve rouge, sinon le test ne prouve
rien.

### B`5` — la condition du garde `ci_budget_guard` a été resserrée, ce que le brief interdit nommément

**Fichier** : `.github/workflows/pipeline-challenge.yml`

L'étape « Post-hoc budget marking », qui appelle `ci_budget_guard record`, voit sa
condition passer de « les identifiants sont disponibles » à cette même condition
**et** « le résultat brut de l'invocation est une réussite ».

Or le brief liste, dans ses interdictions au Générateur : « Ne pas modifier les
gardes existants de `pipeline-challenge.yml` (kill-switch, mode,
`ci_budget_guard`, plafond `--max-budget-usd`) ». Et la rubrique classe en échec
disqualifiant : « Modification ou suppression des gardes existants de
`pipeline-challenge.yml` (kill-switch, mode, `ci_budget_guard`, plafond) —
sécurité du pipeline dégradée ».

Ce n'est pas qu'une question de lettre. La conséquence est réelle : un appel qui
échoue **après avoir dépensé** (une erreur serveur au bout de plusieurs tours, un
dépassement de `--max-turns`) n'est plus enregistré au registre mensuel. Le
plafond de dépense se met alors à sous-compter la dépense réelle — exactement le
genre de mesure fausse contre laquelle le garde existe. Le raisonnement du
Générateur (« un transcript `429` ne coûte rien, donc l'enregistrer serait
trompeur ») est correct **pour le cas `429`** et faux pour les autres échecs.

Je note aussi que le comportement d'avant le lot ne posait pas de problème : cette
étape se terminait déjà proprement en cas de transcript illisible, par son propre
`|| echo "::warning::post-hoc cost marking refused …"`.

**Correction attendue** : rétablir la condition d'origine de cette étape. Si le
Générateur estime que le cas `429` doit être exclu du marquage, ce n'est pas à lui
d'en décider : c'est une dérogation à demander au Planificateur, et elle doit
alors viser précisément la classification `vendor_refusal`, non l'ensemble des
échecs.

**Critère de re-vérification** : la condition de cette étape est identique à
celle d'avant le lot, ou une note du Planificateur autorise explicitement le
resserrement.

---

## 5. Réserves non bloquantes

**N`5` — `mark_fallback_attempted` est correcte mais inerte à l'endroit où le
workflow l'appelle.** L'étape « Commit état du refus fournisseur » se termine par
un retour à la branche d'origine. Or la ligne d'état vient d'être **commise sur la
branche dédiée** : en revenant sur la branche d'origine, git restaure le fichier
dans sa version d'origine, c'est-à-dire vide. Je l'ai démontré dans un vrai dépôt
temporaire (`/tmp/eval014_it2/gitsim`) : après l'étape, le fichier de l'arbre de
travail ne contient plus aucune ligne, et l'appel suivant à
`mark_fallback_attempted` ne trouve donc rien à mettre à jour — il ne lève aucune
erreur et ne fait rien. La branche poussée conserve la ligne avec le champ à
« faux », **même quand le repli a réussi**. Conséquence : le champ ne dira jamais
la vérité en CI, alors que son test unitaire passe. Correction : appeler
`mark_fallback_attempted` **avant** l'étape de commit, ou commiter l'état après le
repli, ou ne pas revenir de branche entre les deux. Re-vérification : un test qui
enchaîne les deux étapes dans un dépôt temporaire et lit le champ dans le commit
produit.

**N`6` — l'étape de commit d'état peut se terminer en restant sur la branche
dédiée.** Le cas « rien à commiter » exécute une sortie immédiate avec un code `0`
alors que le changement de branche a déjà eu lieu ; les étapes suivantes
travailleraient depuis cette branche. J'ai vérifié le comportement du shell : dans
la liste conditionnelle utilisée, la sortie immédiate est bien atteinte quand la
première commande réussit. Le cas est aujourd'hui improbable (une ligne vient
toujours d'être ajoutée), mais il n'est pas impossible et il est gratuit à fermer.

**N`7` — la branche `forge-bot/vendor-refusal-*` n'est jamais fusionnée.** La
trace du refus est donc consultable depuis un clone (l'exigence SC3 est
formellement satisfaite, et c'est un progrès réel par rapport à l'itération `1`),
mais `master` ne la portera jamais et rien ne relie cette branche à l'audit
concerné dans la vue du propriétaire. À arbitrer par le Planificateur, pas par le
Générateur.

Les réserves N`2`, N`3` et N`4` de l'itération `1` sont closes. N`1` est
formellement close mais donne lieu à N`5`.

---

## 6. Violations de périmètre

Aucune sur les fichiers : le lot ne touche que les cinq fichiers annoncés, aucune
archive antérieure, aucun fichier `pipeline-*.yml` créé, aucun condensé SHA256
recopié, et le Générateur n'a modifié ni `brief.md`, ni `eval-rubric.md`, ni
`verdict.md`.

Une violation de contrat, en revanche : le resserrement de la condition du garde
`ci_budget_guard` (B`5`), interdit nommément par le brief et listé comme
disqualifiant par la rubrique.

---

## Verdict final itération 2 : **REJECT**

SC4 reste non satisfaite. Le lot a réellement progressé — B`2` est fermé, le
chemin `429` de B`1` fonctionne, trois réserves sur quatre sont closes — mais le
correctif a **déplacé** le défaut au lieu de le fermer : là où l'échec `429` était
un rouge répété sans trace, un échec non-`429` est maintenant un **vert sans
trace**, invisible à l'escalade (B`3`). S'y ajoutent l'absence de la preuve
mécanique explicitement exigée (B`4`) et une modification d'un garde nommément
protégé (B`5`).

### Sur le plateau et l'escalade

Il n'y a **pas** de plateau au sens de la discipline de boucle : l'itération `2`
a produit des corrections réelles et vérifiables, elle n'a pas piétiné. Une
itération `3` a une chance raisonnable d'aboutir, et je recommande de la tenter
plutôt que d'escalader, pour trois raisons : les trois défauts restants sont
localisés dans **un seul fichier** de workflow plus **un fichier de test** ; aucun
ne demande de repenser les modules, qui sont sains ; et B`3` comme B`5` ont une
correction dont la forme est connue et courte.

Une réserve de méthode, toutefois, à porter à l'orchestrateur : B`4` montre que ce
lot est jugé sur des affirmations de prose au sujet d'un fichier de workflow que
personne ne peut exécuter ici. C'est la source des deux itérations perdues. Si
l'itération `3` livre encore un « déroulé » en prose au lieu d'un test qui rougit
quand on casse la chaîne, il faut escalader vers le propriétaire plutôt que
tenter une itération `4` : le problème ne serait alors plus le code, mais le fait
que rien dans ce dépôt ne contraint mécaniquement la sémantique d'un workflow.

Détail actionnable pour le Générateur : `feedback/feedback-002.md`.
