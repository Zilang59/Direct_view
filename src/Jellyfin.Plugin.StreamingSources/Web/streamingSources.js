(function () {
    'use strict';

    const buttonId = 'streamingSourcesButton';
    const dialogId = 'streamingSourcesDialog';
    const styleId = 'streamingSourcesStyle';
    const debugPrefix = '[Streaming Sources]';
    let lastUrl = '';

    function debug(message, data) {
        if (data === undefined) {
            console.debug(debugPrefix, message);
            return;
        }

        console.debug(debugPrefix, message, data);
    }

    function apiClient() {
        return window.ApiClient || window.ConnectionManager?.currentApiClient?.();
    }

    function getItemId() {
        const hashQuery = window.location.hash.split('?')[1] || '';
        const searchParams = new URLSearchParams(window.location.search);
        const hashParams = new URLSearchParams(hashQuery);
        return searchParams.get('id') || hashParams.get('id') || hashParams.get('itemId');
    }

    function isDetailPage() {
        return Boolean(getItemId()) && /details|itemdetails|item/i.test(window.location.href);
    }

    function ensureStyles() {
        if (document.getElementById(styleId)) {
            return;
        }

        const style = document.createElement('style');
        style.id = styleId;
        style.textContent = `
            .streaming-sources-button {
                margin-left: .5em;
            }
            .streaming-sources-icon-button {
                width: auto;
                min-width: 0;
                height: auto;
                margin: 0 .35em;
                padding: .65em;
                border: 0;
                border-radius: 50%;
                background: transparent !important;
                box-shadow: none !important;
                color: inherit;
                opacity: .9;
            }
            .streaming-sources-icon-button:hover,
            .streaming-sources-icon-button:focus {
                background: rgba(255,255,255,.12) !important;
                opacity: 1;
            }
            .streaming-sources-icon-button .material-icons,
            .streaming-sources-icon-button .material-icons-round {
                font-size: 1.75em;
                line-height: 1;
            }
            .streaming-sources-floating-button {
                position: fixed;
                right: 1.25rem;
                bottom: 1.25rem;
                z-index: 9999;
                box-shadow: 0 8px 24px rgba(0,0,0,.35);
            }
            .streaming-sources-overlay {
                position: fixed;
                inset: 0;
                z-index: 99999;
                background: rgba(0, 0, 0, .72);
                display: flex;
                align-items: center;
                justify-content: center;
                padding: 1rem;
            }
            .streaming-sources-modal {
                width: min(760px, 96vw);
                max-height: 86vh;
                overflow: auto;
                background: var(--theme-body-background, #202020);
                color: inherit;
                border-radius: 8px;
                box-shadow: 0 12px 36px rgba(0,0,0,.45);
                padding: 1.25rem;
            }
            .streaming-sources-header {
                display: flex;
                align-items: center;
                justify-content: space-between;
                gap: 1rem;
                margin-bottom: 1rem;
            }
            .streaming-sources-list {
                display: grid;
                gap: .6rem;
            }
            .streaming-source-row {
                display: grid;
                grid-template-columns: 1fr auto;
                gap: .75rem;
                align-items: center;
                padding: .75rem;
                border: 1px solid rgba(255,255,255,.16);
                border-radius: 8px;
                background: rgba(255,255,255,.05);
            }
            .streaming-source-name {
                font-weight: 600;
            }
            .streaming-source-meta {
                opacity: .78;
                font-size: .9em;
                margin-top: .25rem;
            }
            .streaming-sources-error {
                color: #ffb4ab;
                white-space: pre-wrap;
            }
            .streaming-sources-player {
                position: fixed;
                inset: 0;
                z-index: 100000;
                background: #000;
                display: grid;
                grid-template-rows: auto 1fr;
            }
            .streaming-sources-player-bar {
                display: flex;
                align-items: center;
                justify-content: space-between;
                gap: 1rem;
                padding: .75rem 1rem;
                background: rgba(0,0,0,.82);
            }
            .streaming-sources-video {
                width: 100%;
                height: 100%;
                background: #000;
            }
        `;
        document.head.appendChild(style);
    }

    function jellyfinButton(text, className) {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = className || 'raised button-submit emby-button';
        button.innerHTML = `<span>${text}</span>`;
        return button;
    }

    function jellyfinIconButton(icon, label) {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'emby-button detailButton autoSize paper-icon-button-light streaming-sources-button streaming-sources-icon-button';
        button.title = label;
        button.setAttribute('aria-label', label);
        button.innerHTML = `<span class="material-icons-round material-icons" aria-hidden="true">${icon}</span>`;
        return button;
    }

    function findPlayButton() {
        const candidates = Array.from(document.querySelectorAll('button, .emby-button'));
        return candidates.find(button => {
            const label = [
                button.getAttribute('title'),
                button.getAttribute('aria-label'),
                button.textContent,
                button.className
            ].filter(Boolean).join(' ').toLowerCase();

            return /\b(play|lecture|resume|reprendre|btnplay)\b/.test(label);
        }) || null;
    }

    function findButtonContainer() {
        const playButton = findPlayButton();
        if (playButton?.parentElement) {
            return playButton.parentElement;
        }

        return document.querySelector('.mainDetailButtons') ||
            document.querySelector('.detailButtonContainer') ||
            document.querySelector('.detailButtons') ||
            document.querySelector('.itemDetailButtons') ||
            document.querySelector('.detailPagePrimaryContainer .buttons') ||
            document.querySelector('.itemDetailPage .buttons') ||
            document.querySelector('.detailPageContent .buttons') ||
            document.querySelector('.detailPageWrapper .buttons') ||
            document.body;
    }

    function isElementVisible(element) {
        if (!element || !document.body.contains(element)) {
            return false;
        }

        const rect = element.getBoundingClientRect();
        const style = window.getComputedStyle(element);
        return rect.width > 0 &&
            rect.height > 0 &&
            style.display !== 'none' &&
            style.visibility !== 'hidden' &&
            Number(style.opacity || '1') > 0;
    }

    function moveButtonToFallback(button) {
        button.className = 'raised button-submit emby-button streaming-sources-button streaming-sources-floating-button';
        button.innerHTML = '<span>Sources</span>';
        document.body.appendChild(button);
        debug('Sources button moved to floating fallback', { href: window.location.href });
    }

    function formatSize(bytes) {
        if (!bytes) {
            return 'taille inconnue';
        }

        return `${(bytes / 1024 / 1024 / 1024).toFixed(2)} Go`;
    }

    function normalizeSourcesResponse(response) {
        debug('Raw search response', response);

        if (!response) {
            return [];
        }

        if (typeof response === 'string') {
            try {
                return normalizeSourcesResponse(JSON.parse(response));
            } catch {
                throw new Error(response);
            }
        }

        if (Array.isArray(response)) {
            return response;
        }

        for (const key of ['sources', 'Sources', 'items', 'Items', 'results', 'Results', 'streams', 'Streams', 'data', 'Data', '$values']) {
            if (Array.isArray(response[key])) {
                debug(`Using response.${key}`, response[key]);
                return response[key];
            }
        }

        for (const key of ['result', 'Result', 'value', 'Value', 'payload', 'Payload']) {
            if (response[key]) {
                return normalizeSourcesResponse(response[key]);
            }
        }

        const values = Object.values(response);
        if (values.length > 0 && values.every(value => value && typeof value === 'object')) {
            debug('Using object values as source list', values);
            return values;
        }

        if (response.source || response.Source || response.name || response.Name) {
            return [response.source || response.Source || response];
        }

        const keys = Object.keys(response).join(', ') || 'aucune cle';
        console.warn(debugPrefix, 'Unexpected search response shape', response);
        throw new Error(`Format de reponse sources inattendu. Cles recues: ${keys}`);
    }

    async function parseResponsePayload(response) {
        if (!response || typeof response !== 'object' || typeof response.text !== 'function') {
            return response;
        }

        if (!response.ok) {
            throw new Error(await response.text());
        }

        if (response.status === 204) {
            return null;
        }

        const text = await response.text();
        if (!text) {
            return null;
        }

        try {
            return JSON.parse(text);
        } catch {
            return text;
        }
    }

    async function jellyfinFetch(path, options) {
        const client = apiClient();
        if (!client) {
            throw new Error('ApiClient Jellyfin introuvable.');
        }

        if (typeof client.ajax === 'function') {
            const ajaxResponse = await client.ajax(Object.assign({
                type: options.method || 'GET',
                url: client.getUrl(path),
                contentType: 'application/json'
            }, options.body ? { data: JSON.stringify(options.body) } : {}));
            return parseResponsePayload(ajaxResponse);
        }

        const token = client.accessToken && client.accessToken();
        const response = await fetch(client.getUrl(path), {
            method: options.method || 'GET',
            headers: {
                'Content-Type': 'application/json',
                'X-Emby-Token': token || ''
            },
            body: options.body ? JSON.stringify(options.body) : undefined
        });

        if (!response.ok) {
            throw new Error(await response.text());
        }

        return parseResponsePayload(response);
    }

    async function getItem(itemId) {
        const client = apiClient();
        const userId = client.getCurrentUserId ? client.getCurrentUserId() : window.ApiClient?._serverInfo?.UserId;

        if (client.getItem && userId) {
            return client.getItem(userId, itemId);
        }

        return jellyfinFetch(`/Users/${userId}/Items/${itemId}`, { method: 'GET' });
    }

    function buildLookup(item) {
        const providerIds = item.ProviderIds || {};
        return {
            jellyfinItemId: item.Id,
            title: item.SeriesName || item.Name,
            year: item.ProductionYear || null,
            imdbId: providerIds.Imdb || providerIds.IMDB || null,
            tmdbId: providerIds.Tmdb || providerIds.TMDB || null,
            tvdbId: providerIds.Tvdb || providerIds.TVDB || null,
            seasonNumber: item.ParentIndexNumber || null,
            episodeNumber: item.IndexNumber || null
        };
    }

    function closeDialog() {
        document.getElementById(dialogId)?.remove();
    }

    function showDialog(title, bodyBuilder) {
        closeDialog();
        ensureStyles();

        const overlay = document.createElement('div');
        overlay.id = dialogId;
        overlay.className = 'streaming-sources-overlay';

        const modal = document.createElement('div');
        modal.className = 'streaming-sources-modal';

        const header = document.createElement('div');
        header.className = 'streaming-sources-header';

        const heading = document.createElement('h2');
        heading.textContent = title;

        const close = jellyfinButton('Fermer', 'emby-button');
        close.addEventListener('click', closeDialog);

        header.append(heading, close);
        modal.appendChild(header);
        bodyBuilder(modal);
        overlay.appendChild(modal);
        document.body.appendChild(overlay);
    }

    function showMessage(title, message, isError) {
        showDialog(title, modal => {
            const text = document.createElement('div');
            text.className = isError ? 'streaming-sources-error' : '';
            text.textContent = message;
            modal.appendChild(text);
        });
    }

    async function tryJellyfinPlayback(item, streamingUrl) {
        const playbackManager = window.PlaybackManager || window.playbackManager;
        debug('Playback globals', {
            hasPlaybackManager: Boolean(playbackManager),
            playbackMethods: playbackManager ? Object.keys(playbackManager).filter(key => typeof playbackManager[key] === 'function') : []
        });

        if (!playbackManager || typeof playbackManager.play !== 'function') {
            return false;
        }

        const mediaSourceId = `streaming-sources-${item.Id || getItemId()}`;
        const remoteItem = Object.assign({}, item, {
            MediaSources: [{
                Id: mediaSourceId,
                Name: 'Streaming Sources',
                Path: streamingUrl,
                Protocol: 'Http',
                Type: 'Default',
                IsRemote: true,
                SupportsDirectPlay: true,
                SupportsDirectStream: true,
                SupportsTranscoding: false,
                RunTimeTicks: item.RunTimeTicks || null,
                MediaStreams: item.MediaStreams || []
            }]
        });

        try {
            await playbackManager.play({
                items: [remoteItem],
                mediaSourceId,
                startPositionTicks: 0
            });
            return true;
        } catch (error) {
            console.warn(debugPrefix, 'Jellyfin PlaybackManager refused the external source', error);
            return false;
        }
    }

    function triggerNativePlayback() {
        const playButton = findPlayButton();
        if (!playButton) {
            return false;
        }

        debug('Triggering Jellyfin native play button');
        playButton.click();
        return true;
    }

    function playInEmbeddedPlayer(item, streamingUrl) {
        document.getElementById('streamingSourcesPlayer')?.remove();

        const player = document.createElement('div');
        player.id = 'streamingSourcesPlayer';
        player.className = 'streaming-sources-player';

        const bar = document.createElement('div');
        bar.className = 'streaming-sources-player-bar';

        const title = document.createElement('div');
        title.textContent = item.Name || item.SeriesName || 'Streaming Sources';

        const video = document.createElement('video');
        video.className = 'streaming-sources-video';
        video.src = streamingUrl;
        video.controls = true;
        video.autoplay = true;
        video.playsInline = true;

        const close = jellyfinButton('Fermer', 'emby-button');
        close.addEventListener('click', () => {
            video.pause();
            player.remove();
        });

        bar.append(title, close);
        player.append(bar, video);
        document.body.appendChild(player);

        video.play().catch(error => {
            console.warn(debugPrefix, 'Embedded player autoplay failed', error);
        });
    }

    async function resolveSource(itemId, source, forceRefresh) {
        showMessage('Streaming Sources', 'Mise en cache Debrid et recuperation du lien...');
        const response = await jellyfinFetch('/StreamingSources/Resolve', {
            method: 'POST',
            body: {
                jellyfinItemId: itemId,
                source,
                forceRefresh: Boolean(forceRefresh)
            }
        });

        closeDialog();

        const streamingUrl = response?.streamingUrl || response?.StreamingUrl;
        if (!streamingUrl) {
            throw new Error('Aucune URL de streaming retournee.');
        }

        const item = await getItem(itemId);
        if (await tryJellyfinPlayback(item, streamingUrl)) {
            return;
        }

        if (triggerNativePlayback()) {
            return;
        }

        playInEmbeddedPlayer(item, streamingUrl);
    }

    function showSources(item, sources) {
        if (!sources || sources.length === 0) {
            showMessage('Sources', 'Aucune source trouvee pour ce media.');
            return;
        }

        showDialog('Choisir une source', modal => {
            const list = document.createElement('div');
            list.className = 'streaming-sources-list';

            sources.forEach(source => {
                const row = document.createElement('div');
                row.className = 'streaming-source-row';

                const content = document.createElement('div');
                const name = document.createElement('div');
                name.className = 'streaming-source-name';
                name.textContent = source.name || source.Name || 'Source sans nom';

                const meta = document.createElement('div');
                meta.className = 'streaming-source-meta';
                meta.textContent = [
                    source.quality || source.Quality,
                    source.language || source.Language,
                    source.codec || source.Codec,
                    source.isWebReady === false || source.IsWebReady === false ? 'transcodage conseille' : 'web-ready',
                    formatSize(source.sizeBytes || source.SizeBytes),
                    `${source.seeders || source.Seeders || 0} seeders`
                ].filter(Boolean).join(' | ');

                content.append(name, meta);

                const description = source.description || source.Description;
                if (description) {
                    const details = document.createElement('div');
                    details.className = 'streaming-source-meta';
                    details.textContent = description;
                    content.appendChild(details);
                }

                const play = jellyfinButton('Lire', 'raised button-submit emby-button');
                play.addEventListener('click', async () => {
                    try {
                        await resolveSource(item.Id, normalizeSource(source), false);
                    } catch (error) {
                        showMessage('Erreur', error.message || String(error), true);
                    }
                });

                row.append(content, play);
                list.appendChild(row);
            });

            modal.appendChild(list);
        });
    }

    function normalizeSource(source) {
        return {
            name: source.name || source.Name || '',
            sizeBytes: source.sizeBytes || source.SizeBytes || 0,
            seeders: source.seeders || source.Seeders || 0,
            language: source.language || source.Language || '',
            quality: source.quality || source.Quality || '',
            codec: source.codec || source.Codec || '',
            isHdr: source.isHdr || source.IsHdr || false,
            isDolbyVision: source.isDolbyVision || source.IsDolbyVision || false,
            hash: source.hash || source.Hash || '',
            magnet: source.magnet || source.Magnet || '',
            directUrl: source.directUrl || source.DirectUrl || '',
            provider: source.provider || source.Provider || '',
            description: source.description || source.Description || '',
            isWebReady: source.isWebReady ?? source.IsWebReady ?? true
        };
    }

    async function onSourcesClick() {
        try {
            const itemId = getItemId();
            if (!itemId) {
                showMessage('Sources', 'Impossible de trouver l’identifiant du media.');
                return;
            }

            showMessage('Sources', 'Recherche des sources...');
            const item = await getItem(itemId);
            const lookup = buildLookup(item);
            debug('Lookup request', lookup);
            const sources = await jellyfinFetch('/StreamingSources/Search', {
                method: 'POST',
                body: lookup
            });

            showSources(item, normalizeSourcesResponse(sources));
        } catch (error) {
            showMessage('Erreur', error.message || String(error), true);
        }
    }

    function injectButton() {
        try {
            if (!isDetailPage()) {
                document.getElementById(buttonId)?.remove();
                return;
            }

            if (document.getElementById(buttonId)) {
                return;
            }

            const playButton = findPlayButton();
            const container = findButtonContainer() || document.body;
            const className = container === document.body
                ? 'raised button-submit emby-button streaming-sources-button streaming-sources-floating-button'
                : 'raised button-submit emby-button streaming-sources-button';

            const button = container === document.body
                ? jellyfinButton('Sources', className)
                : jellyfinIconButton('source', 'Sources');

            button.id = buttonId;
            button.addEventListener('click', onSourcesClick);

            if (playButton?.parentElement === container) {
                playButton.insertAdjacentElement('afterend', button);
            } else {
                container.appendChild(button);
            }

            debug('Sources button injected', {
                fallback: container === document.body,
                href: window.location.href
            });

            setTimeout(() => {
                const currentButton = document.getElementById(buttonId);
                if (isDetailPage() && currentButton && !isElementVisible(currentButton)) {
                    moveButtonToFallback(currentButton);
                }
            }, 1200);
        } catch (error) {
            console.error(debugPrefix, 'Failed to inject Sources button', error);
        }
    }

    function observeNavigation() {
        setInterval(() => {
            if (lastUrl !== window.location.href) {
                lastUrl = window.location.href;
                setTimeout(injectButton, 500);
            }

            injectButton();
        }, 1000);

        const observer = new MutationObserver(() => {
            if (isDetailPage() && !document.getElementById(buttonId)) {
                injectButton();
            }
        });
        observer.observe(document.body, { childList: true, subtree: true });
    }

    ensureStyles();
    debug('Client script loaded');
    observeNavigation();
    setTimeout(injectButton, 500);
})();
