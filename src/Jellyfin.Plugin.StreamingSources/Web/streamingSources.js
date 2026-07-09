(function () {
    'use strict';

    const buttonId = 'streamingSourcesButton';
    const dialogId = 'streamingSourcesDialog';
    const styleId = 'streamingSourcesStyle';
    let lastUrl = '';

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

    function findButtonContainer() {
        return document.querySelector('.mainDetailButtons') ||
            document.querySelector('.detailButtonContainer') ||
            document.querySelector('.itemDetailPage .buttons') ||
            document.querySelector('[data-role="content"]');
    }

    function formatSize(bytes) {
        if (!bytes) {
            return 'taille inconnue';
        }

        return `${(bytes / 1024 / 1024 / 1024).toFixed(2)} Go`;
    }

    async function jellyfinFetch(path, options) {
        const client = apiClient();
        if (!client) {
            throw new Error('ApiClient Jellyfin introuvable.');
        }

        if (typeof client.ajax === 'function') {
            return client.ajax(Object.assign({
                type: options.method || 'GET',
                url: client.getUrl(path),
                contentType: 'application/json'
            }, options.body ? { data: JSON.stringify(options.body) } : {}));
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

        return response.status === 204 ? null : response.json();
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

        if (!response?.streamingUrl) {
            throw new Error('Aucune URL de streaming retournee.');
        }

        window.location.href = response.streamingUrl;
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
                    formatSize(source.sizeBytes || source.SizeBytes),
                    `${source.seeders || source.Seeders || 0} seeders`
                ].filter(Boolean).join(' | ');

                content.append(name, meta);

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
            provider: source.provider || source.Provider || ''
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
            const sources = await jellyfinFetch('/StreamingSources/Search', {
                method: 'POST',
                body: lookup
            });

            showSources(item, sources);
        } catch (error) {
            showMessage('Erreur', error.message || String(error), true);
        }
    }

    function injectButton() {
        if (!isDetailPage()) {
            document.getElementById(buttonId)?.remove();
            return;
        }

        if (document.getElementById(buttonId)) {
            return;
        }

        const container = findButtonContainer();
        if (!container) {
            return;
        }

        const button = jellyfinButton('Sources', 'raised button-submit emby-button streaming-sources-button');
        button.id = buttonId;
        button.addEventListener('click', onSourcesClick);
        container.appendChild(button);
    }

    function observeNavigation() {
        setInterval(() => {
            if (lastUrl !== window.location.href) {
                lastUrl = window.location.href;
                setTimeout(injectButton, 500);
            }

            injectButton();
        }, 1000);
    }

    ensureStyles();
    observeNavigation();
    setTimeout(injectButton, 500);
})();
