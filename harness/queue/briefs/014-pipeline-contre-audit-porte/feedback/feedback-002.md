# Feedback pour l'itération `3` — Brief `014`

**Authored**: 2026-08-13T12:13:00Z
**Author**: forge-evaluateur

Verdict de l'itération `2` : **REJECT**, SC4 toujours non satisfaite. Trois points
bloquants (B`3`, B`4`, B`5`) et trois réserves (N`5`, N`6`, N`7`). Deux fichiers
devraient suffire : `.github/workflows/pipeline-challenge.yml` et
`harness/tests/test_vendor_refusal.py`.

---

## Ce qui est acquis — ne le défais pas

- **B`2` est fermé** : la condition d'origine de l'étape de publication est
  rétablie, une revue produite est publiée même si le transcript est illisible.
- **Le chemin `429` de B`1` fonctionne** : classification exécutée, ligne d'état
  écrite, étape de repli exécutée, `::warning::` émis, job rouge. Vérifié.
- **N`2`, N`3`, N`4` sont fermés** : succès Codex déclaré après marquage effectif,
  état commis sans dépendre du repli, chemin de transcript unique.
- **N`1`** : la fonction existe et se comporte correctement en isolation (elle ne
  met à jour que la dernière ligne portant l'identifiant visé — je l'ai vérifié
  sur ma propre fixture à trois lignes). Voir toutefois N`5`.

---

## B`3` — bloquant. Un échec non-`429` rend le job vert sans revue produite

**Fichier** : `.github/workflows/pipeline-challenge.yml`

**Le problème** : `continue-on-error: true` sur l'étape d'invocation était la bonne
idée pour rendre les étapes suivantes atteignables, mais **rien ne relève l'échec**
quand la classification n'est pas `vendor_refusal`. Les étapes de consignation et
de repli sont alors ignorées (elles exigent `vendor_refusal`), l'étape de
publication entre, ne trouve rien, émet son `::warning::` et sort avec un code `0`.
Conclusion du job : **vert**.

**Ce que j'ai mesuré** (déroulé des sept chemins, script reproductible
`/tmp/eval014_it2/simulate_job.py`, qui extrait les conditions et les drapeaux
`continue-on-error` du vrai fichier de workflow) :

| chemin | classification | revue | conclusion du job |
|---|---|---|---|
| refus `429`, pas d'identifiant Codex | `vendor_refusal` | non | rouge — correct |
| refus `429`, identifiants présents, CLI absent | `vendor_refusal` | non | rouge — correct |
| refus `429`, Codex réussit | `vendor_refusal` | oui | vert, publiée — correct |
| erreur statut `500` | `other_error` | non | **vert — incorrect** |
| CLI qui plante, transcript vide | `other_error` | non | **vert — incorrect** |
| transcript illisible, revue produite | `other_error` | oui | vert, publiée — B`2` OK, mais l'échec reste invisible |
| succès normal | `success` | oui | vert, publiée — correct |

**Trois raisons pour lesquelles c'est bloquant** :
1. Le brief l'interdit nommément, SC4 point `2` : « Si la classification est
   `other_error` ou si le transcript est absent : comportement inchangé (`exit 1`
   existant, le job échoue). »
2. Mon critère B`1`.`4` de l'itération `1`, mot pour mot : « le job rouge au total,
   **jamais vert sans revue produite** ».
3. `.github/workflows/pipeline-failure-escalate.yml` ne se déclenche que sur
   `conclusion == 'failure'`. Un run vert n'escalade rien : tu transformes une
   panne fournisseur en panne dont personne n'est prévenu. C'est le mode d'échec
   de l'audit source, déplacé et non fermé.

**Correction attendue** : relever explicitement l'échec après la classification —
par exemple une étape terminale conditionnée sur « le résultat brut (`outcome`) de
l'étape d'invocation est en échec **ET** la classification n'est pas
`vendor_refusal` », qui émette un `::warning::` puis se termine en erreur. Toute
construction équivalente convient. Attention à ne pas casser au passage le chemin
`429` (qui doit rester rouge par l'étape de repli) ni le chemin de succès (qui doit
rester vert).

**Critère de re-vérification** : le tableau des sept chemins ci-dessus, refait par
toi, avec la conclusion du job à **rouge** pour les trois chemins `other_error`
(y compris celui où une revue est produite : la revue doit être publiée **et** le
job rouge, parce que l'invocation a réellement échoué) et à vert seulement pour le
succès normal et le repli Codex réussi.

---

## B`4` — bloquant. La preuve mécanique demandée n'a pas été produite

**Fichier** : `harness/tests/test_vendor_refusal.py`

Mon feedback de l'itération `1` demandait « un test qui monte un faux CLI rendant
le cas `429` … plutôt qu'une relecture du YAML. Une prose de plus ne se distingue
pas d'une prose fausse. »

`test_sequence_429_complete` appelle `classify`, `log_refusal` et
`mark_fallback_attempted` sur un fichier temporaire. Recherche de motifs dans le
fichier de test : **zéro** occurrence de `pipeline-challenge`, de `subprocess`, de
`continue-on-error`, de `PATH`. Le test ne touche donc pas au workflow. Il rejoue
des fonctions déjà toutes vertes à l'itération `1`, dont j'avais écrit « ce n'est
pas le module qui est en cause ».

Résultat : la seule chose qui prétendait établir le déroulé du job était la prose
du journal — et cette prose est fausse, puisqu'elle omet les trois chemins
`other_error` et conclut « jamais vert sans revue produite ». C'est exactement la
raison pour laquelle j'exigeais du mécanique.

**Correction attendue** — deux formes acceptables, choisis :
- un test qui **lit** `.github/workflows/pipeline-challenge.yml`, en extrait pour
  chaque étape la condition `if:` et le drapeau `continue-on-error`, applique les
  trois règles de GitHub Actions (condition `if:` sans fonction de statut =
  « ET l'étape précédente a réussi » ; `continue-on-error` conserve l'`outcome` en
  échec mais met la `conclusion` à réussie ; le job échoue dès qu'une conclusion
  est en échec) et vérifie la conclusion attendue du job pour chacun des sept
  chemins ;
- ou un test qui exécute les corps d'étapes avec de faux CLI (`claude`, `codex`)
  placés dans le `PATH` et vérifie les codes de sortie obtenus.

**Critère de re-vérification, non négociable** : le test doit **rougir** si l'on
retire l'étape ajoutée pour B`3`. Fournis la paire rouge/vert correspondante, comme
tu l'as fait proprement pour les deux paires de SC5. Sans cette preuve rouge, le
test ne prouve rien et je considérerai B`4` non traité.

---

## B`5` — bloquant. Tu as resserré la condition du garde `ci_budget_guard`

**Fichier** : `.github/workflows/pipeline-challenge.yml`

L'étape « Post-hoc budget marking » (qui appelle `ci_budget_guard record`) a reçu
la condition supplémentaire « le résultat brut de l'invocation est une réussite ».

Le brief l'interdit nommément dans ses interdictions au Générateur : « Ne pas
modifier les gardes existants de `pipeline-challenge.yml` (kill-switch, mode,
`ci_budget_guard`, plafond `--max-budget-usd`) ». La rubrique classe cette
modification en échec disqualifiant.

Et la conséquence est réelle, pas seulement formelle : un appel qui échoue **après
avoir dépensé** (erreur serveur au bout de plusieurs tours, `--max-turns` atteint)
n'est plus enregistré. Le plafond mensuel se met à sous-compter la dépense réelle.
Ton raisonnement (« un transcript `429` ne coûte rien ») est juste pour le cas
`429` et faux pour tous les autres échecs. Note aussi que l'étape se protégeait
déjà toute seule du transcript illisible, par son propre
`|| echo "::warning::post-hoc cost marking refused …"`.

**Correction attendue** : rétablir la condition d'origine. Si tu penses que le cas
`429` doit être exclu du marquage, ce n'est pas ta décision : demande la dérogation
au Planificateur et, si elle est accordée, cible précisément la classification
`vendor_refusal`, jamais l'ensemble des échecs.

**Critère de re-vérification** : la condition de cette étape est identique à celle
d'avant le lot, ou une note datée du Planificateur autorise le resserrement.

---

## N`5` — réserve. `mark_fallback_attempted` est inerte là où le workflow l'appelle

**Fichier** : `.github/workflows/pipeline-challenge.yml`

L'étape « Commit état du refus fournisseur » commite la ligne d'état sur une
branche dédiée, puis revient à la branche d'origine. Ce retour de branche
**restaure le fichier d'état dans sa version d'origine, c'est-à-dire vide**. Je
l'ai vérifié dans un vrai dépôt temporaire : après cette étape, l'arbre de travail
ne contient plus aucune ligne. L'appel suivant à `mark_fallback_attempted`, dans
l'étape de repli, ne trouve donc rien à mettre à jour : il ne lève aucune erreur et
ne fait rien. La branche poussée garde le champ à « faux » **même quand le repli
a réussi**.

Ton test unitaire passe, et il est juste ; c'est le câblage qui annule son effet.

**Corrections possibles** : appeler `mark_fallback_attempted` **avant** l'étape de
commit ; ou commiter l'état après le repli plutôt qu'avant ; ou ne pas revenir de
branche entre les deux étapes.

**Critère de re-vérification** : un test qui enchaîne les deux étapes dans un dépôt
temporaire et lit le champ tel qu'il figure **dans le commit produit**, pas dans un
fichier isolé.

---

## N`6` — réserve. L'étape de commit d'état peut se terminer en restant sur la branche dédiée

**Fichier** : `.github/workflows/pipeline-challenge.yml`

Dans le cas « rien à commiter », la ligne conditionnelle sort immédiatement avec un
code `0` alors que le changement de branche a déjà eu lieu : les étapes suivantes
travailleraient depuis la branche dédiée. J'ai vérifié le comportement du shell :
la sortie immédiate est bien atteinte quand la première commande de la liste
réussit. Le cas est aujourd'hui improbable, mais il est gratuit à fermer (revenir à
la branche d'origine avant de sortir).

---

## N`7` — réserve. La branche d'état n'est jamais fusionnée

La trace du refus est consultable depuis un clone — l'exigence SC3 est formellement
satisfaite et c'est un progrès réel — mais `master` ne la portera jamais, et rien
ne relie cette branche à l'audit concerné dans la vue du propriétaire. C'est un
arbitrage pour le Planificateur : si tu ne peux pas le traiter dans le périmètre
autorisé, écris-le dans ton journal comme une limite renvoyée, ne la laisse pas
passer en silence.

---

## Rappels de discipline

- **Ne committe pas, ne pousse pas, ne crée pas de branche.**
- N'écris ni `brief.md`, ni `eval-rubric.md`, ni `verdict.md`, ni ce fichier.
- Jamais l'interpréteur `python` nu dans tes commandes locales : `.venv/bin/python`.
  (Dans les workflows, la convention du dépôt reste l'interpréteur du runner ; ne
  change rien à cet usage.)
- Ne touche pas aux quatre gardes nommés de `pipeline-challenge.yml` — et
  rétablis celui que tu as resserré (B`5`).
- Recopie dans le journal les sorties réelles des deux suites, comme tu l'as fait
  aux deux itérations.
- **Le plus important pour cette itération** : n'écris pas de déroulé en prose. Le
  déroulé doit être produit par un test qui rougit quand la chaîne est cassée. Deux
  itérations ont été perdues sur des affirmations de prose au sujet d'un fichier
  que personne ne peut exécuter ici.
