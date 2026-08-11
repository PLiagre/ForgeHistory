# Brief 010 : la couche contrat de la répartition des rôles — rendre Codex substituable à Claude sans casser l'anti-auto-jugement (issu de l'audit CURSOR-e9a6f4c-codex-passation-full-auto)

**Authored**: 2026-08-11T09:10:00Z
**Author**: forge-planificateur

## Provenance

Ce brief est la conversion des points retenus de l'audit `CURSOR-e9a6f4c-codex-passation-full-auto`.
- Audit source : `architecture/inbox/CURSOR-e9a6f4c-codex-passation-full-auto.md`
- Contre-audit : `architecture/reviews/CLAUDE-CURSOR-e9a6f4c-codex-passation-full-auto.md`
- Décision du propriétaire : `architecture/decisions/DECISION-CURSOR-e9a6f4c-codex-passation-full-auto.md`
- Points retenus : 3, 6, 7, 8, 10, 11, 12, 15, 16

Un audit n'instruit rien. À partir d'ici, **ce brief.md est la SEULE
instruction** (voir CLAUDE.md › Single Source of Instruction). L'audit et la
décision ci-dessus sont de la *provenance*, pas des ordres.

**Pourquoi ce brief est distinct du brief 009.** Le brief 009 câble un
maillon (`pipeline-challenge.yml`) et pose un plafond budgétaire ; ses
propres non-objectifs lui interdisent de toucher au contrat des rôles. Or
la décision du propriétaire porte précisément sur ce contrat. Un lot qui
violerait les non-objectifs de son propre brief serait incohérent : c'est
donc un brief séparé, et il ne dépend pas de l'état d'avancement du 009.

## World-Terms Requirement

Le harnais existe pour empêcher une seule chose : **que celui qui produit
un travail prononce lui-même sa recevabilité**. Tout le reste — le gate,
les compteurs, les briefs — est de l'outillage autour de cette interdiction.

Le propriétaire a décidé le 2026-08-11 que Codex développe le projet **et**
peut remplacer Claude comme juge lorsque Claude atteint son plafond de
crédit. Cette décision est légitime et elle rend la boucle survivable : sans
elle, le travail s'arrête net dès que Claude est plafonné. Mais telle
quelle, elle heurte le dépôt en deux endroits, l'un écrit et l'autre muet :

1. **L'endroit écrit.** `docs/rules/harness-roles.md` réserve aujourd'hui
   Planificateur et Évaluateur à Claude, et donne sa raison : c'est ce qui
   garde le contrôle `verdict_is_not_self_authored` porteur de sens.
   Appliquer la décision sans modifier cette règle, c'est violer le contrat
   en silence — exactement la faute que le harnais existe pour empêcher.
2. **L'endroit muet, et c'est le grave.** Le contrôle
   `verdict_is_not_self_authored` (`harness/verdict_audit.py:262-268`)
   compare deux **chaînes de rôle**, pas deux **acteurs** :

   ```python
   return CheckResult("verdict_is_not_self_authored", gen != ver, ...)
   ```

   Tant qu'un seul backend écrivait, `forge-generateur` ≠
   `forge-evaluateur` suffisait. Avec deux backends, la convention de
   nommage déjà employée dans le dépôt (`forge-evaluateur-codex` est
   l'auteur réel du verdict 009a, commit `c9e9291`) produit ceci : un lot
   dont le journal porte `forge-generateur-codex` et le verdict
   `forge-evaluateur-codex` est **accepté par le gate**, parce que les deux
   chaînes diffèrent. Le même acteur aurait produit et jugé, et la seule
   barrière mécanique du dépôt n'aurait rien vu.

Conséquence observable si l'on ne fait rien : dès la première fois où Codex
travaille seul de bout en bout — c'est-à-dire dès la première fois où le
plafond de crédit de Claude est atteint, donc précisément le cas que la
décision cherche à couvrir — le dépôt accepte un verdict auto-décerné en
affichant `[PASS]`. Le contrôle ne mentirait pas : il n'a jamais su
distinguer un acteur d'un rôle. C'est nous qui aurions cessé de savoir ce
qu'il mesure.

Ce brief ne câble aucun agent et n'accélère aucune boucle. Il rend la
décision du propriétaire **applicable sans perte de garantie** : la règle
dit ce que le propriétaire a décidé, le contrôle mécanique refuse ce que la
règle interdit, et le coût du nouveau backend est mesuré comme celui des
autres.

## Découpage en lots

Trois lots indépendants, livrables et jugeables séparément.

| lot | objet | dépendances |
|---|---|---|
| **010a** | Le contrat des rôles et l'anti-auto-jugement multi-backend | aucune |
| **010b** | Codex, backend de Générateur officiel **et mesuré** | 010a accepté (SC3 fixe le refus que SC11 réutilise) |
| **010c** | Le verrou de fusion : rendre mesurable ce qui bloque le zéro-intervention | aucune |

010a et 010c peuvent être produits en parallèle. 010b attend 010a.

## Success Conditions

### Lot 010a — le contrat des rôles et l'anti-auto-jugement multi-backend

**SC1.** Un ADR `docs/adr/0008-*.md` existe, daté, porte un champ `Status`
non vide, et enregistre quatre choses explicitement : (a) Codex peut tenir
le rôle d'Évaluateur ; (b) **uniquement** dans une session distincte
déclenchée par un tiers — la CI ou le propriétaire — jamais par la session
qui a produit le lot ; (c) l'option « sous-agent d'évaluation engendré par
le Générateur » est écartée, avec sa raison écrite (le producteur cadre son
juge : il rédige son instruction, choisit ce qu'il lui montre, consolide sa
réponse) ; (d) le fait déclencheur de la substitution, à savoir que Claude
a atteint son plafond de crédit. `docs/adr/README.md` gagne sa ligne.

**SC2.** `docs/rules/harness-roles.md` est modifié pour dire ce que SC1
enregistre, et **aucun autre fichier ne le paraphrase** :
`py -m pytest harness/tests/test_single_source_of_instruction.py -q` reste
vert. La règle reste la source unique ; l'ADR enregistre la décision, il ne
la duplique pas en instruction.

**SC3.** `verdict_is_not_self_authored` distingue **l'acteur** du **rôle**.
Un lot dont `deliverables/generator-log.md` porte
`**Author**: forge-generateur-codex` et dont `verdict.md` porte
`**Author**: forge-evaluateur-codex` est **REFUSÉ** par le gate.
Preuve red-first obligatoire, et dans cet ordre : écrire d'abord le test
contre le code actuel, montrer sa sortie rouge en la recopiant, puis
corriger, puis montrer sa sortie verte. Un test écrit après le correctif ne
prouve rien ici, parce que le défaut est précisément une absence de refus.

**SC3b.** *(amendement du 2026-08-11 — voir la note en fin de brief.)* Le
contrôle examine **chaque couple auteur du brief, pas seulement le premier**.
`read_field` utilise aujourd'hui `re.search`, qui rend la première occurrence
seulement : sur un brief multi-lots dont chaque lot ajoute sa propre section
signée, seul le lot le plus ancien est contrôlé. Il est **exigé** qu'un brief
portant `forge-generateur` puis `forge-generateur-codex` dans son journal, et
`forge-evaluateur` puis `forge-evaluateur-codex` dans son verdict, soit
analysé sur **tous** ses couples et non sur le premier. Preuve red-first :
contre le code actuel, un couple auto-jugé placé en seconde position doit
passer inaperçu ; après correctif, il doit être refusé. Les deux sorties sont
recopiées.

**SC4.** Le refus de SC3 porte sur l'acteur en général, **pas sur une liste
en dur de deux backends**. Ajouter un troisième acteur (par exemple
`forge-generateur-gemini` / `forge-evaluateur-gemini`) doit être refusé
**sans modifier le contrôle**. Un test le prouve avec un nom d'acteur qui
n'apparaît nulle part ailleurs dans le dépôt.

**SC5.** Aucune invalidation rétroactive. Le gate est exécuté sur **tous**
les répertoires de brief existants avant et après le correctif, et aucun
verdict ne passe de PASS à FAIL sur ce contrôle. Le compteur associé porte
le nombre de briefs comparés, pas une impression.

**SC6.** Le jugement croisé légitime reste accepté : le verdict 009a, écrit
par `forge-evaluateur-codex` contre un journal signé `forge-generateur`,
continue de passer. Acteurs différents, donc juge indépendant : c'est le
cas que le contrôle doit laisser vivre. Preuve par exécution réelle du gate
sur `harness/queue/briefs/009-full-auto-agent-invocation`, sortie recopiée.

### Lot 010b — Codex, backend de Générateur officiel et mesuré

**SC7.** `harness/backends/run_codex_generator.sh` existe et respecte le
contrat de `harness/backends/README.md` : même interface d'appel que
`run_cursor_generator.sh`, mêmes obligations de journal et de sortie.
Preuve : les deux scripts exposent la même signature d'arguments, montrée
côte à côte.

**SC8.** `.claude/commands/forge-run.md` connaît `--backend
claude|cursor|codex` — dans son `argument-hint`, dans sa description
d'option, et dans sa branche d'exécution. Les trois occurrences sont
citées par chemin et numéro de ligne.

**SC9.** Le coût du backend Codex est **mesuré**, pas seulement déclaré.
`py harness/backends/ledger.py report` fait apparaître une ligne `codex`
avec son nombre d'invocations. Risque nommé par l'audit et retenu ici : un
backend déclaré mais non compté reproduirait à l'identique le défaut déjà
ouvert sur le backend Cursor (`CURSOR-6231186` FINDING-ARCH-003). Si le
coût en jetons n'est pas récupérable pour Codex, c'est une dérogation à
déclarer selon la table ci-dessous — pas un compteur à inventer.

**SC10.** Un ADR `docs/adr/0009-*.md` enregistre Codex comme backend de
développement officiel, avec `Status`, et `docs/adr/README.md` gagne sa
ligne.

**SC11.** Le wrapper refuse de s'exécuter s'il produirait un lot dont le
verdict est déjà signé par le même acteur. Le refus réutilise la fonction
de SC3 ; il ne la réimplémente pas. Preuve : appel réel du wrapper sur un
répertoire préparé pour ce cas, sortie et code de retour recopiés.

### Lot 010c — le verrou de fusion

**SC12.** Un test committé lit `.github/workflows/merge-bot.yml`
**lui-même** et affirme ce que le dépôt auto-fusionne réellement
aujourd'hui : les préfixes de branche acceptés (`cursor/`, `forge-bot/`) et
les chemins autorisés (`architecture/inbox/`, `architecture/reviews/`,
`harness/queue/briefs/*/feedback/`). Si quelqu'un élargit cette liste, le
test devient rouge et l'élargissement devient visible. Le test lit le
fichier ; il ne recopie pas ses valeurs dans une constante.

**SC13.** Un document court énonce la chaîne du zéro-intervention et nomme
**l'étape humaine exacte** qui subsiste. Il ne promet rien qu'aucun
workflow n'exécute — c'est le défaut C4 relevé sur le lot 009a, et il ne
doit pas être reproduit ici.

**SC14.** Compteur mesuré : sur les vingt dernières pull requests fusionnées,
combien auraient satisfait les conditions de `merge-bot.yml` sans
intervention. La commande est citée et sa sortie recopiée. Ce nombre est le
point de départ chiffré de toute discussion sur l'élargissement.

**SC15.** La porte conditionnelle qui remplacerait le clic du propriétaire
est **spécifiée** — ses prédicats exacts et, pour chacun, la preuve qu'il
lit (CI verte, gate mécanique ACCEPT, verdict indépendant ACCEPT écrit par
un acteur différent du producteur, audit Cursor déposé) — et elle n'est
**pas activée**. `git diff` sur `.github/workflows/` doit être vide pour ce
lot. La spécification rend l'activation possible en une décision ; elle ne
la prend pas.

## Non-Goals

Ce brief ne doit explicitement PAS :

1. **Câbler un maillon agent.** `pipeline-audit.yml`,
   `pipeline-challenge.yml` et `pipeline-forge-run.yml` gardent leurs
   `TODO(operator` intacts. Le maillon challenge appartient au lot 009c ;
   les deux autres n'ont pas encore de brief.
2. **Modifier quoi que ce soit sous `.github/workflows/`.** Y compris
   `merge-bot.yml`, y compris pour l'améliorer. Le lot 010c mesure et
   spécifie ; il ne touche pas.
3. **Élargir la denylist d'auto-fusion.** La protection de branche est
   indisponible sur ce plan GitHub (`HTTP 403`, vérifié le 2026-08-11) :
   cette denylist est la seule barrière réelle. L'élargir demande une
   décision du propriétaire enregistrée sous `architecture/decisions/`.
4. **Donner un droit d'écriture à Hermes.** Hermes reste en lecture seule.
   Son contrat d'écriture fera l'objet d'un brief distinct.
5. **Toucher au brief 009, à ses lots, à ses verdicts ou à ses feedbacks.**
   Les deux briefs avancent en parallèle sans se croiser.
6. **Modifier `VISION.md`.**
7. **Affaiblir un contrôle existant.** Le lot 010a rend
   `verdict_is_not_self_authored` **plus strict** ; toute modification qui
   ferait passer un cas aujourd'hui refusé est disqualifiante, et SC5 est là
   pour le prouver dans les deux sens.

## Required Counters

| nom | source de l'échantillon | dénominateur |
|---|---|---|
| `self_authored_multibackend_refused_test_count` | fonctions de test qui prouvent le refus d'un couple `<role>-<acteur>` identique | nombre de tests ajoutés pour SC3 |
| `author_pairs_examined_per_brief` | couples auteur extraits du journal et du verdict du brief 009 par le contrôle corrigé | nombre réel de couples présents dans ces deux fichiers |
| `second_position_self_judgment_refused` | couple auto-jugé placé en seconde position, avant puis après correctif | 1 exécution de chaque côté, les deux sorties recopiées |
| `unknown_actor_refused_without_code_change` | exécution du contrôle sur un acteur absent du dépôt, sans modifier le contrôle | 1 exécution, sortie recopiée |
| `briefs_gate_verdict_unchanged_count` | gate exécuté sur chaque répertoire de brief avant et après le correctif | nombre total de répertoires de brief comparés |
| `cross_actor_judgment_still_accepted` | gate réel sur le brief 009 (journal `forge-generateur`, verdict `forge-evaluateur-codex`) | 1 exécution |
| `forge_run_backend_mentions_count` | occurrences de `codex` comme valeur de `--backend` dans `.claude/commands/forge-run.md` | 3 emplacements attendus (hint, option, branche) |
| `codex_invocations_in_ledger` | sortie de `py harness/backends/ledger.py report` | nombre de lignes de backend rapportées |
| `mergebot_allowed_prefixes_count` / `mergebot_allowed_paths_count` | lecture de `.github/workflows/merge-bot.yml` par le test de SC12 | valeurs lues dans le fichier, jamais recopiées en dur |
| `recent_prs_automergeable_count` | vingt dernières PR fusionnées confrontées aux règles réelles | 20 |
| `workflows_diff_bytes` | `git diff --stat` sur `.github/workflows/` pour le lot 010c | doit valoir 0 |

Chaque compteur porte sa valeur, sa taille d'échantillon et **la commande
réellement exécutée** qui l'a produit. Un chiffre sans commande n'est pas
un compteur.

## Acceptable Waivers (if any claim of infeasibility arises)

| affirmation d'impossibilité | commande exigée | erreur exigée |
|---|---|---|
| « le coût en jetons de Codex n'est pas récupérable » (SC9) | la commande de lecture réellement tentée sur les transcripts Codex | son message d'erreur littéral, ou la sortie vide accompagnée du chemin inspecté |
| « l'API GitHub ne rend pas les vingt dernières PR fusionnées » (SC14) | l'appel `gh` réellement tenté | le corps de la réponse en erreur |
| « le wrapper ne peut pas être exécuté sur cette machine » (SC7, SC11) | la commande d'exécution tentée | le message d'erreur du shell, avec la version de l'interpréteur |

Aucune autre dérogation n'est recevable. En particulier, « je n'ai pas pu
faire la preuve red-first » n'est pas une dérogation : SC3 est
inatteignable sans elle, et un lot qui l'omet est incomplet, pas dérogé.

## Amendment Note (2026-08-11)

Une seule chose a été ajoutée à ce brief après sa première rédaction, et
avant tout travail de Générateur : **SC3b**, ses deux compteurs, et sa ligne
de rubrique.

Origine du changement, pour qu'il ne passe pas pour une intuition tardive :
en évaluant le lot 009b, l'Évaluateur a constaté que le gate n'avait
**rien vérifié de ce lot**. Le journal du brief 009 porte `forge-generateur`
en tête (lot 009a, Claude) et `forge-generateur-codex` plus bas (lot 009b,
Codex) ; le verdict porte de même deux auteurs. `read_field` s'appuyant sur
`re.search`, le contrôle a comparé le couple du premier lot et ignoré le
second. La preuve est dans la section « Évaluation — lot 009b » de
`harness/queue/briefs/009-full-auto-agent-invocation/verdict.md`.

C'est un second angle mort, indépendant de celui décrit en tête de ce brief
et qui se cumule avec lui : le premier laisse passer un acteur qui se juge
lui-même, le second laisse passer **tout lot autre que le premier**, quel
que soit son auteur. Corriger l'un sans l'autre laisserait la porte ouverte.
Aucune autre section n'a été modifiée.

## Execution Contract

**Qui produit, qui juge.** La règle ne change pas parce que le brief parle
d'elle : celui qui produit un lot n'écrit pas son verdict.

- **010a** modifie `harness/verdict_audit.py`, qui figure sur la denylist
  d'auto-fusion et que le prompt de passation Codex interdit à Codex de
  toucher. Deux conséquences à assumer, pas à contourner : ce lot est
  produit par **Claude**, et sa pull request ne sera **pas**
  auto-fusionnable. Son verdict est écrit par **Codex**, en session
  distincte.
- **010b** et **010c** sont produits par **Codex** et jugés par **Claude**.
  Si Claude est plafonné au moment du jugement, l'ADR de SC1 dit ce qui est
  alors permis — et rien d'autre.

**Ordre.** 010a d'abord ou 010c d'abord, indifféremment. 010b après
l'acceptation de 010a, parce que SC11 réutilise la fonction que SC3
introduit.

**Budget.** Première action de chaque lot :
`py harness/budget.py split-check --brief harness/queue/briefs/010-repartition-roles-full-auto --estimated-calls <N>`,
puis `py harness/budget.py status` pendant le travail. Seuils inchangés :
avertissement à 100 appels, checkpoint à 130, arrêt dur à 160. Au
checkpoint on écrit l'état et on s'arrête — on ne continue pas « puisque
c'est presque fini ».

**Fin de lot.** Le gate mécanique doit répondre ACCEPT
(`py harness/verdict_audit.py harness/queue/briefs/010-repartition-roles-full-auto`)
et la suite complète doit être verte (`py -m pytest harness/tests/ -q`),
avec les deux sorties réelles recopiées dans le journal — pas seulement
déposées dans un fichier annexe. C'est le défaut C1 relevé sur le lot 009a.
