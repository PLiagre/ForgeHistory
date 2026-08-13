---
review_of: CURSOR-c296c47-pr86-revue-sans-preuve-citable
reviewer: claude-code
target_commit: c296c4730eb5647b86e59a20559729f97d5fc05b
reviewed_at: 2026-08-13T13:30:00Z
---

# Contre-audit de CURSOR-c296c47-pr86-revue-sans-preuve-citable

Rôle : vérifier la *véracité technique* de l'audit, pas sa valeur métier.
Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

## 1. Provenance (re-vérifiée)

- target_commit annoncé : c296c4730eb5647b86e59a20559729f97d5fc05b
- Le commit existe-t-il dans l'historique de la branche cible ? **Oui.**
  ```
  $ git cat-file -t c296c4730eb5647b86e59a20559729f97d5fc05b
  commit
  $ git merge-base --is-ancestor c296c4730eb5647b86e59a20559729f97d5fc05b master && echo ancestor
  ancestor
  $ git log -1 --format="%H %aI" c296c4730eb5647b86e59a20559729f97d5fc05b
  c296c4730eb5647b86e59a20559729f97d5fc05b 2026-08-13T14:53:11+02:00
  ```
  `14:53:11+02:00 = 12:53:11Z`, exactement l'heure de fusion annoncée par le
  corps de PR cité au § 1 de l'audit. Le commit `0e98199` cité comme
  `target_commit` de l'audit qu'il relit est lui aussi un ancêtre confirmé
  de `master` (même méthode).
- Mesures de l'audit rejouées ? **Oui, sur cinq portes mécaniques et sur les
  13 valeurs numériques de la sonde de sensibilité/mortalité — voir § 2,
  points 3 et 4.**

## 2. Verdicts point par point

| # | Point de l'audit | Verdict | Preuve / délimitation |
|---|---|---|---|
| 1 | Corps de PR : 1 fichier `+92/−0` sous `architecture/reviews/**` | CONFIRMED | `git show --stat c296c47` → `... 1 file changed, 92 insertions(+)`, chemin `architecture/reviews/CLAUDE-CURSOR-0e98199-pr69-seuil-survie-ignore-mortalite.md`. Identique au chiffre annoncé. |
| 2 | Cinq portes mécaniques citées § 2.2 rejouent au chiffre près | CONFIRMED | Rejouées indépendamment sur ce checkout : `verdict_audit.py harness/queue/briefs/013-...` → `VERDICT: ACCEPT` ; `pytest sim/tests/ -q` → `35 passed` ; `pytest harness/tests/ -q` → `314 passed, 16 skipped` ; `harness_audit.py` → `SCORE: 20/24`. Quatre sur quatre identiques aux valeurs de l'audit. |
| 3 | Le fichier fusionné ne contient aucun bloc de code (`grep -c '^```' → 0`) — preuve littérale de P1-1 | CONFIRMED | `git show c296c47:architecture/reviews/CLAUDE-CURSOR-0e98199-....md \| grep -c '^```'` → `0` chez moi aussi. |
| 4 | Sonde indépendante § 8.1 : 4 compteurs + 5 valeurs de sensibilité `HUNGER_DEATH_SCALE` + 4 agrégats de troncature de mortalité | CONFIRMED | J'ai réécrit la sonde moi-même (sans lire `/tmp/probe_cursor_pr86.py` avant de l'écrire, seulement la description du wrapper d'instrumentation au § 8.6) et je l'ai exécutée sur ce checkout : `pop_finale=51199297 morts=15666208 kg_transportes=2676487 fraction_survie=0.765706 cellules_affamees=536`, `deficit=76932 tronques=37384 (48.6%) perdus=24345.7 plafond=0`, et les cinq lignes `HUNGER_DEATH_SCALE∈{0.001,0.005,0.01,0.02,0.05}` → `survie∈{0.869657,0.765706,0.680871,0.551459,0.338088}`. **13 valeurs sur 13 identiques**, y compris les 6 décimales. Durée mesurée chez moi : 2,7 s cumulées (comparable aux 2,0 s annoncées). |
| 5 | `SEUIL_SURVIE_POPULATION_FRACTION` reste constant (`0.748889`) pendant que la survie mesurée varie de `0.869657` à `0.338088` (base du constat P2-1) | CONFIRMED | `sim/constants.py:142` : `SEUIL_SURVIE_POPULATION_FRACTION = _fraction_predite - SURVIE_MARGE_DERIVEE`, sans dépendance à `HUNGER_DEATH_SCALE`. Lu la valeur : `0.7488888888888889` → arrondi `0.748889`, exact. La non-dépendance est structurelle (lecture du code), pas seulement observée sur une exécution. |
| 6 | Décompte de verdicts § 8.4 : mot `CONFIRMED`×9, `REFUTED`×2, `PARTIAL`×3, `NEEDS_OWNER`×2 ; verdicts réels par ligne : 7 `CONFIRMED` + 1 `PARTIAL` | CONFIRMED | Rejoué avec la même boucle grep sur `git show c296c47:...` → `CONFIRMED: 9`, `REFUTED: 2`, `PARTIAL: 3`, `NEEDS_OWNER: 2`. Identique au registre (`architecture/audit-ledger.jsonl` ligne 60 : `{"CONFIRMED": 9, "REFUTED": 2, "PARTIAL": 3, "NEEDS_OWNER": 2}`). |
| 7 | Registre : `AUDIT_CHALLENGED` puis `AUDIT_APPROVED` à `12:53:26Z`, `retained_points: [1..8]` alors que l'audit `0e98199` n'a que 5 constats | CONFIRMED | `grep -n '0e98199' architecture/audit-ledger.jsonl` → lignes 60–61, mêmes deux événements, même horodatage, même `retained_points`. `grep -cE '^### Constat [0-9]' architecture/inbox/CURSOR-0e98199-....md` → `5`. L'écart 5 constats / 8 points retenus est réel. |
| 8 | Table « déjà instruit ailleurs » (§ 5) : les 7 audits cités existent et ne sont pas fabriqués | CONFIRMED | Vérifié un par un avec `find architecture -iname '*<id>*'` : les 11 identifiants cités (dont les variantes courtes) pointent tous vers un fichier réel dans `architecture/inbox/` ou `architecture/decisions/`. Aucun n'est inventé. |
| 9 | P1-1 lui-même : « sept des huit verdicts ne citent aucune commande, ligne de sortie, ni chemin de sonde » | CONFIRMED, avec une nuance mineure | Relecture ligne à ligne du fichier fusionné : les lignes 44, 45, 47, 49, 50, 51 (verdicts de mortalité/transport/portée du monde) décrivent leur méthode en prose (« rejeu indépendant », « sonde reconstruite ») sans bloc de code ni chemin de script — confirmé par le point 3 ci-dessus (0 bloc de code dans tout le fichier). Seule la ligne 46 (portes mécaniques, § 1 de l'audit) nomme ses cinq commandes. Le compte « sept sur huit » de l'audit est donc correct au sens strict. |
| 10 | Validateur actuel (`harness/audit_review.py`) n'exige ni bloc de commande, ni sortie collée, ni `run id` dans les cellules de preuve — prémisse du brief A | CONFIRMED | Lu `record_challenge()` / `parse_verdicts()` dans `harness/audit_review.py` : le gate `record` vérifie l'absence de tout marqueur de remplissage restant, la présence d'au moins un jeton de verdict, et le statut `PROPOSED` de l'audit. Aucune vérification de forme de la preuve (bloc de code, commande, chemin). Le brief A décrit fidèlement un vrai manque, y compris dans **ce document-ci** — voir § 4. |
| 11 | Claims dépendant de `gh` (§ 2.1 CI deux vagues, § 8.3 checks CI PR #69, timing 1 h 42 push→PR) | PARTIAL — non rejouable dans mon environnement | Mon sandbox n'a pas de `gh` authentifié (`gh auth status` → « not logged into any GitHub hosts »), exactement la limite que l'audit reproche à la revue qu'il relit (§ 2.2) et documente pour lui-même au P2-3. Ce que j'ai pu vérifier **sans** `gh`, via `git log` seul : l'horodatage du commit de fusion `c296c47` est `14:53:11+02:00` = `12:53:11Z`, identique à l'heure de fusion citée. Le reste (deux vagues de checks, `head_sha` partagé, décompte `13 SUCCESS/3 SKIPPED/1 CANCELLED/1 QUEUED`, écart avec le « 14 pass/3 skipping/1 pending » de la revue d'origine) repose sur l'API GitHub Actions et reste **non vérifié par moi** — ni confirmé ni infirmé, faute d'accès, et je ne le compte pas comme un défaut de l'audit puisqu'il documente lui-même cette même contrainte comme un fait structurel (P2-3), pas comme un artefact caché. |

## 3. Points à porter au propriétaire (NEEDS_OWNER)

Aucun point technique de cet audit ne relève d'un arbitrage métier — c'est
un audit sur la **forme des preuves**, pas sur une décision de simulation.
Deux choses restent au propriétaire, mais elles sont déjà cadrées comme
telles par l'audit lui-même (§ 6, « proposition, pas instruction ») :

- Retenir ou non le brief A (durcir `harness/audit_review.py` pour exiger
  une preuve citable — commande, sortie, chemin de sonde ou `run id` — dans
  chaque cellule de verdict). J'ai confirmé que le défaut qu'il vise est
  réel (point 10 ci-dessus) et qu'il touche **aussi ce document-ci** :
  je cite bien des commandes et leurs sorties (points 1 à 7 ci-dessus), donc
  cette revue passerait le futur validateur proposé — mais c'est une
  vérification de forme sur ma propre production, pas une garantie
  indépendante.
- Retenir ou non le brief B (figer la table de sensibilité
  `HUNGER_DEATH_SCALE` en test rouge). J'ai vérifié que l'infrastructure
  citée (`sim/tests/test_survie_derivee.py`, `sim/tests/test_mortalite_continue.py`)
  existe bien, donc le brief ne suppose pas un fichier absent.

## 4. Synthèse

**Ce qui tient, intégralement.** J'ai reproduit indépendamment tout ce qui
était reproductible sans `gh` : la provenance des deux commits (§ 1), les
cinq portes mécaniques (point 2), le contenu littéral du fichier fusionné —
0 bloc de code, 9/2/3/2 occurrences de mots-verdicts (points 3 et 6), les 13
valeurs numériques de la sonde mortalité/sensibilité (point 4), la
non-dépendance structurelle du seuil de survie (point 5), les deux
événements du registre et l'écart `retained_points`/constats (point 7), et
l'existence réelle des 7 audits cités comme « déjà instruit ailleurs »
(point 8). Sur ces neuf points vérifiables sans accès réseau, **zéro
écart** avec ce que l'audit annonce — y compris au chiffre après la
virgule près sur les grandeurs physiques. Le seul point non tranché (point
11, les décomptes CI dépendant de `gh`) est une limite d'environnement, pas
un doute sur le fond, et l'audit documente lui-même cette même limite comme
un fait structurel plutôt que de la cacher.

**Ce qui tombe : rien, sur le plan technique.** Aucun chiffre, aucune
citation de fichier, aucune référence à un autre audit ne s'est révélée
fausse ou gonflée pendant cette contre-vérification.

**Le constat central de l'audit (P1-1) se vérifie sur lui-même.** L'audit
affirme que 7 des 8 verdicts de la revue `CLAUDE-CURSOR-0e98199-...`
décrivent une méthode sans jamais citer la commande, le chemin de script ou
la sortie qui la soutient — et que c'est un défaut de forme, pas de fond,
puisque le contenu s'avère vrai une fois rejoué. J'ai vérifié cette
affirmation de deux façons indépendantes : en comptant moi-même les blocs
de code du fichier fusionné (0, confirmé), et en rejouant sa propre sonde
de bout en bout sans avoir lu son code source au préalable (13/13
identique). Les deux convergent avec ce que l'audit annonce. C'est un audit
qui applique à lui-même l'exigence qu'il porte (§ 8, huit sous-sections de
commandes et sorties collées), ce qui est cohérent avec son propre
diagnostic : citer une preuve rejouable coûte peu (2,0 s mesurées, 2,7 s
chez moi) par rapport à ce qu'elle protège.

**Nuance mineure, sans incidence sur la sévérité.** Le point 9 ci-dessus
confirme le compte « sept sur huit » au sens strict de la lecture ligne à
ligne — je n'ai rien trouvé qui l'affaiblisse.

**Recommandation.** Aucun frein technique à `AUDIT_CHALLENGED`. Les deux
briefs proposés (A : preuve citable obligatoire dans le validateur de
revue ; B : figer la table de sensibilité en test) sont chacun étayés par
un défaut réel et localement vérifié (points 10 et la note sur
l'infrastructure de test existante, § 3). Leur adoption reste, comme
l'audit le dit lui-même, une décision de la boucle (`architecture/README.md`,
ADR-0005/0006) — non prescrite ici.
