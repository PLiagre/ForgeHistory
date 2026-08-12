# HANDOFF.md

État de reprise vérifié le 2026-08-11. Ce fichier décrit l'état réel utile à
la prochaine session ; l'historique détaillé reste dans Git.

## Point de départ

- Branche par défaut : `master`, commit `304c59a` (PR #19 fusionnée, donc les
  quatre commits de `forge/roles-full-auto` sont désormais sur `master`).
  Reprise active sur `forge/009a-iteration-3`, au commit partiel `999dcf3`,
  avec les corrections finales de l'itération 3 encore non commitées.
- Jalon général : F0 terminé ; F1 en cours. Le jeu Unity porté, le pipeline
  géographique et les travaux visuels n'ont pas été touchés cette session.
- `py -m pytest harness/tests/ -q` → **300 passed**, zéro échec.
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

**Cette décision n'est pas encore applicable sans perte de garantie**, et
c'est l'objet du brief 010 ci-dessous.

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
| 009a — séparation du mode | **ITÉRATION 3 PRODUITE, À RÉÉVALUER** | Codex a repris le commit partiel `999dcf3` comme Générateur et fermé C1-C4. Suite complète : 300 tests. Gate mécanique : 10/10. Le Générateur ne prononce pas le verdict ; Claude doit reconstruire les preuves dans une session distincte. |
| 009b — plafond budgétaire CI | **ACCEPTÉ** | Verdict Claude ajouté à `verdict.md` (`ba035b1`). SC8 à SC13 reconstruites indépendamment, red-first rejoué depuis une copie jetable. Trois constats non bloquants y sont consignés. |
| 009c — invocation réelle de challenge | **bloqué en attente du verdict 009a** | 009b est accepté et 009a est produit, mais la dépendance n'est levée qu'après une réévaluation indépendante de 009a. Ne pas démarrer avant ce verdict. |
| 010a — contrat des rôles | **spécifié, non produit** | Corrige les deux trous ci-dessus. Doit être produit par **Claude** : il touche `verdict_audit.py`, que le prompt de passation interdit à Codex. Jugé par Codex. |
| 010b — Codex backend officiel | **spécifié, non produit** | Attend 010a. Produit par Codex, jugé par Claude. |
| 010c — verrou de fusion | **spécifié, non produit** | Indépendant. Produit par Codex, jugé par Claude. |

## Prochaines actions, dans l'ordre

1. **Faire réévaluer 009a itération 3 par Claude**, dans une session distincte
   de celle qui l'a produit. Rejouer les compteurs, les tests ciblés et la
   suite complète ; ne pas reprendre le `VERDICT: ACCEPT` mécanique comme un
   jugement humain.
2. **Produire 010a** (Claude), puis le faire juger par Codex.
3. **Produire 010c** (Codex), indépendant, en parallèle et sur une branche
   distincte pour ne pas mélanger ses changements avec 009a.
4. **Arbitrer `--max-budget-usd`** avant 009c : `claude --help` expose
   désormais un plafond USD natif, ce que la planification croyait inexistant.
   Décider explicitement si l'appel headless l'utilise aussi.
5. **Arbitrer le verrou de fusion** une fois 010c livré : le propriétaire
   décide si la porte conditionnelle remplace son clic.
6. Après 009c seulement, une passe Planificateur écrira les briefs des deux
   maillons restants (`pipeline-audit.yml`, `pipeline-forge-run.yml`). Un
   agent sans brief n'a pas d'instruction : ne pas les câbler avant.

## Full automatisation : ne pas surannoncer

Les trois stubs sont toujours là sur `master` :

```text
.github/workflows/pipeline-audit.yml       TODO(operator...)
.github/workflows/pipeline-challenge.yml   TODO(operator...)
.github/workflows/pipeline-forge-run.yml   TODO(operator...)
```

Fournir les secrets aujourd'hui ne déclencherait aucun appel d'agent : le code
qui les utiliserait n'existe pas encore. Hermes reste en lecture seule.

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
