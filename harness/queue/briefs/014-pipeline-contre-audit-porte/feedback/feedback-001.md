# Feedback pour l'itération `2` — Brief `014`

**Authored**: 2026-08-13T11:53:00Z
**Author**: forge-evaluateur

Verdict de l'itération `1` : **REJECT**, motif SC4. Tout le reste du lot est
recevable et vérifié — **ne le refais pas, ne le déplace pas**. Deux fichiers
seulement devraient bouger : `.github/workflows/pipeline-challenge.yml` (points
B`1`, B`2`, N`2`, N`4`) et `harness/pipeline/vendor_refusal.py` (point N`1`).

Vocabulaire, expliqué une fois : **étape ignorée (« skipped »)** = dans GitHub
Actions, une étape dont la condition `if:` est fausse n'est pas exécutée du tout,
elle ne produit ni sortie, ni message, ni effet de bord.

---

## Ce qui est validé et ne doit pas changer

- `harness/pipeline/pr_audit_guard.py` et `harness/tests/test_pr_audit_guard.py` :
  SC1 satisfaite, mes reconstructions concordent, pas de faux positif sur l'inbox
  réelle.
- Le job `audit-check` dans `.github/workflows/audit-guard.yml` : SC2 satisfaite.
- `harness/pipeline/vendor_refusal.py` pour les trois fonctions exigées : SC3
  satisfaite (une seule réserve, N`1`, ci-dessous).
- Les quatre fichiers de `harness/pipeline/proof_red/` : SC5 satisfaite, mes
  propres sabotages reproduisent la même rougeur.
- Les suites vertes et le registre de coût : SC6 et SC7 satisfaites.

---

## B`1` — bloquant. Les étapes ajoutées ne s'exécutent pas dans le cas mesuré

**Fichier** : `.github/workflows/pipeline-challenge.yml`

**Le problème, en une phrase** : quand le CLI rend le refus `429`, l'étape
« Invoke claude-challenger headless » se termine en erreur ; à partir de là,
GitHub Actions ajoute implicitement « ET l'étape précédente a réussi » à toute
condition `if:` qui ne contient pas de fonction de statut — donc « Classify
vendor refusal » et « Repli Codex si refus fournisseur » sont **ignorées**.
Aucune classification, aucune ligne dans le fichier d'état, aucun repli, aucun
`::warning::`. C'est exactement le comportement d'avant le lot.

**Preuves** :
- Documentation GitHub Actions, section « Status check functions » : *« A default
  status check of `success()` is applied unless you include one of these
  functions »* ; et *« you must still include `failure()` to override the default
  status check of `success()` that is automatically applied to `if` conditions
  that don't contain a status check function »*.
- Fait mesuré : audit source
  `architecture/inbox/CURSOR-a600532-fusion-sans-contre-audit.md` § 5.3, ligne
  `##[error]Process completed with exit code 1.` juste après la ligne portant
  `"api_error_status":429`.
- Ma reproduction locale : avec un faux CLI qui écrit la ligne `429` puis se
  termine en erreur, le corps exact de l'étape d'invocation (`set -euo pipefail`
  et redirection par `tee` compris) rend le code de sortie `1`. Le module, lui,
  classe bien ce transcript en `vendor_refusal` — **ce n'est pas le module qui est
  en cause**.

**Piste de correction** (à toi de choisir la construction ; il y en a plusieurs
qui marchent) : rendre l'échec de l'invocation non interruptif, par exemple avec
`continue-on-error: true` sur l'étape d'invocation, puis donner aux étapes de
classification et de repli une condition contenant une fonction de statut, du
genre `if: ${{ !cancelled() && … }}`. Attention à deux pièges :
- il faut que le job **reste rouge au total** dans le cas `429` sans repli
  possible ; `continue-on-error` seul rendrait le job vert, donc l'échec doit
  être porté par l'étape de repli (elle le fait déjà : `exit 1`) ;
- l'étape « Post-hoc budget marking » est aujourd'hui également ignorée en cas
  d'échec ; décide explicitement si elle doit s'exécuter ou non, et dis-le dans
  le journal, plutôt que de laisser le hasard de l'ordre des étapes décider.

**Critère de re-vérification que j'appliquerai** — le journal de l'itération `2`
doit montrer, pour le cas `429`, le déroulé étape par étape avec :
1. « Classify vendor refusal » **exécutée** (pas ignorée) ;
2. une ligne ajoutée à `harness/pipeline/vendor-refusal-state.jsonl` ;
3. l'étape de repli **exécutée**, émettant son `::warning::` et se terminant en
   erreur quand les identifiants Codex sont absents ;
4. le job **rouge** au total, jamais vert sans revue produite.

Je préférerai de loin une preuve mécanique à un raisonnement en prose : un test
qui monte un faux CLI rendant le cas `429` (comme ma reproduction) et qui vérifie
la séquence, plutôt qu'une relecture du YAML. Une prose de plus ne se distingue
pas d'une prose fausse.

---

## B`2` — bloquant. La condition de l'étape de publication a été resserrée, créant un silence

**Fichier** : `.github/workflows/pipeline-challenge.yml`

**Le problème** : l'étape existante « Publish the review » avait pour seule
condition `steps.check.outputs.available == 'true'`. Le lot y a ajouté « et la
classification vaut `success` ou le repli Codex a réussi ». Si l'appel réussit
mais que le transcript est illisible ou absent, `classify` rend `other_error`,
l'étape est **ignorée**, le job finit **vert**, et la revue produite est perdue
**sans aucun message**. Avant le lot, l'étape entrait toujours et émettait
`::warning::the invocation left no review` quand il n'y avait rien à publier.

Ce n'est pas un risque théorique : le même fichier se défend déjà contre la
dérive de format du transcript à l'étape « Post-hoc budget marking », par un
`::warning::post-hoc cost marking refused (unreadable transcript format)`.

**Pistes de correction, au choix** :
- rétablir la condition d'origine de l'étape de publication et ne conserver que
  la ligne `git add harness/pipeline/vendor-refusal-state.jsonl` que tu as
  ajoutée (c'est le minimum que SC4 point `1`.e demandait) ; ou
- conserver la condition resserrée, mais ajouter une étape qui, quand la
  publication est sautée alors qu'une revue existe dans `architecture/reviews/`,
  émet un `::warning::` et se termine en erreur.

**Critère de re-vérification** : démontrer qu'il n'existe **aucun** chemin où le
job se termine vert alors qu'une revue a été produite et n'a pas été publiée.

---

## N`1` — réserve. `mark_fallback_attempted` n'existe pas

**Fichier** : `harness/pipeline/vendor_refusal.py`

Le brief nomme cette fonction en SC3 comme celle qui met à jour le champ
`fallback_attempted`. Elle n'existe nulle part dans `harness/` : le champ est
écrit à « faux » et ne change jamais. Le fichier d'état ne peut donc jamais dire
si un repli a été tenté, ce qui vide une partie du sens de l'« état explicite »
du Volet B.

**Correction attendue** : soit écrire la fonction et un test qui prouve qu'elle
fait passer le champ à « vrai » (et l'appeler depuis l'étape de repli), soit
retirer le champ du format plutôt que de le laisser mentir par défaut. Les deux
sont acceptables ; le silence ne l'est pas.

**Critère de re-vérification** : si la fonction est ajoutée, un test dans
`harness/tests/test_vendor_refusal.py` qui écrit une ligne de refus, appelle la
fonction, relit le fichier d'état et vérifie que le champ vaut « vrai ».

---

## N`2` — réserve. Le repli se déclare réussi avant d'avoir posé le marqueur

**Fichier** : `.github/workflows/pipeline-challenge.yml`

L'étape écrit `codex_success=true`, **puis** cherche le fichier de revue par
`architecture/reviews/CLAUDE-<audit_id>.md` — un chemin exact, pas un motif. Si
Codex nomme sa revue autrement, la boucle ne trouve rien, aucun message n'est
émis, et la revue est publiée **sans** l'encart « Acteur réel » : elle passe donc
pour une revue de Claude. C'est précisément le défaut de traçabilité d'acteur que
le dépôt cherche à fermer.

**Correction attendue** : quand aucun fichier de revue n'est trouvé, émettre un
`::warning::` et ne pas positionner `codex_success=true`.

**Critère de re-vérification** : lecture de l'étape ; l'écriture de la sortie de
succès doit être postérieure et conditionnée à l'insertion effective du marqueur.

---

## N`3` — réserve. La preuve du refus n'est jamais committée dans la configuration actuelle

**Fichiers** : `.github/workflows/pipeline-challenge.yml` (et, potentiellement,
une décision du Planificateur)

`log_refusal` écrit dans la copie de travail du runner ; le seul `git add` de
`harness/pipeline/vendor-refusal-state.jsonl` se trouve dans l'étape de
publication, qui exige la réussite du repli Codex. Or les identifiants Codex sont
absents et le CLI `codex` n'est pas disponible (dérogation correctement consignée
au manifeste, que j'ai revérifiée). En l'état, la ligne de refus disparaît avec le
runner, contrairement à l'exigence SC3 « la preuve d'un refus doit être
consultable depuis un clone ».

Je ne bloque pas là-dessus parce que cela découle du dessin du brief lui-même
(SC4 point `1`.e lie le commit à la branche de revue) et non d'un écart de ta
part. **Si tu peux le traiter sans sortir du périmètre autorisé, fais-le** ; sinon,
écris-le explicitement dans ton journal comme une limite renvoyée au
Planificateur, plutôt que de la laisser passer en silence.

---

## N`4` — réserve. Le chemin du transcript est réécrit à la main dans chaque étape

**Fichier** : `.github/workflows/pipeline-challenge.yml`

Quatre étapes recomposent chacune de leur côté le même nom de fichier sous le
répertoire temporaire du runner. Une sortie d'étape unique, réutilisée ensuite,
supprimerait le risque de divergence. Amélioration de robustesse, pas une
exigence du brief.

---

## Rappels de discipline pour l'itération `2`

- **Ne committe pas, ne pousse pas, ne crée pas de branche.** C'est
  l'orchestrateur qui commite.
- N'écris ni `brief.md`, ni `eval-rubric.md`, ni `verdict.md`, ni ce fichier.
- Jamais l'interpréteur `python` nu dans tes commandes locales : toujours
  `.venv/bin/python`. (Dans les workflows, la convention du dépôt reste
  l'interpréteur installé par `actions/setup-python` sur le runner ; ne change
  rien à cet usage-là.)
- Ne recopie aucun condensé SHA256 en valeur hexadécimale.
- Ne crée aucun fichier `.github/workflows/pipeline-*.yml`.
- Ne touche pas aux quatre gardes existants de `pipeline-challenge.yml`
  (kill-switch par label, lecture du mode, pré-contrôle budgétaire, plafond
  natif par appel) : ils sont intacts aujourd'hui, ils doivent le rester.
- Recopie dans le journal les sorties réelles des suites `harness/tests/` et
  `sim/tests/`, comme tu l'as bien fait à l'itération `1`.
