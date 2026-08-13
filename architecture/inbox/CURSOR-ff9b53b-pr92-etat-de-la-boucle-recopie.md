---
audit_id:                CURSOR-ff9b53b-pr92-etat-de-la-boucle-recopie
auditor:                 cursor-cloud
target_branch:           master
target_commit:           ff9b53b0da1be3af74af09fe43fe711f8e8c8fdd
created_at:              2026-08-13T13:20:00Z
audit_type:              pr-critique
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---

# Critique de la pull request #92 — « Clôture de session 2026-08-13 (après-midi) : addendum HANDOFF + correction factuelle ROADMAP »

Critique conduite selon `architecture/review-guidelines.md` (six lentilles,
sévérités P0–P3, une preuve citée par constat). Cet audit **n'instruit rien** :
il propose, la décision reste à la boucle (`architecture/README.md`,
ADR-0005/0006).

## 0. Ce qu'il faut retenir en trois phrases

Presque tout ce que cette PR affirme est **vrai et rejouable** : j'ai remesuré
neuf de ses affirmations chiffrées (heures de fusion des PRs #65/#69, huit
cycles contre-audit→décision, registre de budget CI à 1 octet, trois runs
`pipeline-challenge` en échec à partir de 11:14 UTC, `ACCEPT` du gate « dix
contrôles au vert », branches parasites du lot 014 réellement absentes du
distant) et **toutes tombent juste**.

Le défaut est ailleurs, dans le seul passage du texte qui n'a pas été mesuré
mais **recopié** : la ligne « État de la boucle » nomme « graines 015/016 » les
deux audits `AUDIT_CONVERTED`, alors que les deux audits convertis à ce commit
sont `a4de4bb` et `a600532` — le même fichier le dit correctement 90 lignes plus
bas — et sa décomposition des 15 `PROPOSED` ne s'additionne pas (11 + 4 mesurés
contre 12 + 3 écrits, l'audit de la PR #74 étant omis).

Second défaut, de forme mais traçant : la correction du `ROADMAP.md` **retire**
la ligne d'historique en invoquant le constat P1-1 de `CURSOR-3b47ffe`, alors
que ce constat et la revue qui l'a jugé demandaient l'inverse — que la ligne
existe et soit **signée à la forme d'une délégation Hermes**.

## Provenance et périmètre audité

| | |
|---|---|
| PR | [#92](https://github.com/PLiagre/ForgeHistory/pull/92), `forge/cloture-session-20260813-e180` → `master`, **ouverte** (non fusionnée) |
| Commit audité | `ff9b53b0da1be3af74af09fe43fe711f8e8c8fdd`, committé 2026-08-13T13:07:08Z (auteur `Cursor Agent`, co-auteur `liagre.pe`) |
| Diff | 2 fichiers, +117 / −2 : `HANDOFF.md` (+115), `ROADMAP.md` (1 ligne remplacée) |
| Base de la branche | `13432b8` (2026-08-13T12:55:45Z) — **6 commits de retard** sur `master` (`1601290`) au moment de l'audit |
| État de la boucle à ce commit | 15 `PROPOSED`, 3 `CHALLENGED`, 11 `APPROVED`, 2 `CONVERTED`, 8 `ARCHIVED` (39 fichiers d'inbox, 63 lignes de registre) |

`target_commit` est ici la **tête de la PR**, pas un commit de `master` : la PR
est ouverte. Même convention que `CURSOR-bd34ded-pr83-…` (audit de la PR #83,
elle aussi ouverte).

## Les six lentilles — où chacune a mordu

| lentille | résultat sur cette PR |
|---|---|
| 1 — Intention avant diff | **tenue** : un seul objet (la clôture de session), séparé du registre et des lots, conformément au constat 8 de `CURSOR-a4de4bb` que la PR cite. Le diff ne touche ni le registre, ni un brief, ni du code. |
| 2 — Preuve d'exécution | **partielle** : neuf affirmations vérifiées justes (§ 7), mais les compteurs de la boucle ne sont pas mesurés (P1-1, P2-1). |
| 3 — Portes mécaniques | **vertes**, une réserve déjà consignée ailleurs (§ 2). |
| 4 — Cadrage adverse | appliqué : j'ai cherché où chaque phrase du journal est **fausse**, pas si elle « a l'air correcte ». Deux endroits trouvés. |
| 5 — Taille et découpage | **tenue** pour la PR (2 fichiers), mais le fichier cible grossit sans borne (P2-2). |
| 6 — Pièges du code généré par IA | **un cas caractéristique** : le succès affirmé non mesuré (« graines 015/016 » converties alors que la conversion attend une PR non fusionnée) — P1-1. |

## 1. Lentille 1 — Intention avant diff

La description de PR annonce un objet unique : « Clôture de session — séparée du
registre et des lots (constat 8 de `a4de4bb` : registre ≠ lot ≠ clôture de
session) ». Vérifié : le diff ne contient que `HANDOFF.md` et `ROADMAP.md`,
aucun chemin de registre, de brief ou de code.

Elle annonce aussi l'indépendance : « indépendante des trois autres PRs, aucun
conflit attendu — elle ne touche ni le ledger ni le code ». Vérifié : les trois
autres PRs sont bien encore ouvertes, donc l'ordre de fusion proposé reste
valable et aucun de leurs chemins n'est touché.

```
$ gh pr view 77 --json state,mergedAt -q '"\(.state) \(.mergedAt)"'   → OPEN null
$ gh pr view 89 ...                                                  → OPEN null
$ gh pr view 83 ...                                                  → OPEN null
```

L'intention est donc lisible et respectée. C'est **parce que** l'intention est
« consigner l'état vrai de la session » que les deux constats qui suivent
comptent : sur ce document, l'exactitude *est* la livraison.

## 2. Lentille 3 — Portes mécaniques : classification de la CI du commit audité

**Verte.** `gh pr checks 92` rejoué (17 lignes, deux événements) :

```
actionlint pass (x2)   f0-demo pass (x2)   gitleaks pass (x2)   schema pass (x2)
tests pass (x2)        sim-tests pass (x2) invoke-cursor-auditor pass
cursor-scope skipping (x2)   check-and-automerge skipping
Reconcile local Hermes state  pending
```

`mergeStateStatus` vaut `UNSTABLE` : cela vient du seul job en attente,
`Reconcile local Hermes state`, qui vise un runner auto-hébergé chez le
propriétaire (`hermes-observer.yml:32`, `runs-on: [self-hosted, Windows, X64,
hermes-observer]`). **Ce n'est pas un constat neuf** : il est déjà consigné au
§ 1 de `CURSOR-9626e9b-pr85-…` (« `pending` et le restera … n'a bloqué ni la
fusion ni le reste »). Je le classe, je ne le recompte pas.

`check-and-automerge` est **ignoré**, ce qui est le comportement voulu ici : le
job est conditionné à `startsWith(github.head_ref, 'cursor/') ||
startsWith(github.head_ref, 'forge-bot/')` (`merge-bot.yml:27`) et la branche
est `forge/*`. La PR ne peut donc pas s'auto-fusionner — cohérent avec
`hermes/README.md:97-98` (« Ces chemins ne figurent pas dans l'allowlist du
merge-bot : une PR Hermes est toujours relue par le propriétaire »). Réserve
d'information : la garantie tient ici par le **nom de branche**, pas par la
liste de chemins ; une même modification de `ROADMAP.md` poussée sur une branche
`cursor/*` ou `forge-bot/*` ferait entrer le job et serait alors jugée sur les
chemins.

## 3. P1-1 — La ligne « État de la boucle » nomme deux audits convertis qui ne le sont pas, et sa décomposition ne s'additionne pas

**Preuve.** `HANDOFF.md:99-106` au commit audité :

```
**État de la boucle** : 15 `PROPOSED` (les 12 du 2026-08-12 + les
critiques du jour des PRs #71/#73/#76, non challengées — le 429 est
revenu), 3 `CHALLENGED` du 2026-08-12 (...), 11 `APPROVED` (dont les six
non convertis, décision tracée), 2 `CONVERTED` (graines 015/016), 8 `ARCHIVED`.
```

Reconstruction depuis les deux sources de vérité du même commit
(`architecture/inbox/**` pour l'existence, `architecture/audit-ledger.jsonl`
pour l'état — dernier événement par `audit_id`, absence d'événement =
`PROPOSED`) :

```
$ git show ff9b53b:architecture/audit-ledger.jsonl | grep -c .
63
$ git ls-tree -r ff9b53b --name-only architecture/inbox/ | wc -l
39
=== etats au SHA de la PR #92 (ff9b53b) ===
  PROPOSED                   15
  AUDIT_APPROVED             11
  AUDIT_ARCHIVED              8
  AUDIT_CHALLENGED            3
  AUDIT_CONVERTED             2
total: 39
=== CONVERTED ===
   CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois
   CURSOR-a600532-fusion-sans-contre-audit
```

Les cinq totaux sont **exacts**. Deux de leurs libellés ne le sont pas.

**a) « 2 `CONVERTED` (graines 015/016) » est faux.** Les deux audits convertis à
ce commit sont `a4de4bb` et `a600532`, convertis le 2026-08-13T08:40:34Z (soit
les graines **013** et **014**, pas 015/016). Les deux audits d'où viennent les
graines 015/016 sont, au même commit, `AUDIT_APPROVED` :

```
$ git show ff9b53b:architecture/audit-ledger.jsonl | grep -E "0e98199|29913c0"
AUDIT_CHALLENGED   2026-08-13T12:50:05Z  CURSOR-29913c0-pr69-seuil-survie-non-borne
AUDIT_APPROVED     2026-08-13T12:50:05Z  CURSOR-29913c0-pr69-seuil-survie-non-borne
AUDIT_CHALLENGED   2026-08-13T12:53:26Z  CURSOR-0e98199-pr69-seuil-survie-ignore-mortalite
AUDIT_APPROVED     2026-08-13T12:53:26Z  CURSOR-0e98199-pr69-seuil-survie-ignore-mortalite
```

(les lignes JSON sont réduites ici à `event`, `timestamp`, `audit_id` ; aucun
événement `AUDIT_CONVERTED` ne les concerne à ce commit)

Leur conversion vit dans la PR **#89**, encore ouverte (§ 1). Le même fichier
`HANDOFF.md` le dit d'ailleurs correctement 90 lignes plus bas, dans la section
de la session précédente, restée inchangée par cette PR :

```
HANDOFF.md:193-195
**État de la boucle d'audit** : `3b47ffe` ARCHIVED ; `a4de4bb` CONVERTED
(graine 013, exécutée par ce lot) ; `a600532` CONVERTED (graine 014 en
file d'attente).
```

Deux phrases du même fichier attribuent donc les deux mêmes compteurs à deux
paires d'audits différentes. C'est le piège n° 1 de la lentille 6 : un succès
**affirmé** (« converties ») là où la mesure dit « approuvées, conversion en
attente de fusion ».

**b) La décomposition des 15 `PROPOSED` ne s'additionne pas.** Mesuré, par
`created_at` du frontmatter des 15 fichiers concernés :

```
  2026-08-12 : 11
  2026-08-13 : 4
       CURSOR-4b6dcff-pr73-contre-audit-recompte-a-tort
       CURSOR-786ec32-pr74-verdicts-fantomes-au-registre
       CURSOR-8894f15-pr71-arbitrage-proprietaire-efface
       CURSOR-a7d1c57-pr76-approbation-sans-conversion
```

Le texte écrit « les 12 du 2026-08-12 + les critiques du jour des PRs
#71/#73/#76 » : **11 + 4**, pas 12 + 3. Les deux opérandes sont faux et leur
somme est juste par compensation. L'audit de la PR **#74**
(`CURSOR-786ec32`) est omis de l'énumération, alors que le point 2 de la même
section liste bien #74 parmi les huit revues traitées. La valeur « 12 » figure
telle quelle à `HANDOFF.md:195` (section précédente) : la décomposition a
vraisemblablement été reprise du texte antérieur au lieu d'être recomptée — je
donne ce mécanisme comme hypothèse, la mesure ci-dessus se suffit.

**Pourquoi P1 et pas P3.** `CLAUDE.md` § Status désigne `HANDOFF.md` comme
l'état de référence lu au début de chaque session. La même section planifie
« la purge motivée de l'arriéré (STALE/archivage) » en s'appuyant sur ces
chiffres. Un prochain acteur qui lit « graines 015/016 converties » croira le
travail des deux audits moteur enregistré alors qu'il tient encore à une PR
ouverte : si #89 était refermée sans fusion, la perte serait invisible depuis le
document censé la signaler. Correction possible avant fusion en une phrase, sans
toucher au code.

*Note.* Les cinq classes couvrent bien les 39 fichiers d'inbox présents à ce
commit — la somme du journal est donc juste **de sa propre branche**. Elle ne
l'est plus de `master`, qui en compte 42 : voir P2-1.

## 4. P1-2 — La correction du `ROADMAP.md` retire la ligne d'historique en invoquant un constat qui demandait l'inverse

**Preuve.** Message de commit de `ff9b53b` :

```
Correction factuelle de ROADMAP.md signalée ici, sans ligne ajoutée à la
table d'historique (voix éditoriale d'Hermes, réponse au point 2/P1-1 de
CURSOR-3b47ffe).
```

Ce que P1-1 de `CURSOR-3b47ffe` dit réellement
(`architecture/inbox/CURSOR-3b47ffe-pr57-monde-sans-faim.md:167-172`) :

```
Les deux lignes précédentes de l'historique de `ROADMAP.md` montrent la
forme attendue d'une délégation (« hermes (rédaction déléguée à Cursor,
décision propriétaire) ») : l'auteur reste Hermes. La nouvelle ligne signe
au nom du CTO substitué, ce qui est un autre acteur.
```

Ce que la revue de Claude en a retenu — point 2, verdict `PARTIAL`
(`architecture/reviews/CLAUDE-CURSOR-3b47ffe-pr57-monde-sans-faim.md:50`) :

```
Ce qui reste confirmé : la ligne d'historique signe un acteur hors du
contrat d'écriture d'Hermes (la forme attendue est la délégation)
```

Et ce point 2 est **retenu** par la décision propriétaire
(`architecture/decisions/DECISION-CURSOR-3b47ffe-pr57-monde-sans-faim.md:5`,
`retained_points: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]`).

Le défaut visé était donc la **signature** de la ligne, jamais son existence.
La réponse apportée — supprimer la ligne — enlève l'artefact de traçabilité et
laisse intacte la question de l'acteur. Elle contredit par ailleurs une règle
écrite : `hermes/README.md:18` — « `ROADMAP.md` (racine) | la feuille de route
jeu + projet | libre, **mais l'« Historique des révisions » en bas est
obligatoire** ». La table existe toujours (`ROADMAP.md:97-103`), et son
précédent immédiat est exactement le cas d'espèce :

```
ROADMAP.md:103
| 2026-08-12 | orchestrateur Cursor (remplaçant du CTO Claude, indisponible
— instruction propriétaire) | correction factuelle uniquement : brief 011 (...)
```

Résultat mesurable : la seule ligne de statut modifiée par cette PR
(`ROADMAP.md:38`, phase F2) n'a plus de trace dans le document — qui, quand, à
quel titre. La trace existe dans le message de commit, pas dans le fichier que
le propriétaire lit.

**Ce que je ne conteste pas** (et qu'il serait du bruit de recontester) : le
**droit** de faire cette correction. L'en-tête du document l'autorise
explicitement — `ROADMAP.md:7-8`, « une correction factuelle (statut devenu
faux) est permise à tout acteur, en la signalant dans le message de commit » —
et c'est précisément la délimitation qui a fait passer P1-1 de « confirmé » à
`PARTIAL`. Le fond de la correction est également **juste** : brief 013 est bien
fusionné dans `master` (répertoire `harness/queue/briefs/013-sim-tick-nourrit-une-fois`
présent à `origin/master`) et brief 014 n'y a que `brief.md`, `eval-rubric.md`
et un `.gitkeep`, ce qui correspond bien à « livré et accepté, PR en revue ».

## 5. P2-1 — Un compteur recopié à la main là où une vue générée existe déjà

**Preuve.** `hermes/DASHBOARD.md:14`, au `master` du jour (`1601290`, régénéré
2026-08-13T13:05:35Z) :

```
- **Audits en cours** : 34 — boucles closes : 8.
```

`HANDOFF.md:99-106`, écrit deux minutes plus tard (13:07:08Z), dit
15 + 3 + 11 + 2 = **31** en cours et 8 closes. Deux documents du même dépôt, à
deux minutes d'écart, donnent deux états de la même boucle. L'écart de 3 est
identifiable exactement : les trois audits fusionnés pendant que la branche
vivait, absents de sa base :

```
$ git log $(git merge-base ff9b53b origin/master)..origin/master --oneline
1601290 hermes: tableau de bord régénéré
731fc20 audit: critique de la PR #85 (...) (#91)
e4b60d1 hermes: tableau de bord régénéré
6bd3709 audit: critique de la PR #84 (...) (#90)
a7852d6 hermes: tableau de bord régénéré
13e55f1 audit: critique de la PR #83 (cursor-auditor) (#88)
```

Recompté sur `master` : **18** `PROPOSED` et 42 audits au total, contre 15 et
39 (la somme du journal). Le chiffre du journal est donc vrai de l'arbre de sa
branche et faux de `master` — y compris **avant** sa fusion.

La comparaison est bien de même nature : `hermes/dashboard.py:197` définit
« audits en cours » comme `[a for a in audits if a["event"] != "AUDIT_ARCHIVED"]`,
soit 42 − 8 = 34, tandis que le journal additionne ses quatre classes non
archivées, 15 + 3 + 11 + 2 = 31.

Ce n'est pas une faute de frappe mais un mode de panne structurel : le principe
non négociable n° 1 de `CLAUDE.md` (« One source of truth — views never become
parallel databases ») s'applique ici au journal de session, qui redit à la main
ce que `hermes/dashboard.py` dérive du registre — et que `hermes/README.md:19`
qualifie de « **généré** …  jamais édité à la main ». Le dépôt possède déjà
`harness/audit_ledger.py` et le registre lui-même ; un compteur recopié ne peut
que dériver. Preuve par l'absurde : le dépôt de **cet audit-ci** rendra le
chiffre faux d'une unité de plus.

## 6. P2-2 — `HANDOFF.md` n'est plus réécrit mais empilé : 572 lignes, 36 374 octets, cinq sessions

**Preuve.**

```
$ git show ff9b53b:HANDOFF.md | wc -l                 → 572   (master : 457)
$ git show ff9b53b:HANDOFF.md | wc -c                 → 36374
$ git show ff9b53b:HANDOFF.md | grep -c '^## Session' → 5
```

Le diff ne réécrit pas : il **préfixe** 115 lignes et rétrograde l'ancienne
section (`-## Session la plus récente — … (suite)` →
`+## Session précédente — … (suite)`). Le contrat écrit dit autre chose :
`CLAUDE.md` § Status — « See HANDOFF.md — **rewritten** at the end of every
session » — et `.claude/commands/forge-checkpoint.md:2` : « Rewrite HANDOFF.md's
Status and Last Session Summary **from live command output** — the actual
end-of-session state, **not a narrated guess** ». Cette dernière phrase décrit
aussi, mot pour mot, le défaut de P1-1.

Conséquence mesurable : ~36 Ko de contexte, dont quatre sessions périmées, lus à
chaque démarrage de session. C'est exactement le mode de panne décrit par les
sources externes sur le budget de contexte : l'historique est le seul tier qui
croît sans borne et il doit être **comprimé en roulant**, pas conservé brut
[S1 Tier 3, S2]. Le coût est réel et déjà comptabilisé ailleurs dans ce même
dépôt (`827d54e` point 4 : 7,2771804 USD de transcripts pour la journée). Le
constat P1-1 en est un symptôme : la valeur fausse « 12 » est disponible à la
ligne 195 du même fichier, ce qui rend la recopie plus facile que la mesure.

## 7. Ce qui tient (P3 — information, pas constat)

Rejoué, exact au chiffre ou à la minute près :

| affirmation du journal | mesure |
|---|---|
| « PRs #65 et #69 … fusionnées … (10:47 et 10:48 UTC) » | `#65 MERGED 2026-08-13T10:47:51Z`, `#69 MERGED 2026-08-13T10:48:46Z` |
| « huit cycles CHALLENGED→APPROVED au ledger » | 8 cycles le 2026-08-13 hors session du matin : 11:00:16 / 11:01:51 / 11:03:12 / 11:04:50, puis 12:50:05 / 12:51:53 / 12:53:26 / 12:55:16 |
| « PRs #71/#73/#74/#76 … puis #84/#85/#86/#87 ouvertes à la main, une à une » | les huit sont `MERGED`, espacées de 1 à 2 minutes (11:00:01 → 11:04:26, 12:49:51 → 12:55:00) : la sérialisation revendiquée est visible dans les horodatages |
| « `429` à partir de 11:14 UTC, runs 31694643198/31694909507/31694993448 en échec » | trois runs `pipeline-challenge`, `conclusion: failure`, créés 11:14:48Z / 11:18:22Z / 11:19:33Z |
| « Gate ACCEPT dix sur dix » | `verdict.md` du lot 014, branche `forge/014-…-e180` : « code de sortie `0` et `VERDICT: ACCEPT`, les dix contrôles au vert » |
| « `ci-budget-ledger.jsonl` à 1 octet » | `git cat-file -s origin/master:harness/pipeline/ci-budget-ledger.jsonl` → `1` |
| « branches parasites supprimées (locales ET distantes) » | ni `cursor/brief-014-planificateur-d4e7` ni `cursor/014-pipeline-it3-*-111d` ne figurent dans `git ls-remote --heads origin 'refs/heads/cursor/*'` |
| « brief 013 … fusionné » (ROADMAP) | `harness/queue/briefs/013-sim-tick-nourrit-une-fois/` présent à `origin/master` |
| ordre de fusion #77 → #89 → #83 | les trois PRs sont encore `OPEN` : l'ordre proposé est toujours applicable |

Deux informations complémentaires, sans demande attachée :

- Neuf branches `cursor/*` orphelines subsistent sur le distant
  (`audit-3663de5-…`, `audit-beb57b5-…`, `audit-commit-master-7bf5`,
  `audit-commit-master-ebee`, `audit-dbd315c-…`, `audit-de-commit-master-e7e6`,
  `audit-pr-30-122d`, `audit-pull-request-34-713b`, `ledger-post-fusion-39f4`).
  La revendication du point 6 est **vraie** pour les deux branches du lot 014 ;
  l'hygiène n'a simplement jamais été appliquée en amont.
- Les réserves N9–N12 citées par le journal correspondent bien à des réserves
  écrites du `verdict.md` du lot 014 ; elles concernent la PR #83, pas celle-ci.

## 8. Lentille 5 — Taille et découpage

2 fichiers, 117 lignes ajoutées : sous le seuil (~5 fichiers / quelques centaines
de lignes) au-delà duquel une relecture honnête décroche
(`architecture/review-guidelines.md` § lentille 5). Aucun
`NEEDS_SPLIT` à recommander pour la PR elle-même. La remarque de taille porte
sur le **fichier cible**, pas sur le diff (P2-2).

## 9. Risques par sévérité

| sévérité | constat | risque si rien n'est fait |
|---|---|---|
| P1 | P1-1 — deux `CONVERTED` mal nommés, décomposition qui ne s'additionne pas | Le document de référence de la prochaine session affirme enregistré un travail (graines 015/016) qui tient encore à une PR ouverte ; sa fermeture serait invisible depuis le seul endroit censé la signaler. |
| P1 | P1-2 — ligne d'historique `ROADMAP` retirée en invoquant un constat qui demandait l'inverse | La modification de la feuille de route perd sa trace dans le document, et un constat retenu par le propriétaire est classé « traité » par une action qui ne le traite pas — le registre des constats cesse d'être fiable. |
| P2 | P2-1 — compteur de boucle recopié à la main | Deux états de la boucle coexistent (31 vs 34) ; la vue générée cesse d'être la référence et le principe n° 1 de `CLAUDE.md` s'affaiblit à chaque session. |
| P2 | P2-2 — `HANDOFF.md` empilé au lieu d'être réécrit | ~36 Ko dont quatre sessions périmées lus à chaque démarrage : coût de contexte croissant, et recopie d'un chiffre périmé rendue plus facile que sa mesure (cause directe de P1-1). |
| P3 | branches `cursor/*` orphelines, `check-and-automerge` garanti par le nom de branche | Information ; aucune action demandée par cet audit. |

## 10. Briefs atomiques proposés (3 au maximum — proposition, pas instruction)

Un audit ne s'attribue aucune autorité d'exécution : ces pistes n'ont de valeur
que si le propriétaire les retient et que Claude les transforme en briefs
(`CLAUDE.md` › Single Source of Instruction).

1. **L'état de la boucle, dérivé et non recopié.** Une commande unique produit
   le décompte par état depuis `architecture/inbox/**` + le registre, avec le
   SHA sur lequel elle a été calculée ; `HANDOFF.md` cite cette sortie (ou
   renvoie à `hermes/DASHBOARD.md`) au lieu de la reformuler. Répond à P1-1 et
   P2-1. Traite aussi la matière déjà approuvée non convertie
   (`f978cc7` point 1 : un critère déclaré qu'aucun code n'évalue).
2. **La trace d'une correction factuelle du `ROADMAP`.** Fixer la forme
   attendue d'une ligne d'historique quand l'auteur n'est pas Hermes (la
   délégation, telle que la revue de `3b47ffe` l'a retenue) et la rendre
   observable : un diff qui touche `ROADMAP.md` hors table d'historique sans
   ajouter de ligne devient visible dans la CI. Répond à P1-2.
3. **Rotation de `HANDOFF.md`.** Une seule session courante dans le fichier,
   les précédentes déplacées vers un emplacement archivé, avec un plafond de
   taille mesuré. Répond à P2-2.

## Sources externes

| # | source | consulté le |
|---|---|---|
| S1 | tianpan.co — *Tokens Are a Finite Resource: A Budget Allocation Framework for Complex Agents* (2026-04-17) — <https://tianpan.co/blog/2026-04-17-token-budget-allocation-complex-agents> | 2026-08-13 |
| S2 | Zylos Research — *Token Budget Management and Cost Control for Autonomous AI Agents* (2026-06-30) — <https://zylos.ai/research/2026-06-30-token-budget-management-cost-control-autonomous-agents/> | 2026-08-13 |
| S3 | arXiv 2604.01664v1 — *ContextBudget: Budget-Aware Context Management for Long-Horizon Search Agents* (avril 2026) — <https://arxiv.org/html/2604.01664v1> | 2026-08-13 |
| S4 | Tenki — *OWASP AI Agent Security Top 10: CI/CD Audit Guide* (`datePublished` 2026-04-27) — <https://tenki.cloud/blog/owasp-ai-agent-security-cicd-audit> | 2026-08-13 |
| S5 | Harness — *AI Deployment in 2026: CI/CD for LLMs & Agents* (date de publication non affichée sur la page) — <https://www.harness.io/blog/ai-deployment-in-production-orchestrate-llms-rag-agents> | 2026-08-13 |

Rattachement des sources aux constats :

- **S4 fonde P1-1.** La catégorie ASI06 (« memory poisoning ») décrit exactement
  ce mode de panne pour un état persistant relu par l'agent suivant : « slow and
  hard to detect. It looks like organic learning ». Sa recommandation
  d'audit — « track provenance for every stored fact: where it came from, when
  it was added, what confidence level it carries » — est ce qui manque au
  compteur : aucun SHA, aucune commande, aucune date de mesure. ASI09 (« trust
  erosion ») ajoute le versant humain : « Over-trust leads to rubber-stamping. »
- **S1 et S3 fondent P2-2.** S1 range l'historique de conversation dans le
  tier 3, « where most systems fail. History grows unboundedly if left
  unmanaged. The correct model is rolling compression ». S3 formalise la même
  contrainte comme décision séquentielle sous budget de contexte, et mesure la
  dégradation quand le budget se resserre.
- **S2 quantifie le coût de P2-2** : « Context window cost is quadratic in a
  naive implementation … A 100K-token context costs 10x more per call than a
  10K-token context », et recommande des plafonds durs par session — même
  discipline que `harness/budget.py` côté Générateur.
- **S5 fonde P2-1** : « immutable logging and audit trails so you can trace
  decisions back to specific versions of your AI stack » — un état recopié à la
  main dans un second document casse ce chaînage, puisque deux versions du même
  fait coexistent sans que l'une soit désignée comme la source.
- La **forme** de cette critique est fondée par les sources propres du guide
  (`architecture/review-guidelines.md` § Sources — numérotation distincte de
  celle du tableau ci-dessus) : preuve d'exécution plutôt qu'affirmation
  (§ 7), portes mécaniques d'abord (§ 2), seuil de découpage (§ 8).

## Commandes rejouées (récapitulatif)

```bash
# identité, diff et CI de la PR
gh pr view 92 --repo PLiagre/ForgeHistory --json changedFiles,additions,deletions,commits,mergeStateStatus
gh pr diff 92 --repo PLiagre/ForgeHistory        # 140 lignes, 2 fichiers
gh pr checks 92 --repo PLiagre/ForgeHistory      # 13 pass, 3 skipping, 1 pending

# dérive de la branche par rapport à master
git log $(git merge-base ff9b53b origin/master)..origin/master --oneline   # 6 commits

# état de la boucle, recompté aux deux SHA (inbox + dernier événement du registre)
git show ff9b53b:architecture/audit-ledger.jsonl        # 63 lignes, 24 audits touchés
git ls-tree -r ff9b53b --name-only architecture/inbox/  # 39 fichiers (master : 42)
#   → ff9b53b : 15 PROPOSED / 3 CHALLENGED / 11 APPROVED / 2 CONVERTED / 8 ARCHIVED
#   → master  : 18 PROPOSED / 3 CHALLENGED / 11 APPROVED / 2 CONVERTED / 8 ARCHIVED
#   → CONVERTED = a4de4bb + a600532 (2026-08-13T08:40:34Z), PAS 0e98199/29913c0

# décomposition des 15 PROPOSED par created_at du frontmatter
#   → 11 datés 2026-08-12, 4 datés 2026-08-13 (PRs #71, #73, #74, #76)

# affirmations du journal vérifiées une à une
gh pr view 65 / 69 / 77 / 83 / 89 --json state,mergedAt
gh run view 31694643198 / 31694909507 / 31694993448 --json conclusion,createdAt
git cat-file -s $(git rev-parse origin/master:harness/pipeline/ci-budget-ledger.jsonl)   # 1
git ls-remote --heads origin 'refs/heads/cursor/*'      # 9 branches, aucune du lot 014
git show FETCH_HEAD:harness/queue/briefs/014-pipeline-contre-audit-porte/verdict.md

# croissance du fichier cible
git show ff9b53b:HANDOFF.md | wc -l   # 572   (origin/master : 457)
git show ff9b53b:HANDOFF.md | wc -c   # 36374
git show ff9b53b:HANDOFF.md | grep -c '^## Session'   # 5
```
