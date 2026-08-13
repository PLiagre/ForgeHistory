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
