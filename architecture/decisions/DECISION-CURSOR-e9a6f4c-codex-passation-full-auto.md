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

---

## Décision produit du propriétaire (2026-08-11) — les quatre arbitrages restants

Le contre-audit avait laissé quatre points ouverts, dont un que l'audit
d'origine ne voyait pas (le verrou de fusion). Le propriétaire les a
tranchés le 2026-08-11, chacun dans le sens recommandé. Enregistré ici pour
qu'un futur brief parte d'un énoncé écrit, et non d'un souvenir de
conversation. Un brief ne doit pas paraphraser cette section : il la lit.

### 1. Verrou de fusion → **porte conditionnelle**

L'auto-fusion est autorisée seulement si **quatre** preuves sont réunies :
CI verte, gate mécanique ACCEPT, verdict d'un Évaluateur dont l'acteur
diffère du producteur, et audit Cursor déposé sur la pull request.

Ce que cela veut dire, et ne veut pas dire : le clic du propriétaire est
remplacé par des conditions vérifiables, il n'est pas supprimé au profit du
vide. La denylist actuelle n'est **pas** élargie telle quelle — elle est
remplacée par une porte plus exigeante. La protection de branche restant
indisponible sur ce plan GitHub (`HTTP 403`, vérifié le 2026-08-11), aucune
étape de cette porte ne peut être rendue facultative sans une nouvelle
décision écrite.

### 2. Plafond budgétaire → **plafond natif ET marquage post-hoc**

L'appel headless du maillon challenge passe `--max-budget-usd 5`, qui coupe
avant que la dépense ait lieu ; le marquage post-hoc du lot 009b garde la
trace de ce qui a été dépensé. Les deux, pas l'un ou l'autre.

Cette décision annule l'hypothèse de planification selon laquelle aucun
plafond natif n'existait — hypothèse démentie par la sortie réelle de
`claude --help` conservée dans
`harness/queue/briefs/009-full-auto-agent-invocation/deliverables/claude-help-budget-excerpt.txt`.

### 3. Ordre de câblage des deux maillons restants → **`cursor-auditor` d'abord**

`pipeline-audit.yml` avant `pipeline-forge-run.yml`. Trois raisons, dans
l'ordre : c'est la demande explicite du propriétaire (Cursor auditeur de
chaque PR) ; c'est le maillon le moins cher et le seul qui n'écrive pas de
code ; et c'est un **prérequis** de la porte conditionnelle décidée au
point 1, qui exige un audit Cursor déposé.

Conséquence à ne pas contourner : câbler `forge-run` avant l'auditeur
reviendrait à produire du code sans relecture automatique, et rendrait la
porte du point 1 inapplicable faute d'une de ses quatre preuves.

### 4. Hermes → **contrat d'écriture dans le dépôt**

Hermes dépose ses briefs de suivi dans un dossier dédié et versionné du
dépôt, sous un format imposé et avec un auteur traçable. Il reste
observateur : un rapport est une **entrée**, jamais une instruction, et
Hermes n'acquiert aucun droit d'implémentation.

État de départ à ne pas ignorer quand le brief sera écrit : Hermes produit
déjà des rapports quotidiens et hebdomadaires et sert un tableau de bord sur
`http://127.0.0.1:9119`, entièrement hors dépôt, et se déclare en phase
« shadow » jusqu'au 2026-08-24 dans sa propre configuration. La question
n'est donc pas de lui apprendre à produire des rapports — il le fait — mais
de définir où ils atterrissent, sous quelle forme, et comment on prouve qui
les a écrits.

### Ce que ces quatre décisions ne tranchent pas

Elles ne disent rien du contenu des briefs qui les mettront en œuvre, ni de
leur ordre par rapport aux lots 009a, 009c et 010a-c déjà en cours. Elles ne
lèvent aucun blocage existant : le lot 009a reste rejeté et le lot 009c
reste bloqué tant qu'il ne l'est plus.
