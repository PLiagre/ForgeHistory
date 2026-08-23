---
author: hermes
kind: demande
created_at: 2026-08-14T12:00:00Z
concerns: projet
status: CLOSED
---
# Tester un workflow Hermes, Claude Code et Cursor simplifié

Le propriétaire valide la simplification suivante : Hermes pilote sans modèle
local lourd, Claude Code prépare le plan et relit le résultat via l'abonnement
Pro actif jusqu'en avril 2027, Cursor est le seul agent qui modifie le code,
les tests mécaniques restent autoritaires et la fusion reste humaine pendant
le pilote.

Le pilote doit être neuf, lisible et réversible. L'ancien full-auto passe en
mode manuel. L'observateur Windows local est suspendu. Aucun cron et aucun
auto-merge ne sont ajoutés avant un bilan de trois lots réels.

Hermes sait exposer un serveur ACP mais ne possède pas encore de client ACP
générique. Claude Code est donc lancé avec son CLI headless. Claude Pro ne peut
pas servir de provider Anthropic natif à Hermes ; le CLI `claude -p` est la
frontière retenue pour profiter de l'abonnement sans facturation API.

Correction propriétaire : Unity est installé nativement sous Windows. Le pilote
local garde donc Windows démarré et utilise WSL2 seulement si nécessaire ; il ne
dépend plus du double démarrage Linux. Après trois lots seulement, un bilan
décide d'une éventuelle migration vers un VPS 4 Go/2 vCPU/40 Go.

Le VPS éventuel porte Hermes et ForgePilot. Le PC Windows reste le worker Unity.
Lorsqu'il est éteint, les tâches Unity attendent sans bloquer Hermes, mais leur
fusion reste interdite. Render n'est pas retenu pour héberger Hermes.
