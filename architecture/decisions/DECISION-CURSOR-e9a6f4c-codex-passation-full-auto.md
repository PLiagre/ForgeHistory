---
decision_of: CURSOR-e9a6f4c-codex-passation-full-auto
decided_by: owner
verdict: APPROVED
retained_points: [3, 6, 7, 8, 10, 11, 12, 15, 16]
---

# Décision sur CURSOR-e9a6f4c-codex-passation-full-auto

**Verdict : APPROVED**

## Raison

Decision du proprietaire du 2026-08-11, en reponse directe aux cinq questions de la section 8 de l audit. Objectif fixe: un workflow entierement automatise, sans aucune action du proprietaire. Repartition arretee: Codex est le developpeur du projet ET doit pouvoir remplacer Claude lorsque Claude atteint son plafond de credit (option B de la section 4.1: session distincte declenchee par un tiers, jamais par le producteur -- l option C, sous-agent engendre par le Generateur, est ecartee). Cursor reste auditeur externe de CHAQUE pull request. Hermes est observateur et produit des briefs de suivi et des tableaux de bord montrant l avancement. Claude reste Planificateur et Evaluateur par defaut. Points non retenus et pourquoi: point 1, le score 20/24 de harness_audit ne se reproduit pas (23/24 mesure sur la machine du proprietaire) -- chiffre d environnement, pas fait du depot; point 2, le constat central est exact mais depasse par c9e9291 qui a rejuge l iteration 2 (verdict toujours REJECT, sur quatre defauts neufs C1 a C4, donc le blocage subsiste pour d autres raisons); point 4 retenu via le point 6; point 9, affirmation produit OpenAI invérifiable depuis ce depot, ni retenue ni contestee; point 14, un prompt n est ni vrai ni faux. Reserve inscrite par le contre-audit et retenue: le verrou de fusion (O5) n est couvert par aucun point de l audit. merge-bot.yml n auto-fusionne que les branches cursor/ et forge-bot/ et uniquement sur des chemins documentaires, donc aucune PR de code n est auto-fusionnable et une branche codex/ ne l est jamais. La conversion en briefs doit traiter ce verrou explicitement, comme une question posee au proprietaire et non comme un elargissement silencieux de la denylist, qui reste la seule barriere reelle puisque la protection de branche est indisponible sur ce plan GitHub (403 verifie).

## Points retenus

3, 6, 7, 8, 10, 11, 12, 15, 16
