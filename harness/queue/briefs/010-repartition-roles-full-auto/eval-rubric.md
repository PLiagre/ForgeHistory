# Eval Rubric — Brief 010 (issu de l'audit CURSOR-e9a6f4c-codex-passation-full-auto)

**Authored**: 2026-08-11T10:16:38
**Author**: forge-planificateur

Écrite avant tout travail du Générateur. Ne sera pas révisée après avoir vu
les livrables : une grille réécrite pour épouser ce qui a été livré ne juge
plus rien.

## Comment l'Évaluateur se sert de cette grille

Le gate mécanique est **nécessaire et non suffisant**. Il a déjà répondu
ACCEPT sur un lot que l'Évaluateur a ensuite rejeté à juste titre (lot 008a,
puis 009a deux fois). L'Évaluateur reconstruit **chaque** compteur par sa
propre commande. Un chiffre repris du manifeste sans être recalculé n'est
pas une vérification.

Règle de preuve inversée, propre à ce brief : ses conditions parlent de
**refus**. On ne prouve pas un refus en montrant qu'un test passe — on le
prouve en montrant que le test **échoue avant** le correctif. Toute
condition portant sur un refus et livrée sans sa sortie rouge datée est
comptée comme non satisfaite, quelle que soit la qualité du code.

## Lot 010a — le contrat des rôles

| SC | Ce qui est exigé | Ce qui rend la condition NON satisfaite |
|---|---|---|
| SC1 | ADR `0008-*.md` avec `Status` non vide, portant les quatre points (a) Codex peut évaluer, (b) session distincte déclenchée par un tiers, (c) option « sous-agent du Générateur » écartée avec sa raison, (d) le plafond de crédit comme fait déclencheur. Ligne ajoutée à `docs/adr/README.md`. | Un des quatre points absent, ou présent en paraphrase vague. Un `Status` vide. Une formulation qui autorise implicitement le sous-agent en n'en parlant pas. |
| SC2 | `harness-roles.md` modifié ; `test_single_source_of_instruction.py` vert. | La règle est recopiée dans l'ADR au lieu d'y être référencée. Le test est rouge. Le fichier de règle dit encore que l'Évaluateur est réservé à Claude. |
| SC3 | Le couple `forge-generateur-codex` / `forge-evaluateur-codex` est REFUSÉ, avec sortie rouge **avant** correctif recopiée, puis verte après. | Test écrit après le correctif. Sortie rouge absente, ou remplacée par une affirmation en prose. Refus obtenu par une liste en dur de deux noms. |
| SC3b | Tous les couples auteur du brief sont examinés, pas seulement le premier. Preuve red-first : un couple auto-jugé en **seconde** position passe inaperçu avant correctif, et est refusé après. Les deux sorties recopiées. | Le correctif se contente de lire le dernier auteur au lieu du premier — cela déplace l'angle mort au lieu de le fermer. Preuve faite uniquement en première position, ce qui ne teste rien de nouveau. Compteur `author_pairs_examined_per_brief` annoncé sans être reconstruit sur le brief 009 réel. |
| SC4 | Un acteur inédit (`forge-generateur-gemini` / `forge-evaluateur-gemini`) est refusé **sans modifier le contrôle**. | Le contrôle contient une énumération de backends. Le test réutilise `codex`, ce qui ne prouve pas la généralité. |
| SC5 | Gate exécuté sur **tous** les répertoires de brief avant et après ; aucun verdict ne passe de PASS à FAIL sur ce contrôle ; le compteur porte le nombre réel de répertoires comparés. | Un échantillon partiel présenté comme exhaustif. Un brief existant devenu rouge et « expliqué » plutôt que traité. Le dénominateur ne correspond pas au nombre réel de répertoires. |
| SC6 | Gate réel sur le brief 009 : le verdict signé `forge-evaluateur-codex` face au journal `forge-generateur` passe toujours. Sortie recopiée. | Le contrôle est devenu si strict qu'il refuse un jugement croisé légitime — c'est un échec, pas une prudence. |

**Disqualifiant pour 010a** : rendre le contrôle plus permissif, de quelque
manière que ce soit. Ce lot ne peut que resserrer.

## Lot 010b — Codex backend officiel

| SC | Ce qui est exigé | Ce qui rend la condition NON satisfaite |
|---|---|---|
| SC7 | `run_codex_generator.sh` conforme au contrat de `backends/README.md`, signature montrée face à celle du wrapper Cursor. | Interface divergente non justifiée. Wrapper qui n'a jamais été exécuté une seule fois. |
| SC8 | `forge-run.md` connaît `codex` aux trois emplacements, cités par ligne. | Un seul emplacement modifié : la commande annonce un backend qu'elle ne sait pas lancer. |
| SC9 | Ligne `codex` réellement présente dans la sortie de `ledger.py report`. | Compteur déclaré sans exécution. Coût annoncé « non applicable » sans passer par la table des dérogations. |
| SC10 | ADR `0009-*.md` avec `Status`, ligne ajoutée au README des ADR. | ADR sans statut, ou qui réécrit la décision du propriétaire au lieu de l'enregistrer. |
| SC11 | Le wrapper refuse le cas d'auto-jugement, en **réutilisant** la fonction de SC3. Appel réel, sortie et code de retour recopiés. | Logique de refus réimplémentée dans le wrapper : deux vérités qui divergeront. Refus prouvé par lecture de code plutôt que par exécution. |

**Disqualifiant pour 010b** : déclarer le backend sans le mesurer. C'est le
défaut déjà ouvert sur Cursor ; le reproduire sciemment sur Codex est un
rejet immédiat.

## Lot 010c — le verrou de fusion

| SC | Ce qui est exigé | Ce qui rend la condition NON satisfaite |
|---|---|---|
| SC12 | Le test **lit** `merge-bot.yml` et en extrait préfixes et chemins ; élargir la liste rend le test rouge. | Valeurs recopiées en constantes dans le test : il ne surveille alors plus rien. Test qui passerait sur un fichier vide ou tronqué — le défaut C3 du lot 009a, à ne pas répéter. |
| SC13 | Document court nommant l'étape humaine exacte qui subsiste. | Une phrase promettant un comportement qu'aucun workflow n'exécute — défaut C4 du lot 009a. |
| SC14 | Compteur sur les 20 dernières PR fusionnées, commande citée, sortie recopiée. | Estimation. Échantillon réduit sans le dire. |
| SC15 | Prédicats de la porte conditionnelle spécifiés un par un avec la preuve que chacun lit ; `git diff` vide sur `.github/workflows/`. | Le moindre octet modifié sous `.github/workflows/`. Une spécification qui décrit une porte déjà active. |

**Disqualifiant pour 010c** : toucher à un workflow. Le lot mesure et
spécifie ; il n'active pas.

## Contrôles de gate mécanique (les dix, rappelés)

`files_declared_exist`, `mtime_after_brief`, `captures_differ_when_should`,
`waivers_have_command_and_error`, `no_empty_sample_pass`,
`verdict_numbers_traceable`, `no_bare_python_alias`,
`verdict_is_not_self_authored`, `rubric_predates_deliverables`,
`declared_files_are_tracked`.

Note propre à ce brief : le lot 010a **modifie** le huitième de ces
contrôles. L'Évaluateur exécute donc le gate avec la version corrigée **et**
vérifie que le contrôle corrigé se juge lui-même correctement sur ce lot —
un contrôle qui s'exempterait de sa propre règle serait un échec.

## Échecs disqualifiants, tous lots confondus

1. Écrire le verdict d'un lot qu'on a produit soi-même, y compris via un
   sous-agent qu'on a lancé.
2. Affaiblir ou contourner un test pour le faire passer.
3. Recopier le texte du brief dans un autre fichier.
4. Produire un chiffre sans la commande qui l'a mesuré.
5. Annoncer dans un document un comportement qu'aucun code n'exécute.
6. Modifier `.github/workflows/`, `VISION.md`, ou le brief 009 et ses
   livrables.
7. Fabriquer un horodatage, un hash ou une sortie de commande.

## Ce qui vaut ACCEPT

Un lot est ACCEPTÉ quand **toutes** ses conditions sont satisfaites, que
chaque compteur a été reconstruit par l'Évaluateur avec ses propres
commandes, qu'une preuve red-first datée existe pour chaque condition
portant sur un refus, et qu'aucun échec disqualifiant n'est présent. Une
condition « satisfaite en substance » ne l'est pas.
