# Porte conditionnelle de fusion — spécification inactive

État au 2026-08-11 : **spécifiée, non câblée**. Aucun workflow ne lit ce
document et ce lot ne modifie rien sous `.github/workflows/`.

## Ce qui bloque aujourd'hui

La chaîne vise : production du lot → CI → gate mécanique → verdict
indépendant → audit Cursor → fusion. Les quatre premières preuves peuvent
exister, mais `.github/workflows/merge-bot.yml` ne tente une auto-fusion que
pour une branche `cursor/` ou `forge-bot/` dont tous les chemins appartiennent
à sa petite allowlist documentaire.

Une PR de code issue d'une branche `codex/` ou `forge/` ne franchit donc même
pas le `if:` du job. L'étape humaine exacte qui subsiste est : **le
propriétaire clique “Merge pull request” dans GitHub (ou lance lui-même
`gh pr merge`) après avoir relu les preuves**. Aucun workflow du dépôt ne
remplace actuellement ce clic pour une PR de code.

## Porte qui pourrait remplacer ce clic

Une future implémentation part du SHA de tête de la PR et du chemin de brief
déclaré une seule fois dans son corps sous la forme
`Forge-Brief: harness/queue/briefs/<id>/`. Une valeur absente, multiple, hors
du dépôt ou ne pointant pas vers un brief suivi par Git bloque la fusion.

La fusion n'est permise que si les quatre prédicats suivants sont vrais au
même SHA :

1. **CI verte.** Lire `gh pr checks <numéro> --json name,bucket,workflow`.
   Les deux jobs `harness-ci / tests` et `harness-ci / f0-demo` doivent être
   présents. Il doit exister au moins un contrôle, tous les contrôles renvoyés
   doivent avoir `bucket=pass`, et aucun ne peut être en échec, en attente,
   annulé ou ignoré.
2. **Gate mécanique ACCEPT.** Sur un checkout propre du SHA de tête, exécuter
   `py harness/verdict_audit.py <Forge-Brief>`. La preuve lue est conjointe :
   code de retour `0`, dix lignes `[PASS]`, dernière ligne
   `VERDICT: ACCEPT`. Une sortie conservée d'un autre SHA ne compte pas.
3. **Verdict indépendant ACCEPT.** Lire le `verdict.md` du même brief et la
   section correspondant au lot de la PR : elle doit porter explicitement
   `Verdict: ACCEPT`. Lire aussi les auteurs du lot dans
   `deliverables/generator-log.md` et `verdict.md`; la ligne
   `verdict_is_not_self_authored` du gate doit être `[PASS]` et les acteurs
   doivent être identifiables sans ambiguïté. Un acteur absent ou inconnu
   bloque au lieu d'être deviné.
4. **Audit Cursor déposé.** Trouver exactement un fichier suivi sous
   `architecture/inbox/CURSOR-*.md` dont le frontmatter porte
   `auditor: cursor-cloud` et `target_commit: <SHA de tête complet>`. Le
   fichier doit passer `py harness/audit_schema.py`; un audit visant un SHA
   antérieur ou une simple mention de Cursor dans la PR ne compte pas.

Chaque lecture est refaite immédiatement avant la tentative de fusion. Si le
SHA de tête change, les quatre preuves sont invalidées et doivent être
reconstruites. Un prédicat faux, absent ou illisible laisse la PR ouverte et
produit un refus visible ; il ne devient jamais une dérogation implicite.

## Limite de ce lot

Cette spécification ne crée aucun job, n'élargit aucun préfixe ni chemin, et
n'appelle pas `gh pr merge`. L'activation exige un lot ultérieur qui traduise
exactement ces lectures en code et fasse l'objet de sa propre évaluation.
