# Première surface en simulation

Importer **Surface Demo** depuis les Samples du package, puis ouvrir `FalconSurfaceDemo.unity` et lancer Play. Cette scène sélectionne le backend Simulation ; elle ne charge pas le Falcon.

Lors d’une mise à jour d’un sample déjà importé, ne conserver qu’une copie de son script `FalconSurfaceDemo` dans les Assets, afin d’éviter des classes en double.

1. Laisser le bouton simulé relâché et le curseur à Z = +6 mm.
2. Dans la vue Game, cliquer sur **Activer la surface à 30 %**.
3. Cocher **Maintenir le bouton 1 simulé**, puis réduire Z vers +2 mm et au-delà. La force commence lorsque la sphère de rayon 2 mm rencontre la face, située à Z brut = 0.
4. Déplacer X ou Y pour observer l’atténuation près du bord puis l’absence de force hors de la face. Le relâchement du bouton arrête l’effort.
5. Déplacer X ou Y en contact pour observer la friction ; la démonstration utilise un coefficient de 0,5. Le **pic de friction sur cette prise** mémorise les commandes brèves, sans représenter une force encore active.
6. Décocher **Friction dynamique** pour comparer sans friction. Le changement désarme : replacer devant, réarmer puis maintenir le bouton.
7. **Replacer devant la face** coupe les forces et remet la position de départ.

Le champ **Force calculée** affiche la commande simulée en newtons matériels. Le curseur montre la position mesurée et peut pénétrer la paroi : ce prototype utilise une surface souple, sans proxy géométrique contraint. Les réglages par défaut du composant limitent la force à 0,6 N à 100 %, donc 0,18 N à 30 %. Ce sont des choix logiciels de démonstration, pas des limites matérielles qualifiées.

Une seule face du cube est haptique : **Negative Z** dans la scène, orientée vers Z positif dans le repère brut du Falcon après inversion du mapping. Les autres faces et les arêtes ne produisent pas de contact. La friction dynamique s’oppose au glissement, devient nulle à l’arrêt et partage le plafond avec le contact normal. Elle n’ajoute pas d’adhérence statique et n’augmente pas le plafond de force.

La scène démarre devant la face. Activer le bouton alors que l’outil est déjà derrière celle-ci provoque un refus et un désarmement. Pour reprendre, relâcher le bouton, replacer l’outil devant et activer à nouveau.

Les modifications de géométrie, du mapping et des paramètres désarment l’interaction. Les Rigidbody, même cinématiques, sont refusés. L’absence de publication de la géométrie pendant 100 ms désarme aussi la surface. La stabilité, les axes et la coupure réelles nécessitent une validation séparée sur un Falcon.
