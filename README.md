# Streaming Sources for Jellyfin

Plugin Jellyfin visant a proposer des sources de streaming externes pour les medias absents de la bibliotheque locale, via une API de recherche existante et un fournisseur Debrid comme AllDebrid.

L'objectif est de garder l'utilisateur dans l'interface Jellyfin : recherche de source, choix du torrent, mise en cache Debrid, puis lecture immediate dans le lecteur Jellyfin.

> Statut du projet : premiere base technique. Le depot contient un squelette de plugin Jellyfin 10.11.x / .NET 9, une page de configuration, les modeles principaux, un client API externe, une interface Debrid, une premiere implementation AllDebrid et des endpoints backend internes. L'integration complete du bouton `Sources` dans l'interface de lecture Jellyfin reste a implementer.

## Fonctionnement vise

### Media present localement

Si Jellyfin possede deja le fichier dans sa bibliotheque, le comportement standard reste inchange :

- bouton `Lecture`
- bouton `Sources`
- reprise de lecture
- sous-titres
- pistes audio
- historique et progression Jellyfin

Le bouton `Sources` reste disponible afin de permettre a l'utilisateur de choisir une source externe meme si un fichier local existe deja.

### Media absent localement

Si le media n'est pas disponible localement, le plugin utilisera le meme bouton :

- `Sources`
- ou `Trouver une source`

Au clic, le plugin interrogera une API REST externe deja existante. Cette API est responsable de la recherche torrent et peut utiliser sa propre base de donnees, Prowlarr ou toute autre source.

Le plugin ne doit pas implementer la recherche torrent lui-meme.

## Parcours utilisateur

1. L'utilisateur ouvre un film ou un episode dans Jellyfin.
2. Jellyfin affiche toujours le bouton `Sources`, que le fichier local existe ou non.
3. Si le media est local, le bouton `Lecture` reste disponible et conserve le comportement natif.
4. L'utilisateur clique sur `Sources` lorsqu'il veut chercher une source externe.
5. Le plugin affiche les resultats disponibles.
6. L'utilisateur choisit une source.
7. Le plugin envoie le magnet au fournisseur Debrid.
8. Le fournisseur Debrid retourne un lien HTTP streamable.
9. Jellyfin lance la lecture.
10. Le choix est mis en cache pour les lectures suivantes.

## Exemple de sources retournees

L'API externe pourra retourner des resultats de ce type :

- `2160p BluRay REMUX HDR VF`
- `2160p WEB-DL DV`
- `1080p BluRay x265`
- `1080p WEB-DL`
- `720p WEBRip`

Chaque resultat devrait contenir au minimum :

- nom
- taille
- seeders
- langue
- qualite
- codec
- informations HDR / Dolby Vision
- hash torrent
- magnet

## Architecture cible

Le projet doit rester modulaire afin de pouvoir ajouter plusieurs fournisseurs Debrid sans modifier le coeur du plugin.

### Backend Jellyfin

Responsabilites :

- communiquer avec l'API externe de recherche
- gerer la configuration du plugin
- gerer les utilisateurs et leurs droits
- gerer le cache des sources choisies
- appeler l'API AllDebrid
- generer ou transmettre l'URL de lecture au lecteur Jellyfin
- journaliser les erreurs et les etapes importantes

### Frontend Jellyfin

Responsabilites :

- ajouter le bouton `Sources` ou `Trouver une source`
- afficher une fenetre de selection
- afficher les etats : recherche, ajout Debrid, recuperation du lien, lecture
- permettre le changement de source
- lancer la lecture via les API officielles Jellyfin lorsque possible

### API externe

Responsabilites :

- identifier le media via IMDb, TMDb, TVDb ou les metadonnees Jellyfin disponibles
- rechercher les torrents dans la base existante
- interroger Prowlarr si necessaire
- retourner une liste normalisee de sources

Le plugin consomme cette API mais ne doit pas la remplacer.

## Interface Debrid

Une interface generique devra etre prevue pour faciliter l'ajout de nouveaux fournisseurs.

Exemple :

```csharp
public interface IDebridProvider
{
    Task<DebridMagnetResult> AddMagnetAsync(string magnet, CancellationToken cancellationToken);
    Task<IReadOnlyList<DebridFile>> GetFilesAsync(string magnetId, CancellationToken cancellationToken);
    Task<string> GetStreamingUrlAsync(string fileId, CancellationToken cancellationToken);
    Task<bool> IsCachedAsync(string hash, CancellationToken cancellationToken);
}
```

Fournisseurs envisages :

- AllDebrid
- Real-Debrid
- Premiumize
- TorBox
- Debrid-Link

## Cache

Le plugin devra memoriser la source selectionnee afin d'eviter les recherches et appels API inutiles.

Exemple de donnees a conserver :

- identifiant Jellyfin du media
- identifiants externes disponibles : IMDb, TMDb, TVDb
- saison et episode pour les series
- hash torrent
- fournisseur Debrid
- identifiant Debrid du magnet
- URL de streaming si elle est reutilisable
- date de creation
- date de derniere validation
- utilisateur concerne, si le cache doit etre isole par utilisateur

Un bouton `Changer de source` devra :

1. supprimer ou invalider l'entree de cache
2. relancer une recherche
3. permettre a l'utilisateur de choisir une autre source

## Series

Pour les episodes, la recherche devra inclure :

- nom de la serie
- annee si disponible
- numero de saison
- numero d'episode
- identifiants IMDb, TMDb ou TVDb si disponibles

Les resultats affiches doivent correspondre a l'episode demande. Les packs de saison peuvent etre pris en charge plus tard, mais le premier objectif est de fiabiliser la lecture episode par episode.

## Configuration prevue

La page de configuration Jellyfin devra proposer :

- URL de l'API externe
- cle API de l'API externe
- timeout des appels API
- fournisseur Debrid actif
- cle API AllDebrid
- taille maximale des resultats
- nombre maximal de resultats
- tri par defaut
- niveau de logs

Les secrets ne doivent jamais etre commites dans le depot.

## Securite

Ce projet doit etre developpe avec une attention particuliere a la securite.

Principes obligatoires :

- ne jamais stocker de cle API en clair dans le code source
- ne jamais afficher les cles API completes dans les logs
- masquer les tokens dans les erreurs et traces
- valider strictement les URLs configurees
- eviter les redirections arbitraires vers des domaines non approuves
- limiter les appels reseau inutiles
- appliquer des timeouts sur tous les appels externes
- gerer proprement les erreurs des fournisseurs Debrid
- ne pas exposer de magnet, hash ou URL Debrid a un utilisateur non autorise
- respecter les permissions Jellyfin de l'utilisateur courant

Pour un depot public, utilisez des valeurs factices dans les exemples :

```json
{
  "ExternalApiUrl": "https://api.example.invalid",
  "ExternalApiKey": "REPLACE_ME",
  "DebridProvider": "AllDebrid",
  "AllDebridApiKey": "REPLACE_ME"
}
```

## Installation developpement

Le plugin cible Jellyfin 10.11.x et .NET 9.

Compiler le plugin :

```powershell
dotnet restore
dotnet build Jellyfin.Plugin.StreamingSources.sln -c Release
```

Copier ensuite les fichiers generes dans un dossier de plugin Jellyfin, par exemple sous Windows :

```powershell
$pluginDir = "$env:LOCALAPPDATA\jellyfin\plugins\StreamingSources"
New-Item -ItemType Directory -Force -Path $pluginDir
Copy-Item ".\src\Jellyfin.Plugin.StreamingSources\bin\Release\net9.0\Jellyfin.Plugin.StreamingSources.dll" $pluginDir -Force
Copy-Item ".\src\Jellyfin.Plugin.StreamingSources\bin\Release\net9.0\Jellyfin.Plugin.StreamingSources.pdb" $pluginDir -Force
```

Redemarrer Jellyfin apres la copie.

## Configuration

Dans Jellyfin :

1. Ouvrir `Tableau de bord > Plugins`.
2. Ouvrir `Streaming Sources`.
3. Renseigner `URL API externe`.
4. Renseigner `Cle API externe` si votre API en demande une.
5. Selectionner `AllDebrid`.
6. Renseigner `Cle API AllDebrid`.
7. Ajuster `Timeout`, `Taille maximale`, `Nombre maximal de resultats` et `Tri par defaut`.
8. Enregistrer puis redemarrer Jellyfin si necessaire.

Pour le moment, le client API externe appelle l'endpoint suivant :

```text
POST {ExternalApiUrl}/sources/search
```

Le corps envoye correspond au modele `MediaLookupRequest`.

La reponse attendue est un tableau JSON de sources :

```json
[
  {
    "name": "1080p WEB-DL VF",
    "sizeBytes": 8500000000,
    "seeders": 120,
    "language": "VF",
    "quality": "1080p",
    "codec": "x265",
    "isHdr": false,
    "isDolbyVision": false,
    "hash": "TORRENT_HASH",
    "magnet": "magnet:?xt=urn:btih:..."
  }
]
```

## Endpoints backend internes

Endpoints exposes par le plugin :

- `POST /StreamingSources/Search` : recherche les sources via l'API externe.
- `POST /StreamingSources/Resolve` : envoie le magnet au fournisseur Debrid, recupere une URL de streaming et met la source en cache.
- `DELETE /StreamingSources/Cache/{jellyfinItemId}` : supprime le cache d'un media.

Ces endpoints sont proteges par l'authentification Jellyfin.

## Utilisation

Flux attendu apres installation :

1. Ouvrir Jellyfin.
2. Aller sur un film ou un episode.
3. Cliquer sur `Lecture` pour utiliser le fichier local si disponible.
4. Cliquer sur `Sources` pour chercher une source externe, que le fichier local existe ou non.
5. Choisir une source selon qualite, langue, taille et seeders.
6. Attendre la mise en cache Debrid.
7. La lecture demarre automatiquement.

Lors des lectures suivantes, le plugin devra reutiliser la source mise en cache.

Etat actuel : la partie backend est initialisee et compilable. Le bouton `Sources` dans les pages film/episode et le lancement direct dans le player Jellyfin restent les prochaines etapes.

## Developpement

Structure cible possible :

```text
src/
  Jellyfin.Plugin.StreamingSources/
    Plugin.cs
    Configuration/
    Controllers/
    Debrid/
    ExternalApi/
    Cache/
    Frontend/
tests/
  Jellyfin.Plugin.StreamingSources.Tests/
README.md
LICENSE
```

Priorites de developpement :

1. Squelette du plugin Jellyfin.
2. Page de configuration.
3. Client API externe.
4. Interface `IDebridProvider`.
5. Implementation AllDebrid.
6. Cache des sources.
7. Bouton frontend `Sources`.
8. Lecture via URL HTTP Debrid.
9. Tests unitaires des clients API et du cache.
10. Documentation d'installation complete.

## Logs

Les logs doivent aider au diagnostic sans exposer d'informations sensibles.

Informations utiles :

- media concerne
- fournisseur Debrid utilise
- nombre de resultats retournes
- temps de reponse API
- etape en cours
- erreur normalisee

Informations a ne pas logger :

- cles API
- tokens
- URLs Debrid completes si elles contiennent un token
- magnets complets si cela expose des donnees sensibles

## Limites et responsabilite

Ce plugin est concu pour connecter Jellyfin a une API externe et a un fournisseur Debrid configure par l'utilisateur.

L'utilisateur est responsable :

- de respecter les lois applicables dans son pays
- de n'utiliser que des sources auxquelles il a droit
- de respecter les conditions d'utilisation de Jellyfin, AllDebrid et des autres services configures

Ce repository ne fournit pas de contenu, ne telecharge pas de fichiers localement et n'a pas vocation a contourner les droits d'acces a des contenus proteges.

## Licence

Ce projet est distribue sous licence MIT. Voir [LICENSE](LICENSE).
