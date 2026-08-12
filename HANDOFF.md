# HANDOFF.md

## Session la plus récente — 2026-08-12 : évaluation indépendante + stabilisation

Claude a repris le projet comme **orchestrateur, Évaluateur indépendant et
intégrateur final**. Les trois lots produits par Codex/Claude ont été évalués
contre leurs Success Conditions originales, chacun par un acteur distinct de
son producteur, puis les trois ont été intégrés sur une branche de
stabilisation. Aucun lot n'a été accepté sur la seule foi de son propre
journal ; chaque compteur et chaque preuve ont été reconstruits par des
commandes de l'Évaluateur.

**Verdicts indépendants (tous ACCEPT)** :

| lot | verdict | reconstruit par l'Évaluateur |
|---|---|---|
| **009a** itération 3 (`999dcf3`+`3703d75`, prod. Codex) | **ACCEPT** | C1 fermé (sortie complète `300 passed` au journal) ; C2 (compteur transition = 2 sur `244a4f2~1..3703d75`) ; C3 (cas `commentaires-seuls` et `tronqué-avant-steps` désormais **refusés**, sondés directement ; cas `echo no-agent` restreint en limite documentée et testée) ; C4 (docs/config ne décrivent plus le mode comme coupe-circuit actif). Verdict `7c76c52`. |
| **010b** (`42679d7`, prod. Codex) | **ACCEPT** | SC7-SC11 : wrapper conforme et réellement exécuté ; `--backend codex` aux 3 emplacements de `forge-run.md` ; `2 codex` mesurés au ledger ; dérogation jetons recevable (commande tentée + `codex.exe: Permission denied`) ; refus d'auto-jugement **avant écriture**, réutilisant `verdict_audit.check_verdict_not_self_authored` (pas de réimplémentation). Verdict `d6f7cdb`. |
| **010c** (`df142e6`, prod. Codex) | **ACCEPT** | SC12-SC15 : le test lit réellement `merge-bot.yml` et refuse un fichier vide/tronqué ; doc nomme l'étape humaine sans surpromesse ; mesure honnête **5/18** (18 PR fusionnées réelles confirmées par `gh` ; dénominateur 20 **non** fabriqué) ; porte spécifiée non activée, diff `.github/workflows/` vide. Verdict `d6f7cdb`. |

Aucun lot rejeté. La séparation producteur/Évaluateur a été tenue : Codex a
produit 009a-itér.3, 010b et 010c ; Claude les a jugés.

## Point de départ pour la prochaine session

- Branche par défaut : `master`. La stabilisation `forge/stabilisation-2026-08-12`
  (merges `1b860b5` 009a, `6aff527` 010b, `7e06660` 010c, + verdicts `7c76c52`
  et `d6f7cdb`) intègre les trois lots au-dessus de `3822c68` (PR #21, 010a).
- Jalon général : F0 terminé ; F1 en cours. Unity, le pipeline géographique et
  les travaux visuels n'ont pas été touchés cette session.
- Validation finale rejouée sur l'état intégré :
  - `py -m pytest harness/tests/ -q` → **321 passed**, zéro échec.
  - `py harness/verdict_audit.py harness/queue/briefs/009-full-auto-agent-invocation` → **VERDICT: ACCEPT**.
  - `py harness/verdict_audit.py harness/queue/briefs/010-repartition-roles-full-auto` → **VERDICT: ACCEPT**.
  - `py harness/audit_schema.py` → les 7 audits sont valides.
  - `py harness/harness_audit.py` → **23/24**. Le seul rouge,
    `no_premature_stub_content`, est l'**outil qui est périmé** : il croit
    encore que `pipeline/geo/` est un stub vide alors que des lots F1 acceptés
    l'ont rempli. Ce rouge préexiste et n'est pas une régression de cette
    intégration (aucun des trois lots ne touche `pipeline/geo/`). **Ne pas
    vider `pipeline/geo/` pour le faire passer.**
  - `git diff --check` propre ; arbre propre.

## État des briefs

| brief / lot | état | preuve / blocage |
|---|---|---|
| 009a — séparation du mode | **ACCEPTÉ (itération 3)** | Verdict indépendant `7c76c52`. C1-C4 fermés et reconstruits. |
| 009b — plafond budgétaire CI | **ACCEPTÉ** | Verdict `ba035b1`. |
| 009c — invocation réelle de challenge | **débloqué, non produit** | Ses **deux** conditions sont désormais levées (009a ET 009b acceptés). Reste à produire dans un futur lot/brief. |
| 010a — contrat des rôles | **ACCEPTÉ (itération 2)** | Verdict `192218a`. |
| 010b — Codex backend officiel | **ACCEPTÉ** | Verdict `d6f7cdb`. |
| 010c — verrou de fusion | **ACCEPTÉ** | Verdict `d6f7cdb`. |

Le brief **010 est complet sur ses trois lots**. Le brief 009 est complet sur
009a+009b ; 009c reste à produire.

## Prochaine action recommandée (une seule)

**Produire le lot 009c** (invocation réelle de `claude-challenger`,
mode-gated + budget-gated), maintenant que 009a et 009b sont tous deux
acceptés — c'est la seule dépendance qui le bloquait. Avant de le câbler,
trancher l'arbitrage `--max-budget-usd` (voir plus bas, point 2). Un agent
sans brief n'a pas d'instruction : la source reste le brief 009, Success
Conditions SC14-SC21.

Ensuite seulement, une passe Planificateur écrit le brief du maillon
`cursor-auditor` (`pipeline-audit.yml`, décidé en premier), puis
`pipeline-forge-run.yml`, puis le contrat d'écriture d'Hermes.

## La décision du propriétaire est enregistrée (2026-08-11)

L'audit de passation `CURSOR-e9a6f4c-codex-passation-full-auto` était
`PROPOSED` et **absent du ledger**. Sa boucle est désormais close :
`AUDIT_CHALLENGED (claude) → AUDIT_APPROVED (owner) → AUDIT_CONVERTED (owner)`.

Répartition arrêtée, à ne pas re-débattre sans nouvelle décision :

| acteur | rôle | écrit | n'écrit jamais |
|---|---|---|---|
| **Codex** | Développeur ; **et** Évaluateur de substitution quand Claude est à son plafond de crédit | code, tests, `deliverables/` du lot qu'il produit | le verdict d'un lot qu'il a produit |
| **Cursor** | Auditeur externe de **chaque** pull request | `architecture/inbox/**` | code, CI, briefs |
| **Hermes** | Observateur : briefs de suivi et tableaux de bord | rien dans le dépôt à ce jour (lecture seule) | code, CI, briefs |
| **Claude** | Planificateur et Évaluateur par défaut | briefs, rubriques, verdicts | le verdict d'un lot qu'il a produit |

La substitution retenue est l'**option B** : session distincte déclenchée par
un tiers (la CI ou le propriétaire), jamais par la session qui a produit le
lot. L'option « sous-agent d'évaluation engendré par le Générateur » est
écartée — le producteur cadrerait son juge.

## Les quatre arbitrages restants sont tranchés (2026-08-11)

Enregistrés en toutes lettres à la fin de
`architecture/decisions/DECISION-CURSOR-e9a6f4c-codex-passation-full-auto.md`.
Ne pas les paraphraser dans un brief : un brief les lit là-bas.

1. **Verrou de fusion → porte conditionnelle.** L'auto-fusion exige quatre
   preuves réunies : CI verte, gate ACCEPT, verdict d'un Évaluateur dont
   l'acteur diffère du producteur, et audit Cursor déposé. Le clic est
   remplacé par des conditions vérifiables, pas supprimé. **Le lot 010c a
   spécifié cette porte (doc `docs/rules/conditional-merge-gate.md`) sans
   l'activer.**
2. **Budget → plafond natif ET marquage.** `--max-budget-usd 5` sur l'appel
   headless (coupe avant la dépense) plus le marquage post-hoc du lot 009b
   (garde la trace). Les deux. À trancher/câbler dans 009c.
3. **Câblage → `cursor-auditor` d'abord.** `pipeline-audit.yml` avant
   `pipeline-forge-run.yml`. C'est aussi un prérequis du point 1, qui exige
   un audit Cursor déposé.
4. **Hermes → contrat d'écriture dans le dépôt.** Dossier dédié, versionné,
   format imposé, auteur traçable. Il reste observateur : un rapport est une
   entrée, jamais une instruction.

## Troisième angle mort, connu et non couvert

Le lot 010a a fermé les deux angles morts que le brief 010 lui demandait :
`verdict_is_not_self_authored` compare désormais des **acteurs** (pas des
rôles) et examine **tous** les couples d'un brief multi-lots (pas seulement le
premier). Il en reste un **troisième**, que ce brief n'avait pas demandé et
qu'il ne faut donc pas croire fermé :

| couple d'auteurs | contrôle |
|---|---|
| `forge-generateur` / `forge-evaluateur` | **accepté** — le trou |
| `forge-generateur-codex` / `forge-evaluateur-codex` | refusé |
| `forge-generateur` / `forge-evaluateur-codex` | accepté (légitime) |
| `forge-generateur-codex` / `forge-evaluateur` | accepté (légitime) |

Le backend natif s'écrit en rôles nus, sans suffixe d'acteur : le gate ne peut
pas détecter que **Claude a produit et jugé le même lot**. La séparation sur un
lot natif ne repose donc sur aucune mécanique, seulement sur la discipline et
sur la grille écrite avant le travail. Piste (à ne pas improviser) : faire
porter à l'auteur son acteur explicite (`forge-generateur-claude`), en migrant
les journaux existants sans invalider les verdicts déjà rendus — la contrainte
de non-régression que SC5 impose.

Quatre autres évasions (`R1`-`R4`) ont été trouvées par l'Évaluateur de 010a et
consignées dans le verdict du lot 010a. Aucune n'est une régression ; elles
méritent le même brief futur que le cas natif.

## Full automatisation : ne pas surannoncer

Les trois stubs sont toujours là :

```text
.github/workflows/pipeline-audit.yml       TODO(operator...)
.github/workflows/pipeline-challenge.yml   TODO(operator...)
.github/workflows/pipeline-forge-run.yml   TODO(operator...)
```

Fournir les secrets aujourd'hui ne déclencherait aucun appel d'agent : le code
qui les utiliserait n'existe pas encore. Aucun de ces trois workflows n'a été
touché par 009a, 010b ni 010c. Hermes reste en lecture seule.

## Risques connus

- **Le backend Codex n'est pas exécutable sur cette machine.** L'installation
  AppX refuse `codex.exe` (`Permission denied`, code interne `126`), donc le
  transcript JSONL des invocations est vide et le coût en jetons n'est pas
  récupérable. La dérogation SC9 de 010b est recevable **telle quelle** ; ne
  pas convertir cet échec en succès fictif. Pour mesurer un vrai coût jeton, le
  propriétaire doit fournir une installation CLI autonome exécutable, puis le
  wrapper est rejoué.
- **Un red-first lancé depuis la racine du dépôt ne prouve rien.** Sabotant une
  copie jetable mais exécutant `pytest` depuis `D:\ForgeHistory`, les tests
  importent le module **intact** du dépôt. Toujours exécuter depuis la copie.
- **Nombres dans un `verdict.md`.** Le contrôle `verdict_numbers_traceable`
  exige que tout nombre cité en prose trace à un compteur du manifeste — sinon
  il faut l'entourer de backticks (code span). Convention du dépôt à respecter
  en écrivant un verdict.
- Un ledger de budget CI absent ou vide vaut « budget remis à zéro » (non
  bloquant, consigné dans le verdict 009b).
- `budget.py split-check` rapporte 0 condition de succès sur un brief dont les
  conditions sont groupées sous des sous-titres `###` (extracteur limité) ; ne
  pas remodeler un brief pour plaire au détecteur.
- Ne jamais fabriquer de contenu VictoriaProject au-delà de ce qui a été lu.
- Les 7 rouges hérités du portage Unity restent rouges-et-attribués.
- Les Générateurs ne committent jamais.
- Pour Unity, passer par `unity/run-unity.ps1` (un seul processus, rend la main
  une fois).

## Résumé de la session (2026-08-12)

1. **Trois évaluations indépendantes**, chacune reconstruite par des commandes
   de l'Évaluateur, jamais sur la foi du journal du lot : 009a itér.3 (C1-C4),
   010b (SC7-SC11), 010c (SC12-SC15). Toutes **ACCEPT**.
2. **Intégration** sur `forge/stabilisation-2026-08-12` depuis `origin/master`,
   dans l'ordre 009a → 010b → 010c. Conflits `HANDOFF.md`, generator-log et
   manifest du brief 010 résolus **en conservant le contenu des deux lots**
   (fichiers, compteurs, dérogations, auteurs) — jamais un simple « ours »/
   « theirs ».
3. **Validation finale rejouée** : 321 tests verts, gates 009 et 010 ACCEPT,
   7 audits valides, harness 23/24 (rouge geo périmé), arbres propres.
4. Verdicts et feedbacks conservés en append-only ; aucun code fonctionnel de
   lot évalué n'a été modifié par l'Évaluateur.
