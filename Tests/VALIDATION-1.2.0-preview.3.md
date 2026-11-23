# Première surface haptique — validation de 1.2.0-preview.3

Exécutée le 19 novembre 2026 sur Windows x64. Aucun Falcon ouvert et aucune force envoyée au matériel.

| Contrôle | Résultat |
| --- | --- |
| Suite noyau et concurrence | 21 scénarios réussis, incluant les régressions de calibration et les 100 créations/arrêts de worker |
| Contact de surface | Force répulsive, plafond, montée limitée, zéro hors contact, atténuation aux bords, démarrage derrière la face refusé et pénétration excessive rejetée |
| Amortissement | Opposition à l’entrée sans force attractive à la sortie |
| Fraîcheur et mutation | Expiration de la géométrie avec signal de vie applicatif actif, désarmement sur modification et refus après suppression |
| Export Unity | Six faces, centres et tailles, transformations et inversion du mapping ; sens de normale et force répulsive vérifiés |
| Propriété de surface | Surface exclusive, remplacement après désactivation et refus des Rigidbody cinématiques |
| Compilation | Runtime, éditeur et exemples compilés en C# 9 / .NET Standard 2.1 contre Unity 6000.3.24f1, sans avertissement ; diagnostic Windows compilé sans avertissement |
| Play Mode depuis le tarball final | Quatre configurations domaine/scène réussies ; publication de surface et préparation d’armement vérifiées, sans activation de moteur |
| Scène distribuée | Ouverte sans script manquant ; références, géométrie et sélection du backend Simulation vérifiées |
| Archive UPM | 56 fichiers comparés par SHA-256 aux sources, tous identiques |

Journaux locaux dans `.validation/FalconConsolidation` : `surface-validation.log`, `surface-tarball-validation.log` et `surface-demo-import.log`. Les exports, les quatre configurations de Play et l’ouverture de la scène ont terminé avec le code zéro. L’import UPM final est de type `local-tarball` pour la version 1.2.0-preview.3.

Comme pour les étapes précédentes, les journaux de Play comportent des exceptions internes à `UnityEditor.Search.SearchDatabase` et des messages Mono à la fermeture. Les piles de ces messages ne pointent pas vers le SDK Falcon. Les journaux ne sont donc pas intégralement exempts d’erreurs de l’éditeur.

Archive : `Distribution/com.falcon.haptics-1.2.0-preview.3.tgz`.

SHA-256 : `F006EB2BADD083600335FF3C2FD8577F507E58BFA61573A55C50BAD1AF548CE2`.

## Périmètre et limites

Une seule face finie et une seule surface par Falcon ; le contact utilise une pénalité souple avec rayon d’outil, sans proxy contraint. Les arêtes, autres faces, friction, objets mobiles et contacts multiples restent hors périmètre. L’ajout d’une surface n’additionne pas ses forces aux six modes historiques.

La rigidité et l’amortissement sont calculés en unités physiques après conversion de la géométrie, sans nouvelle amplification d’échelle de la force. La voie surface utilise le plafond matériel logiciel du ressort historique (1,2 N), avec un plafond configurable inférieur et un gain utilisateur. Ces limites ne constituent pas une qualification matérielle.

L’interaction via la vue Game et son focus reste à essayer manuellement. Les forces réelles, la stabilité du contact, les axes, la cadence USB, les coupures après incident et le watchdog demandent un Falcon. Aucun nouveau Player Mono ou IL2CPP n’est qualifié ici. Une géométrie expirée désarme seulement si le worker reste opérationnel ; elle ne protège pas contre un appel DHD bloqué.
