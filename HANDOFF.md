# HANDOFF.md

## Mise à jour Codex la plus récente — 2026-08-11

Codex a repris la session et poursuit le projet sans modifier les verdicts ni
committer le travail des Générateurs.

- `D:\ForgeHistory`, branche `forge/009a-iteration-3` : correction 009a
  itération 3 produite, 300 tests verts, gate 10/10 ; prête pour évaluation
  indépendante par Claude.
- `D:\ForgeHistory-010b`, branche `codex/010b-codex-backend` : lot 010b
  produit, 311 tests verts. Le wrapper Codex, le préflight anti-auto-jugement,
  le ledger, ADR-0009 et les preuves SC7–SC11 sont présents. Deux appels réels
  sont comptés ; l'installation AppX locale refuse toutefois l'exécution de
  son `codex.exe` (`Permission denied`, code interne 126), donc aucun coût jeton
  n'est inventé. Lot prêt pour évaluation indépendante par Claude après staging.
- `D:\ForgeHistory-010c`, branche `codex/010c-merge-lock` : lot 010c produit
  et stagé, 311 tests verts, gate mécanique 10/10. Le dépôt GitHub ne possède
  que 18 PR fusionnées : la mesure honnête est 5/18, pas un dénominateur 20
  fabriqué. Prêt pour évaluation indépendante.

Pour 010b, la référence CLI utilisée est l'interface officielle
`codex exec` : https://developers.openai.com/codex/cli/reference/. Le fixture
inter-acteurs et les sorties brutes sont sous
`harness/queue/briefs/010-repartition-roles-full-auto/deliverables/proofs/`.
Le prochain acteur ne doit pas convertir l'échec AppX en succès fictif : soit
il évalue la dérogation SC9 telle quelle, soit le propriétaire fournit une
installation CLI autonome exécutable et le wrapper est rejoué.

État de reprise vérifié le 2026-08-11. Ce fichier décrit l'état réel utile à
la prochaine session ; l'historique détaillé reste dans Git.

## Point de départ

- Branche par défaut : `master`, commit `89219cc` (PR #16, #17, #18 de Codex
  fusionnées). Travail de cette session sur `forge/roles-full-auto`, quatre
  commits, **non fusionnés**.
- Jalon général : F0 terminé ; F1 en cours. Le jeu Unity porté, le pipeline
  géographique et les travaux visuels n'ont pas été touchés cette session.
- `py -m pytest harness/tests/ -q` → **296 passed**, zéro échec.
- `py harness/verdict_audit.py harness/queue/briefs/009-full-auto-agent-invocation`
  → **10/10, VERDICT: ACCEPT**.
- `py harness/harness_audit.py` → **23/24**. Le seul rouge,
  `no_premature_stub_content`, est l'outil qui est périmé : il croit encore
  que `pipeline/geo/` est un stub vide alors que des lots acceptés l'ont
  rempli. Ne pas vider le répertoire pour satisfaire l'audit.
- `py harness/audit_schema.py` → les sept audits sont valides.

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
   remplacé par des conditions vérifiables, pas supprimé.
2. **Budget → plafond natif ET marquage.** `--max-budget-usd 5` sur l'appel
   headless (coupe avant la dépense) plus le marquage post-hoc du lot 009b
   (garde la trace). Les deux.
3. **Câblage → `cursor-auditor` d'abord.** `pipeline-audit.yml` avant
   `pipeline-forge-run.yml`. C'est aussi un prérequis du point 1, qui exige
   un audit Cursor déposé.
4. **Hermes → contrat d'écriture dans le dépôt.** Dossier dédié, versionné,
   format imposé, auteur traçable. Il reste observateur : un rapport est une
   entrée, jamais une instruction.

## Deux trous dans le seul contrôle qui protège vraiment le dépôt

`verdict_is_not_self_authored` (`harness/verdict_audit.py:262-268`) est la
barrière mécanique contre l'auto-jugement. Elle a deux angles morts, trouvés
cette session, indépendants l'un de l'autre et qui se cumulent :

1. **Elle compare des rôles, pas des acteurs.** Le code est
   `gen != ver`, une simple inégalité de chaînes. Avec un seul backend,
   `forge-generateur` ≠ `forge-evaluateur` suffisait. Avec deux,
   `forge-generateur-codex` ≠ `forge-evaluateur-codex` **passe aussi** : le
   même acteur peut produire et juger, et le gate affiche `[PASS]`.
2. **Elle ne lit que le premier auteur de chaque fichier.** `read_field`
   utilise `re.search`, qui rend la première occurrence. Sur le brief 009,
   `generator-log.md` porte `forge-generateur` en tête (lot 009a, Claude) et
   `forge-generateur-codex` plus bas (lot 009b, Codex) : le gate a comparé le
   couple du lot 009a et **n'a rien vérifié du lot 009b**. Sur un brief
   multi-lots, seul le premier lot est contrôlé.

Le contrôle ne ment pas : il n'a jamais su distinguer un acteur d'un rôle, ni
un lot d'un autre. Cela devient porteur exactement dans le cas que la
décision du propriétaire vise — Claude plafonné, Codex seul de bout en bout.

## Le verrou qui bloque réellement le zéro-intervention

Ce n'est pas le câblage des agents, c'est la fusion.
`.github/workflows/merge-bot.yml` n'auto-fusionne que les branches préfixées
`cursor/` ou `forge-bot/`, et seulement si **tous** les chemins modifiés
tombent dans `architecture/inbox/`, `architecture/reviews/` ou
`harness/queue/briefs/*/feedback/`. Donc :

- une branche `codex/` n'est **jamais** auto-fusionnée ;
- **aucune pull request de code** ne l'est, même sur une branche autorisée.

Les trois maillons câblés demain, la boucle s'arrêterait encore au clic du
propriétaire. La denylist n'a pas été élargie : la protection de branche est
indisponible sur ce plan GitHub (`HTTP 403`, vérifié), donc cette liste est
la **seule** barrière réelle. Le lot 010c la mesure et spécifie la porte
conditionnelle qui remplacerait le clic, sans l'activer.

## État des briefs

| brief / lot | état | preuve / blocage |
|---|---|---|
| 009a — séparation du mode | **REJETÉ, itération 2** | Rejugé par Codex (`c9e9291`). Quatre défauts C1-C4 dans `feedback/feedback-009a-002.md`. Le plus sérieux est C3 : le garde accepte encore trois faux workflows malgré sa promesse de « preuve positive ». |
| 009b — plafond budgétaire CI | **ACCEPTÉ** | Verdict Claude ajouté à `verdict.md` (`ba035b1`). SC8 à SC13 reconstruites indépendamment, red-first rejoué depuis une copie jetable. Trois constats non bloquants y sont consignés. |
| 009c — invocation réelle de challenge | **bloqué** | Une de ses deux conditions est levée (009b accepté) ; l'autre non (009a rejeté). Ne pas démarrer. |
| 010a — contrat des rôles | **ACCEPTÉ à l'itération 2**, après un vrai cycle REJECT → correction → ACCEPT | Itération 1 (`62a0fe2`) **rejetée** : elle rendait le contrôle *plus permissif* qu'avant — ajouter au journal un lot non encore jugé poussait un couple auto-jugé hors de la fenêtre des `k` derniers auteurs, et le refus disparaissait. Itération 2 (`e912d61`) referme la porte par deux ajouts ; verdict `192218a`. Ce qui a décidé : une énumération **exhaustive** plutôt qu'un échantillon — 66 564 combinaisons de listes d'auteurs, 0 cas refusé-avant/accepté-après, 39 585 acceptés-avant/refusés-après. Le contrôle refuse un sur-ensemble strict. 305 tests. |
| 010b — Codex backend officiel | **spécifié, non produit** | Attend 010a. Produit par Codex, jugé par Claude. |
| 010c — verrou de fusion | **spécifié, non produit** | Indépendant. Produit par Codex, jugé par Claude. |

## Prochaines actions, dans l'ordre

1. **Faire juger 010a par Codex** (PR #20). Claude l'a produit, il ne peut pas
   le juger — et c'est désormais le contrôle corrigé lui-même qui le dirait.
2. **Faire corriger 009a (C1-C4) par Codex**, qui devient ici le Générateur —
   il n'a pas produit l'itération 2, il l'a jugée. Claude jugera l'itération 3.
   Source d'instruction : le brief 009 ; défauts à traiter :
   `feedback/feedback-009a-002.md`.
3. **Produire 010c** (Codex), indépendant de tout le reste.
4. **Produire 010b** (Codex) une fois 010a accepté.
5. Après 009c, une passe Planificateur écrit le brief du maillon
   `cursor-auditor` (`pipeline-audit.yml`) — décidé en premier — puis celui de
   `pipeline-forge-run.yml`, puis le contrat d'écriture d'Hermes. Un agent
   sans brief n'a pas d'instruction : ne rien câbler avant.

## Full automatisation : ne pas surannoncer

Les trois stubs sont toujours là sur `master` :

```text
.github/workflows/pipeline-audit.yml       TODO(operator...)
.github/workflows/pipeline-challenge.yml   TODO(operator...)
.github/workflows/pipeline-forge-run.yml   TODO(operator...)
```

Fournir les secrets aujourd'hui ne déclencherait aucun appel d'agent : le code
qui les utiliserait n'existe pas encore. Hermes reste en lecture seule.

## Troisième angle mort, connu et non couvert (2026-08-11)

Le lot 010a ferme les deux angles morts que le brief 010 lui demandait de
fermer. Il en reste un **troisième**, que ce brief n'avait pas demandé et
qu'il ne faut donc pas croire fermé :

| couple d'auteurs | contrôle |
|---|---|
| `forge-generateur` / `forge-evaluateur` | **accepté** — le trou |
| `forge-generateur-codex` / `forge-evaluateur-codex` | refusé |
| `forge-generateur` / `forge-evaluateur-codex` | accepté (légitime) |
| `forge-generateur-codex` / `forge-evaluateur` | accepté (légitime) |

Le backend natif s'écrit en rôles nus, sans suffixe d'acteur : `_actor_suffix`
rend `None` des deux côtés et il n'y a rien à comparer. Autrement dit, **le
gate ne peut pas détecter que Claude a produit et jugé le même lot** — le cas
le plus fréquent, justement.

Conséquence immédiate et concrète : la séparation des rôles sur le lot 010a
ne repose sur aucune mécanique, seulement sur la discipline. C'est pourquoi
son verdict doit venir de Codex, et pourquoi ce n'est pas une formalité.

Piste de correction pour un brief futur, à ne pas improviser : faire porter à
l'auteur son acteur explicite (`forge-generateur-claude` plutôt que
`forge-generateur`), ce qui suppose de migrer les journaux existants sans
invalider les verdicts déjà rendus — exactement la contrainte de
non-régression que SC5 impose.

Quatre autres évasions ont été trouvées par l'Évaluateur en cherchant
activement, et consignées `R1` à `R4` dans le verdict du lot 010a. Aucune
n'était exigée par le brief, aucune n'est une régression, et elles méritent
le même brief futur que le cas natif :

- **R1** — le test de SC4 vérifie qu'un nom d'acteur inventé est absent de
  tout le dépôt. Il rougit donc si quelqu'un *documente* ce nom hors des
  fichiers exemptés. Un test qui casse parce qu'on parle de lui.
- **R2** — évasion par la casse : `-Morrigan` et `-morrigan` sont vus comme
  deux acteurs.
- **R3** — évasion par le rôle : un verdict signé
  `forge-planificateur-<acteur>` échappe au contrôle, qui ne reconnaît que
  les préfixes générateur et évaluateur.
- **R4** — auto-jugement désaligné à listes de longueur égale. Seule
  l'intersection **par acteur** le fermerait, et cette règle est écartée à
  juste titre : elle refuserait le brief 009 et violerait SC6.

## Risques connus

- **Les hooks du dépôt ont bloqué une session entière cette fois-ci.** Ils
  étaient câblés en chemin relatif ; un `cd` dans un dossier de brief a suffi
  à rendre Bash, Edit et Write inutilisables, sous-agents compris. Corrigé
  par `$CLAUDE_PROJECT_DIR` (`43c2a1b`) et verrouillé par deux tests dans
  `test_hooks_armed.py`. Leçon générale : un garde câblé en chemin relatif ne
  protège que par coïncidence.
- **Un red-first lancé depuis la racine du dépôt ne prouve rien.** Sabotant
  une copie jetable mais exécutant `pytest` depuis `D:\ForgeHistory`, les
  tests importaient le module **intact** du dépôt et restaient verts. Toujours
  exécuter depuis la copie (`cd <copie> && py -m pytest ...`).
- Un ledger de budget CI absent ou vide vaut « budget remis à zéro » ; le
  ledger livré est exactement dans cet état. Non bloquant, consigné dans le
  verdict 009b.
- `budget.py split-check` rapporte 0 condition de succès sur un brief dont
  les conditions sont groupées sous des sous-titres `###` — son extracteur
  s'arrête au premier titre rencontré. Constaté sur le brief 010, non
  contourné : le brief n'a pas été remodelé pour plaire au détecteur.
- Ne jamais fabriquer de contenu VictoriaProject au-delà de ce qui a été lu ;
  ce dépôt y est en lecture seule.
- Les 7 rouges hérités du portage Unity restent rouges-et-attribués. Ne pas
  les « réparer » à la légère.
- Les Générateurs ne committent jamais.
- Pour Unity, passer par `unity/run-unity.ps1` : il attend dans un seul
  processus et rend la main une fois. Ne jamais relire un log Unity d'un
  appel d'outil à l'autre.

## Résumé de la session (2026-08-11)

Quatre commits sur `forge/roles-full-auto`, rien de poussé sur `master`.

1. **La décision du propriétaire sur les rôles est entrée dans le dépôt**
   (`7ea3171`) : contre-audit (11 CONFIRMED, 2 PARTIAL, 6 NEEDS_OWNER) puis
   décision puis conversion. Trois délimitations posées contre l'audit
   d'origine : son `20/24` ne se reproduit pas (`23/24` ici, le second rouge
   était un artefact de conteneur), sa section centrale est dépassée par
   `c9e9291`, et son « techniquement, oui » repose sur des affirmations
   produit invérifiables depuis ce dépôt. Le contre-audit ajoute le verrou de
   fusion, que l'audit ne voyait pas.
2. **Brief 010 écrit** (`b5cf3fc`), trois lots, autour du trou d'auto-jugement
   multi-backend.
3. **Hooks réparés** (`43c2a1b`) après qu'ils ont bloqué la session, avec la
   preuve dans les deux sens : une commande ordinaire passe depuis le
   sous-dossier fautif, et `python --version` y est toujours refusé.
4. **Lot 009b ACCEPTÉ** (`ba035b1`), SC8 à SC13 reconstruites par des
   commandes propres, red-first rejoué après invalidation de mon premier
   protocole, trois constats non bloquants consignés.
