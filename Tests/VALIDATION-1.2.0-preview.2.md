# Validation de la calibration et du mapping

Version : **1.2.0-preview.2**. Exécution le 19 novembre 2026 sur Windows x64. Aucun Falcon ouvert, aucune commande envoyée au matériel.

| Contrôle | Résultat |
| --- | --- |
| Suite noyau / concurrence | 17 scénarios réussis, dont les 100 créations/arrêts existants |
| Transformations | Positions, vitesses, forces ; rotations, échelles non uniformes, inversion et cisaillement ; conservation du travail virtuel |
| Précision | Erreur aller-retour inférieure à 1 micromètre sur les cas numériques testés |
| Calibration | Trois axes requis, échantillons insuffisants, marges excessives et dépassements numériques refusés |
| Limites | Désarmement dans le worker hors boîte, refus d’armer hors boîte et absence de réarmement au retour |
| Compilation Unity | Runtime, éditeur et moniteur compilés en C# 9 / .NET Standard 2.1 contre 6000.3.24f1, sans avertissement |
| Compilation diagnostic | Réussie sans avertissement |
| Unity 6000.3.24f1 réel | Quatre configurations domaine/scène réussies depuis les sources, puis depuis le tarball final |
| Mapping dans Unity | Curseur sous un parent distinct, repère sous un parent tourné et non uniformément mis à l’échelle |
| Outils de calibration | Recentrage et changement de repère désarmés, capture sur trois axes, lecture d’un profil sérialisé et copie de calibration |
| Archive UPM | 44 fichiers comparés par SHA-256 aux sources, tous identiques |

Journaux locaux : `.validation/FalconConsolidation/workspace-validation.log` et `workspace-tarball-validation.log`. Chaque exécution contient quatre messages `FALCON PLAY PASS`, puis le message final de réussite ; code de sortie zéro. Le fichier de verrouillage UPM confirme l’import du tarball `1.2.0-preview.2`.

Les journaux comportent aussi des exceptions internes à `UnityEditor.Search.SearchDatabase` au démarrage de l’indexation et des messages Mono à la fermeture, comme lors de la validation précédente. Ces traces ne pointent pas vers le code Falcon ; les journaux ne sont pas entièrement exempts d’erreurs d’éditeur.

Archive : `Distribution/com.falcon.haptics-1.2.0-preview.2.tgz`.

SHA-256 : `DE56378AE925AADECB7643FB3C194FE78A612EADBD99031168577C7245FC6C86`.

## Limites de cette validation

L’armement réel via le focus de fenêtre, la force physique, les axes réels, l’initialisation, les limites mécaniques et les coupures après incident nécessitent encore un Falcon. Les Players Mono et IL2CPP de cette version, Unity 2021.3/2022.3 et les autres systèmes ne sont pas qualifiés ici. La géométrie accessible n’est pas établie par les extrema de calibration ; les coins de la boîte peuvent rester inaccessibles.

Le mapping ne modifie pas les six effets matériels existants. Les conversions de forces sont disponibles pour les futures interactions ; aucune surface haptique liée à un collider n’est encore ajoutée.
