---
review_of: CURSOR-587ee82-pr84-contre-audit-sans-pouvoir-de-refus
reviewer: claude-code
target_commit: 587ee824c2ba5ba013887076cae9a8aa416cc560
reviewed_at: 2026-08-13T13:05:01Z
---

# Contre-audit de CURSOR-587ee82-pr84-contre-audit-sans-pouvoir-de-refus

Rôle : vérifier la *véracité technique* de l'audit, pas sa valeur métier.
Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

## 1. Provenance (re-vérifiée)

- target_commit annoncé : 587ee824c2ba5ba013887076cae9a8aa416cc560
- Le commit existe-t-il dans l'historique de la branche cible ? **Oui.**

  ```
  $ git log -1 --format='parents=%P%n subj=%s' 587ee82
  parents=e0dcb4fb69e83e72f339295c296cd96241dfe7d7
   subj=challenge: revue CLAUDE-CURSOR-29913c0-pr69-seuil-survie-non-borne (claude-challenger headless, run 31693417136) (#84)
  $ git merge-base --is-ancestor 587ee82 origin/master && echo oui
  oui
  $ git log -1 587ee82 --stat | tail -2
   ...E-CURSOR-29913c0-pr69-seuil-survie-non-borne.md | 115 +++++++++++++++++++++
   1 file changed, 115 insertions(+)
  ```
  Parent unique, sujet, et diff-stat (1 fichier, +115/−0) reproduisent
  exactement la section 0 de l'audit.

- Mesures de l'audit rejouées ? **Oui, les six affirmations rejouées de la
  section 2, plus les mécanismes cités en section 3 (P1-1, P1-2, P2-2,
  P3-1, P3-2).** Détail point par point ci-dessous.

## 2. Verdicts point par point

| # | Point de l'audit | Verdict | Preuve / délimitation |
|---|---|---|---|
| 1 | §0 Identité de l'objet audité (auteur, diff, tête de branche, commit de fusion, squash) | CONFIRMED | `git log -1 587ee82` reproduit parent, sujet, diff-stat (1 fichier, +115/−0) à l'identique. `git merge-base --is-ancestor 587ee82 origin/master` → oui ; `1dc7d09` (tête de branche pré-squash) n'est pas ancêtre — cohérent avec un squash-merge. |
| 2 | §1 Classification CI : un seul job rouge (`regenerate`), le reste vert, `cursor-scope` skip parce que la branche est `forge-bot/*` pas `cursor/*` | PARTIAL | Le mécanisme de skip est CONFIRMED : `.github/workflows/audit-guard.yml:30` porte exactement `if: github.event_name == 'pull_request' && startsWith(github.head_ref, 'cursor/')`. Mais l'appel `gh api repos/.../check-runs` échoue dans mon propre environnement : `gh: To use GitHub CLI in a GitHub Actions workflow, set the GH_TOKEN environment variable.` — je ne peux donc pas rejouer indépendamment la liste des jobs et leurs conclusions ; je m'appuie sur la citation de commande de l'audit sans pouvoir la vérifier moi-même. Ironie notée : c'est exactement le défaut que l'audit décrit lui-même en P3-1, et que je confirme aussi en P3-1 ci-dessous par la même expérience. |
| 3 | §2 Les six affirmations rejouées de la revue de la PR (pytest 35 verts, SC6, horizon N=200..3200, `SURVIE_MARGE_DERIVEE`, formule brief 013, grep `.population =`) | CONFIRMED | Rejeu indépendant de chacune, sur ce dépôt à HEAD (pas de worktree détaché nécessaire — les fichiers cités n'ont pas changé depuis) : `pytest sim/tests/ -q` → `35 passed` ; `measure_sc6_013.py` → `fraction_survie_monde_reel_re = 0.765706`, `SEUIL_SURVIE_POPULATION_FRACTION = 0.7488888888888889`, 536 cellules affamées, 15 666 208 morts, 2 676 487 kg — identique ligne pour ligne ; sonde d'horizon écrite indépendamment (`World.from_g3(42)`, `random.Random(42)`, ticks cumulés jusqu'à N) → `N=200 0.765706`, `N=400 0.754826`, `N=800 0.749715`, `N=1600 0.74748 (hors fenêtre)`, `N=3200 0.746808` — exact au 6ᵉ chiffre ; `SURVIE_MARGE_DERIVEE = 0.15111111111111114`, écart 0.740740740740764 % ; `brief.md:128` porte mot pour mot `cell.food_deficit_kg = max(0.0, cell.food_deficit_kg × (1 - DEFICIT_RECOVERY_RATE_PER_TICK))` ; `grep -n '\.population *=' sim/engine.py` → une seule ligne, `237: cell.population = max(0, cell.population - deaths)`. |
| 4 | P1-1 — la porte de décision automatique (`harness/audit_decision.py:270-300`) n'a que trois branches, dont deux mènent à REJECTED (`all_refuted`, `has_needs_owner`-only) et une à APPROVED (`retained` non vide) ; sur le corpus au commit cible, 0 fichier n'est intégralement REFUTED, donc seule la branche APPROVED est jamais atteinte | CONFIRMED | Lu le code : lignes 270 (`if all_refuted`), 283-290 (`if retained: ... "APPROVED"`), 292-300 (`if has_needs_owner: ... "REJECTED"`) — exactement la logique décrite. Rejeu du comptage sur un worktree détaché au commit cible (`git worktree add --detach /tmp/audit-check 587ee82`) : 18 fichiers dans `architecture/reviews/`, `{'CONFIRMED': 141, 'PARTIAL': 22, 'NEEDS_OWNER': 11, 'REFUTED': 3}`, **aucun fichier intégralement REFUTED** — identique à l'audit. Registre à ce même commit : 13 `AUDIT_APPROVED` (11 `policy:auto` + 2 `owner`), 0 rejet ; l'événement `AUDIT_APPROVED` de `CURSOR-29913c0-pr69-seuil-survie-non-borne` (le 14ᵉ) est daté `2026-08-13T12:50:05Z`, soit **14 secondes** après la fusion de la PR #84 (`12:49:51Z`) — la formule « quatorze secondes après la fusion » de l'audit est donc exacte à la seconde. Total après cet événement : 14 approbations, 0 rejet. |
| 5 | P1-2 — les `retained_points` publiés dans le registre sont des numéros de ligne du tableau de contre-audit, pas des identifiants de points de l'audit source ; `audit_convert.py` les recopie tels quels dans le brief-graine ; précédent déjà sur disque (brief 014) | CONFIRMED | `architecture/audit-ledger.jsonl`, événement `AUDIT_APPROVED` de `CURSOR-29913c0-pr69-seuil-survie-non-borne` : `"retained_points": [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15]` — identique à la citation de l'audit. `harness/audit_convert.py:97-113` (`_approved_retained` + `brief_seed_text`) recopie `retained` verbatim dans la ligne `- Points retenus : {retained_str}` du brief-graine, sans jamais toucher aux identifiants `P1-1`/`P2-1`/etc. de l'audit source. `harness/queue/briefs/014-pipeline-contre-audit-porte/brief.md:11` porte bien `Points retenus : 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 18` — précédent confirmé, issu du même mécanisme (audit source différent, `CURSOR-a600532-fusion-sans-contre-audit`, ce qui est cohérent avec la formulation « hérité du même mécanisme » plutôt que « du même audit »). |
| 6 | P2-1 — un job `regenerate` (hermes-dashboard) a échoué sur le commit de fusion `587ee82` par collision de poussée avec `pipeline-orchestrate` (`aa19906`), contournant la protection de branche | PARTIAL | Le mécanisme allégué (deux écrivains automatiques déclenchés par la même fusion) est plausible et cohérent avec l'historique local : `587ee82` est immédiatement suivi de `aa19906 pipeline-orchestrate: review_recorded` puis `4ceadec hermes: tableau de bord régénéré` — un commit de tableau de bord finit par atterrir juste après, ce qui n'infirme pas « le second a perdu la course sur CE commit précis » (une régénération suivante peut rattraper l'état). Mais je ne peux pas vérifier indépendamment le journal du job 94452854180 ni le message exact « Bypassed rule violations » cité : `gh api` échoue dans cet environnement faute de `GH_TOKEN` (même limitation que le point 2 ci-dessus). Verdict PARTIAL : mécanisme cohérent avec les traces locales, contenu exact du log CI non re-vérifié. |
| 7 | P2-2 — le registre publie un décompte de verdicts (`parse_verdicts`, mots comptés dans tout le texte) différent du décompte réel (`parse_point_verdicts`, colonne du tableau) ; le `REFUTED` fantôme vient de la ligne 11 (phrase de gabarit) ; récurrence non consommée (`CURSOR-786ec32`, `CURSOR-4b6dcff` absents du registre) | CONFIRMED | `harness/audit_review.py:126-134` (`parse_verdicts`) fait `re.findall(rf"\b{token}\b", text)` sur **tout le texte** du fichier, sans restriction à la colonne verdict — vérifié à la lecture. Rejeu sur `architecture/reviews/CLAUDE-CURSOR-29913c0-pr69-seuil-survie-non-borne.md` : `parse_verdicts` → `{'CONFIRMED': 18, 'REFUTED': 1, 'PARTIAL': 3, 'NEEDS_OWNER': 4}`, identique à l'événement `AUDIT_CHALLENGED` du registre ; `parse_point_verdicts` (colonne réelle) → `{'CONFIRMED': 14, 'PARTIAL': 1, 'NEEDS_OWNER': 1}`, cohérent avec la synthèse du document (« 14 sont CONFIRMED […] 1 est PARTIAL »). Ligne 11 du fichier porte bien « Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER. » — seule occurrence du mot REFUTED dans tout le document, hors colonne. `grep -c CURSOR-786ec32-pr74-verdicts-fantomes-au-registre / CURSOR-4b6dcff-pr73-contre-audit-recompte-a-tort architecture/audit-ledger.jsonl` → 0 dans les deux cas ; les deux fichiers existent bien dans `architecture/inbox/` sans jamais avoir été contre-audités. |
| 8 | P3-1 — le challenger (`pipeline-challenge.yml:146-149`) ne reçoit pas `GH_TOKEN`, seulement `CLAUDE_CODE_OAUTH_TOKEN`/`ANTHROPIC_API_KEY`/`AUDIT_ID` ; la publication (`:174`) reçoit `GH_TOKEN` ; donc le point 2 de la revue restera structurellement PARTIAL | CONFIRMED | `grep -n GH_TOKEN .github/workflows/pipeline-challenge.yml` : ligne 147-149 = `CLAUDE_CODE_OAUTH_TOKEN`, `ANTHROPIC_API_KEY`, `AUDIT_ID` (pas de `GH_TOKEN`) ; ligne 174 = `GH_TOKEN: ${{ secrets.FORGE_BOT_PAT \|\| secrets.GITHUB_TOKEN }}`. Confirmation pratique, en plus de la lecture du YAML : j'ai moi-même heurté exactement cette absence en tentant `gh api .../check-runs` au point 2 ci-dessus, dans ce même type d'environnement d'exécution. |
| 9 | P3-2 — `harness/audit_schema.py` ne valide que `architecture/inbox/CURSOR-*.md`, jamais `architecture/reviews/**`, donc la porte `schema` est verte sans avoir lu le diff réel de cette PR | CONFIRMED | `harness/audit_schema.py:26` : `INBOX = REPO_ROOT / "architecture" / "inbox"` ; `validate_inbox` (`:92-98`) itère `inbox.glob("CURSOR-*.md")` exclusivement. Aucune référence à `architecture/reviews/` dans le fichier. Le diff de la PR #84 (`architecture/reviews/CLAUDE-CURSOR-29913c0-...md`) est structurellement hors du périmètre de cette porte. |
| 10 | P3-3 — taille et découpage (1 fichier, +115/−0, seuil ~400 lignes non atteint, aucune recommandation de découpage) | CONFIRMED | `git log -1 587ee82 --stat` confirme 1 fichier, +115/−0 (voir point 1). Constat trivial et sans enjeu de vérifiabilité au-delà du diff-stat déjà vérifié. |
| 11 | §6 Sources externes (S1-S7) et leur usage pour cadrer P1-1/P1-2/P2-2 | NEEDS_OWNER | Les URLs ne sont pas re-visitées ici (hors du périmètre technique de ce contre-audit — vérifier des sources externes n'est pas reproduire une mesure sur ce dépôt). Le lien entre chaque source et le constat qu'elle cadre est cohérent avec le texte de l'audit, mais l'évaluation de la pertinence/qualité de ces sources est un jugement éditorial, pas un fait technique vérifiable par rejeu. |
| 12 | §5 Briefs atomiques proposés B-1/B-2/B-3 (issus de P1-1/P1-2/P2-1) | NEEDS_OWNER | L'audit les présente explicitement comme des propositions et non des ordres (« Ce sont des propositions… la conversion en brief appartient à la boucle »), conformément à la règle d'honnêteté du guide. B-1 et B-2 découlent directement de P1-1 et P1-2, confirmés ci-dessus techniquement ; B-3 découle de P2-1, seulement PARTIAL. Le choix de convertir, prioriser ou fusionner ces propositions (notamment avec le brief 014 déjà en file, qui touche un sujet voisin) est un arbitrage de priorité, pas un fait technique. |

Aucune ligne REFUTED : chaque constat vérifiable a reproduit exactement ;
les deux réserves (points 2 et 6) tiennent à l'absence de `GH_TOKEN` dans
cet environnement d'exécution, pas à un défaut du texte de l'audit — et
cette absence même corrobore indépendamment P3-1.

## 3. Points à porter au propriétaire (NEEDS_OWNER)

- **B-1/B-2/B-3 (§5 de l'audit)** : les défauts qui les motivent sont
  techniquement confirmés à des degrés divers (P1-1 et P1-2 : CONFIRMED ;
  P2-1 : PARTIAL). Reste à trancher si l'un d'eux passe devant le brief 014
  déjà en file — qui touche un sujet très voisin (« porte de contre-audit »)
  — ou s'y fusionne, et sous quelle forme (seuil, échantillonnage, révision
  de `auto_policy.yaml`) B-1 se traduit en brief exécutable.
- **Sources externes (§6)** : leur pertinence et leur poids argumentatif
  relèvent d'un jugement éditorial que ce contre-audit ne tranche pas.

## 4. Synthèse

Sur 12 points vérifiés, **8 CONFIRMED, 2 PARTIAL, 2 NEEDS_OWNER, 0
REFUTED**. Rien ne tombe. Les six affirmations rejouées de la revue
d'origine (point 3) reproduisent au chiffre près, exactement comme l'audit
le rapporte. Les trois constats structurels centraux — P1-1 (la porte n'a
qu'une seule issue mécaniquement atteignable, mesuré sur 18 fichiers et
confirmé par le registre : 14/14 approbations, 0 rejet, 14 secondes après
la fusion), P1-2 (les « points retenus » publiés sont des numéros de ligne,
pas des identifiants de défauts, avec précédent déjà sur disque au brief
014) et P2-2 (le décompte publié au registre diverge du décompte réel par
un bug de portée de regex précisément localisé, ligne 11, et la récurrence
n'est pas consommée faute d'entrée dans le registre) — sont tous les trois
CONFIRMED avec preuve rejouée indépendamment, pas seulement lus dans le
code.

Les deux réserves (points 2 et 6, classification CI et log du job
`regenerate`) ne sont pas des trous dans le texte de l'audit : elles
tiennent à l'absence de `GH_TOKEN` dans **cet** environnement d'exécution
de contre-audit — la même absence que P3-1 documente et que j'ai
personnellement heurtée en tentant `gh api`. Cela corrobore P3-1 plutôt que
de l'affaiblir : le fait qu'un second passage, dans un contexte différent,
retombe sur la même limitation est une preuve de plus que le défaut est
structurel et non ponctuel.

Recommandation : rien à rejeter techniquement. B-1 et B-2 sont mûrs pour
conversion en brief (le NEEDS_OWNER porte sur la priorité et la forme, pas
sur la réalité du défaut) ; B-3 mérite d'abord une relecture du journal CI
réel (avec `GH_TOKEN`) avant conversion, faute de quoi il resterait
lui-même une affirmation non rejouée — le même standard que l'audit
s'impose à lui-même en section 4 de son propre texte.
