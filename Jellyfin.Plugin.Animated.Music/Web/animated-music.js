(function () {
    if (window.AnimatedMusicWeb) {
        return;
    }
    window.AnimatedMusicWeb = true;

    var MARK = 'data-animated-music';
    var IDLE_MS = 10000;
    var LOOKUP_DELAY = 80;
    var MAX_IDS = 50;
    var STYLE_ID = 'animated-music-style';

    var cache = {};
    var waiters = {};
    var queued = [];
    var queuedSet = {};
    var flushTimer = null;
    var scanTimer = null;
    var hoverHost = null;
    var reduceMotion = false;

    function prefersReducedMotion() {
        return window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    }

    function injectCss() {
        if (document.getElementById(STYLE_ID)) {
            return;
        }
        var style = document.createElement('style');
        style.id = STYLE_ID;
        style.textContent =
            '.animated-music-host{position:relative;overflow:hidden}' +
            '.animated-music-media{position:absolute;inset:0;width:100%;height:100%;object-fit:cover;pointer-events:none;opacity:0;transition:opacity .15s ease;z-index:1}' +
            '.nowPlayingImage .animated-music-media,.nowPlayingPageImageContainer .animated-music-media{object-fit:contain}' +
            '.animated-music-media.is-visible{opacity:1}';
        document.head.appendChild(style);
    }

    function apiClient() {
        return window.ApiClient;
    }

    function waitForApi(cb) {
        if (apiClient() && apiClient().accessToken && apiClient().accessToken()) {
            cb();
            return;
        }
        setTimeout(function () { waitForApi(cb); }, 250);
    }

    function withToken(path) {
        var api = apiClient();
        if (!api || !path) {
            return path;
        }
        var rel = path.charAt(0) === '/' ? path.substring(1) : path;
        return api.getUrl(rel, { ApiKey: api.accessToken() });
    }

    function normalizeId(id) {
        if (!id) {
            return '';
        }
        var hex = String(id).replace(/[{}-]/g, '').toLowerCase();
        if (hex.length === 32 && /^[0-9a-f]+$/.test(hex)) {
            return hex;
        }
        return String(id).toLowerCase();
    }

    function idFromUrl(value) {
        if (!value) {
            return '';
        }
        var match = String(value).match(/Items\/([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}|[0-9a-fA-F]{32})/i);
        return match ? normalizeId(match[1]) : '';
    }

    function cardId(card) {
        return normalizeId(card.getAttribute('data-id'));
    }

    function isMusicType(type) {
        return type === 'MusicAlbum' || type === 'Audio';
    }

    function isDetailHero(el) {
        return !!(el.closest && el.closest('.itemDetailPage .detailImageContainer'));
    }

    function isNowPlayingHost(el) {
        return el.classList.contains('nowPlayingImage')
            || el.classList.contains('nowPlayingPageImage')
            || !!(el.closest && (el.closest('.nowPlayingImage') || el.closest('.nowPlayingPageImageContainer')));
    }

    function hostFor(el) {
        if (!el) {
            return null;
        }
        if (el.tagName === 'IMG') {
            return el.parentElement || el;
        }
        return el;
    }

    function ensureHost(el) {
        var host = hostFor(el);
        if (!host) {
            return null;
        }
        host.classList.add('animated-music-host');
        return host;
    }

    function removeMedia(host) {
        if (!host) {
            return;
        }
        var nodes = host.querySelectorAll('.animated-music-media');
        for (var i = 0; i < nodes.length; i++) {
            unload(nodes[i]);
            nodes[i].remove();
        }
        host.removeAttribute(MARK);
        if (host._amIdle) {
            clearTimeout(host._amIdle);
            host._amIdle = null;
        }
    }

    function unload(media) {
        if (!media) {
            return;
        }
        try {
            if (media.tagName === 'VIDEO') {
                media.pause();
                media.removeAttribute('src');
                media.removeAttribute('poster');
                media.load();
            } else {
                media.removeAttribute('src');
            }
        } catch (e) {
            /* ignore */
        }
    }

    function isGif(cover) {
        var mime = (cover && cover.mimeType) || '';
        var url = (cover && cover.url) || '';
        return mime.indexOf('gif') !== -1 || /\.gif(\?|$)/i.test(url);
    }

    function createMedia(cover) {
        var el;
        if (isGif(cover)) {
            el = document.createElement('img');
            el.alt = '';
        } else {
            el = document.createElement('video');
            el.muted = true;
            el.loop = true;
            el.playsInline = true;
            el.setAttribute('playsinline', '');
            el.preload = 'none';
            if (cover.previewUrl) {
                el.poster = withToken(cover.previewUrl);
            }
        }
        el.className = 'animated-music-media';
        return el;
    }

    function getMedia(host) {
        var child = host.firstElementChild;
        while (child) {
            if (child.classList && child.classList.contains('animated-music-media')) {
                return child;
            }
            child = child.nextElementSibling;
        }
        return host.querySelector('.animated-music-media');
    }

    function attachMedia(host, cover) {
        var media = getMedia(host);
        var tag = isGif(cover) ? 'IMG' : 'VIDEO';
        if (media && media.tagName !== tag) {
            unload(media);
            media.remove();
            media = null;
        }
        if (media) {
            return media;
        }
        media = createMedia(cover);
        host.appendChild(media);
        return media;
    }

    function playMedia(host, cover) {
        if (document.hidden || !cover || !cover.available || !cover.url) {
            return;
        }
        var media = attachMedia(host, cover);
        if (isGif(cover)) {
            if (media.getAttribute('src') !== withToken(cover.url)) {
                media.src = withToken(cover.url);
            }
            media.classList.add('is-visible');
            return;
        }
        var src = withToken(cover.url);
        if (media.getAttribute('src') !== src) {
            media.src = src;
        }
        var play = media.play();
        if (play && play.catch) {
            play.catch(function () { /* autoplay blocked */ });
        }
        media.classList.add('is-visible');
    }

    function pauseMedia(host, unloadAfterIdle) {
        var media = getMedia(host);
        if (!media) {
            return;
        }
        if (media.tagName === 'VIDEO') {
            try {
                media.pause();
            } catch (e) {
                /* ignore */
            }
        }
        media.classList.remove('is-visible');
        if (!unloadAfterIdle) {
            return;
        }
        if (host._amIdle) {
            clearTimeout(host._amIdle);
        }
        host._amIdle = setTimeout(function () {
            host._amIdle = null;
            unload(media);
        }, IDLE_MS);
    }

    function bindHover(card, host) {
        if (host._amHoverBound) {
            return;
        }
        host._amHoverBound = true;

        function enter() {
            if (reduceMotion) {
                return;
            }
            var cover = host._amCover;
            if (!cover) {
                return;
            }
            if (hoverHost && hoverHost !== host) {
                pauseMedia(hoverHost, true);
            }
            hoverHost = host;
            if (host._amIdle) {
                clearTimeout(host._amIdle);
                host._amIdle = null;
            }
            playMedia(host, cover);
        }

        function leave() {
            if (hoverHost === host) {
                hoverHost = null;
            }
            pauseMedia(host, true);
        }

        card.addEventListener('pointerenter', enter);
        card.addEventListener('pointerleave', leave);
        card.addEventListener('focusin', enter);
        card.addEventListener('focusout', function (ev) {
            if (card.contains(ev.relatedTarget)) {
                return;
            }
            leave();
        });
    }

    function applyCover(el, cover, mode) {
        if (!cover || !cover.available) {
            return;
        }
        var host = ensureHost(el);
        if (!host) {
            return;
        }
        var id = host.getAttribute(MARK);
        var nextId = cover.url;
        if (id && id !== nextId) {
            removeMedia(host);
        }
        host.setAttribute(MARK, nextId);
        host._amCover = cover;

        if (id === nextId && getMedia(host) && mode === 'hero') {
            if (reduceMotion) {
                pauseMedia(host, false);
            }
            return;
        }

        if (mode === 'hero') {
            if (reduceMotion) {
                var still = attachMedia(host, cover);
                if (cover.previewUrl && still.tagName === 'VIDEO') {
                    still.poster = withToken(cover.previewUrl);
                    still.classList.add('is-visible');
                } else if (cover.previewUrl) {
                    still.src = withToken(cover.previewUrl);
                    still.classList.add('is-visible');
                }
                return;
            }
            playMedia(host, cover);
            return;
        }

        bindHover(el.closest('.card') || el, host);
    }

    function addWaiter(id, el, mode) {
        if (!waiters[id]) {
            waiters[id] = [];
        }
        var list = waiters[id];
        for (var i = 0; i < list.length; i++) {
            if (list[i].el === el) {
                list[i].mode = mode;
                return;
            }
        }
        list.push({ el: el, mode: mode });
    }

    function requestCover(id, el, mode) {
        id = normalizeId(id);
        if (!id) {
            return;
        }
        if (Object.prototype.hasOwnProperty.call(cache, id)) {
            applyCover(el, cache[id], mode);
            return;
        }
        addWaiter(id, el, mode);
        if (queuedSet[id]) {
            return;
        }
        queuedSet[id] = true;
        queued.push(id);
        if (!flushTimer) {
            flushTimer = setTimeout(flushLookups, LOOKUP_DELAY);
        }
    }

    function flushLookups() {
        flushTimer = null;
        if (!queued.length) {
            return;
        }
        var api = apiClient();
        if (!api || !api.ajax) {
            return;
        }
        var batch = queued.splice(0, MAX_IDS);
        for (var i = 0; i < batch.length; i++) {
            delete queuedSet[batch[i]];
        }
        var url = api.getUrl('AnimatedMusic/Lookup', { ids: batch.join(',') });
        api.ajax({ url: url, type: 'GET', dataType: 'json' }).then(function (data) {
            var items = (data && data.items) || [];
            var seen = {};
            for (var n = 0; n < items.length; n++) {
                var item = items[n];
                var id = normalizeId(item.itemId);
                seen[id] = true;
                cache[id] = item.cover || { available: false };
                finishWaiters(id);
            }
            for (var b = 0; b < batch.length; b++) {
                if (!seen[batch[b]]) {
                    cache[batch[b]] = { available: false };
                    finishWaiters(batch[b]);
                }
            }
        }).catch(function () {
            for (var b = 0; b < batch.length; b++) {
                delete queuedSet[batch[b]];
            }
        });
        if (queued.length) {
            flushTimer = setTimeout(flushLookups, LOOKUP_DELAY);
        }
    }

    function finishWaiters(id) {
        var list = waiters[id];
        if (!list) {
            return;
        }
        delete waiters[id];
        var cover = cache[id];
        for (var i = 0; i < list.length; i++) {
            var entry = list[i];
            if (entry.el && document.contains(entry.el)) {
                applyCover(entry.el, cover, entry.mode);
            }
        }
    }

    function observeCard(card) {
        if (!isMusicType(card.getAttribute('data-type'))) {
            return;
        }
        var image = card.querySelector('.cardImageContainer');
        if (!image) {
            return;
        }
        var id = cardId(card);
        if (!id || image.getAttribute('data-am-seen') === id) {
            return;
        }
        cardIo.observe(image);
    }

    var cardIo = new IntersectionObserver(function (entries) {
        for (var i = 0; i < entries.length; i++) {
            var entry = entries[i];
            if (!entry.isIntersecting) {
                continue;
            }
            var image = entry.target;
            var card = image.closest('.card');
            cardIo.unobserve(image);
            if (!card) {
                continue;
            }
            var id = cardId(card);
            image.setAttribute('data-am-seen', id);
            var mode = isDetailHero(image) ? 'hero' : 'hover';
            requestCover(id, image, mode);
        }
    }, { rootMargin: '80px', threshold: 0.01 });

    function scanCards(root) {
        var cards = (root || document).querySelectorAll('.card[data-type="MusicAlbum"], .card[data-type="Audio"]');
        for (var i = 0; i < cards.length; i++) {
            observeCard(cards[i]);
        }
    }

    function scanDetail() {
        var page = document.querySelector('.itemDetailPage');
        if (!page) {
            return;
        }
        var image = page.querySelector('.detailImageContainer .cardImageContainer');
        var card = image && image.closest('.card');
        if (!image || !card || !isMusicType(card.getAttribute('data-type'))) {
            return;
        }
        requestCover(cardId(card), image, 'hero');
    }

    function scanNowPlaying() {
        var nodes = document.querySelectorAll('.nowPlayingImage, .nowPlayingPageImage');
        for (var i = 0; i < nodes.length; i++) {
            var node = nodes[i];
            var url = node.getAttribute('src') || (node.style && node.style.backgroundImage) || '';
            if (!url && node.currentSrc) {
                url = node.currentSrc;
            }
            var id = idFromUrl(url);
            if (!id && node.style) {
                id = idFromUrl(getComputedStyle(node).backgroundImage);
            }
            if (!id) {
                removeMedia(hostFor(node));
                continue;
            }
            requestCover(id, node, 'hero');
        }
    }

    function scan() {
        scanTimer = null;
        injectCss();
        reduceMotion = prefersReducedMotion();
        scanCards(document);
        scanDetail();
        scanNowPlaying();
    }

    function isOurs(node) {
        return node && node.nodeType === 1 && node.classList
            && (node.classList.contains('animated-music-media') || node.id === STYLE_ID);
    }

    function mutationMatters(mutation) {
        var lists = [mutation.addedNodes, mutation.removedNodes];
        for (var i = 0; i < lists.length; i++) {
            for (var n = 0; n < lists[i].length; n++) {
                if (!isOurs(lists[i][n])) {
                    return true;
                }
            }
        }
        return false;
    }

    function scheduleScan() {
        if (scanTimer) {
            return;
        }
        scanTimer = setTimeout(scan, 120);
    }

    function onVisibility() {
        var media = document.querySelectorAll('.animated-music-media');
        for (var i = 0; i < media.length; i++) {
            var node = media[i];
            var host = node.parentElement;
            if (!host) {
                continue;
            }
            if (document.hidden) {
                if (node.tagName === 'VIDEO') {
                    try { node.pause(); } catch (e) { /* ignore */ }
                }
                continue;
            }
            var cover = host._amCover;
            if (!cover) {
                continue;
            }
            if (isNowPlayingHost(host) || isDetailHero(host) || hoverHost === host) {
                playMedia(host, cover);
            }
        }
    }

    waitForApi(function () {
        injectCss();
        reduceMotion = prefersReducedMotion();
        if (window.matchMedia) {
            var mq = window.matchMedia('(prefers-reduced-motion: reduce)');
            if (mq.addEventListener) {
                mq.addEventListener('change', function () {
                    reduceMotion = mq.matches;
                    scheduleScan();
                });
            }
        }
        document.addEventListener('visibilitychange', onVisibility);
        document.addEventListener('viewshow', scheduleScan, true);
        window.addEventListener('hashchange', scheduleScan);
        var body = document.body;
        if (body) {
            new MutationObserver(function (mutations) {
                for (var i = 0; i < mutations.length; i++) {
                    if (mutationMatters(mutations[i])) {
                        scheduleScan();
                        return;
                    }
                }
            }).observe(body, { childList: true, subtree: true });
        }
        scheduleScan();
    });
})();
