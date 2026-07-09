# Streaming Sources for Jellyfin

Plugin Jellyfin permettant de chercher et lire des sources externes Debrid directement depuis l'interface Jellyfin.

Le but est simple : rester dans Jellyfin, cliquer sur `Sources`, choisir une version, puis lancer la lecture sans telechargement local.

> Statut : prototype installable pour Jellyfin `10.11.10` / `.NET 9`. Le plugin est en cours de developpement actif. Certaines fonctions, notamment l'integration complete au player natif Jellyfin, restent experimentales.

## Fonctionnalites

- Bouton `Sources` sur les pages film et episode.
- Compatible avec des manifests Stremio/Lumio pour recuperer des streams.
- Compatible avec une API externe de recherche de sources.
- Support initial AllDebrid.
- Cache de la source choisie par media.
- Affichage des infos utiles quand elles sont disponibles : qualite, langue, codec, taille, provider, web-ready.
- Page de configuration Jellyfin.
- Injection web via le plugin communautaire `File Transformation`.

## Securite

Ne committez jamais :

- URL de manifest personnelle.
- cle API AllDebrid, Real-Debrid, Prowlarr, Lumio ou autre.
- token Jellyfin.
- logs Jellyfin contenant des tokens.
- URL Debrid complete si elle contient un jeton d'acces.

Utilisez toujours des exemples neutres :

```text
https://example.invalid/your-addon/manifest.json
```

Si une URL personnelle a deja ete publiee dans l'historique Git, regenerez-la cote service si possible.

## Installation Depuis Le Depot Jellyfin

Dans Jellyfin :

1. Ouvrir `Tableau de bord > Plugins > Repositories`.
2. Ajouter un depot.
3. Nom : `Streaming Sources`.
4. URL :

```text
https://raw.githubusercontent.com/Zilang59/Direct_view/main/manifest.json
```

5. Sauvegarder.
6. Aller dans `Catalogue`.
7. Installer `Streaming Sources`.
8. Redemarrer Jellyfin.

## Dependances

Pour afficher le bouton `Sources` dans Jellyfin Web, installez aussi :

- `File Transformation`

Apres installation ou mise a jour :

1. Redemarrer Jellyfin.
2. Vider le cache navigateur avec `Ctrl+F5`.
3. Ouvrir une page film ou episode.

## Configuration

Dans `Tableau de bord > Plugins > Streaming Sources` :

### Mode Manifest Stremio/Lumio

Activez :

- `Activer les manifests Stremio/Lumio`

Puis renseignez un manifest par ligne :

```text
https://example.invalid/user-or-addon/manifest.json
```

Pour laisser le manifest gerer lui-meme les filtres :

- `Nombre maximal de resultats` : `0`
- `Taille maximale` : `0`

### Mode API Externe

Activez :

- `Activer API externe`

Puis renseignez :

- URL API externe
- cle API externe, si necessaire

Le plugin appelle :

```text
POST {ExternalApiUrl}/sources/search
```

### Debrid

Pour AllDebrid :

- Fournisseur Debrid : `AllDebrid`
- Cle API AllDebrid : votre cle personnelle

Ne partagez jamais cette cle.

## Utilisation

1. Ouvrir Jellyfin.
2. Aller sur un film ou un episode.
3. Cliquer sur `Sources`.
4. Choisir une source selon qualite, langue, taille et provider.
5. Le plugin resout la source.
6. La lecture est lancee.

Le bouton `Lecture` natif reste disponible pour les fichiers locaux.

## Etat Du Player

L'objectif final est que la source Debrid soit lue comme une source Jellyfin native :

- progression
- reprise
- transcodage
- sous-titres
- pistes audio
- historique

Etat actuel :

- Les sources peuvent etre recherchees et resolues.
- Les URL directes peuvent etre lancees.
- Une premiere integration `IMediaSourceProvider` est presente pour exposer les sources cachees a Jellyfin.
- L'integration complete au player natif Jellyfin est encore experimentale.

## API De Recherche Attendue

Une source normalisee ressemble a ceci :

```json
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
  "magnet": "magnet:?xt=urn:btih:...",
  "directUrl": ""
}
```

Le plugin ne fait pas lui-meme la recherche torrent. Il consomme soit :

- un manifest compatible Stremio/Lumio
- une API externe deja existante

## Developpement

Pre-requis :

- .NET SDK 9
- Jellyfin 10.11.10 pour les tests

Compiler :

```powershell
dotnet restore
dotnet build -c Release
```

Creer un package :

```powershell
.\build\package.ps1 -Version 0.2.25
```

Les zips de test sont publies dans `packages/` parce que le manifest Jellyfin pointe dessus.

## Structure

```text
src/Jellyfin.Plugin.StreamingSources/
  Cache/
  Configuration/
  Controllers/
  Debrid/
  ExternalApi/
  Models/
  Playback/
  Web/
```

## Logs

Les logs utiles doivent indiquer :

- nombre de sources trouvees
- provider utilise
- etape en cours
- erreurs normalisees

Les logs ne doivent pas exposer :

- cles API
- tokens
- manifests personnels
- liens Debrid complets
- magnets complets

## Roadmap

- Stabiliser l'integration au player Jellyfin natif.
- Ajouter `Changer de source`.
- Persister le cache au-dela du redemarrage.
- Isoler le cache par utilisateur si necessaire.
- Ajouter Real-Debrid, Premiumize, TorBox, Debrid-Link.
- Ajouter des tests automatises.
- Publier des releases GitHub propres.

## Responsabilite

Ce plugin ne fournit aucun contenu. Il permet uniquement de connecter Jellyfin a des sources et services configures par l'utilisateur.

L'utilisateur est responsable de respecter les lois applicables, les conditions d'utilisation des services configures et les droits d'acces aux contenus.

## Licence

MIT. Voir [LICENSE](LICENSE).
