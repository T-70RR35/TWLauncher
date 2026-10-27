// O estado de hover/foco fica fora do Razor para funcionar inclusive quando
// o mouse entra em um iframe e quando o foco muda entre botões do carrossel.
window.tvLauncherHero = (() => {
    let observer = null;
    let cleanup = [];
    let dotnetRef = null;
    let targets = [];
    const hovered = new Set();
    let lastInteraction = null;
    let keyboardNavigation = false;
    let activeIframe = null;
    let iframeCleanup = null;
    let playerObserver = null;
    let audioRetries = [];
    let heroRoot = null;

    const sendPlayerCommand = (command) => {
        const iframe = activeIframe;
        if (!iframe || !iframe.isConnected || !iframe.contentWindow) return;
        iframe.contentWindow.postMessage(JSON.stringify({
            event: 'command', func: command, args: []
        }), 'https://www.youtube.com');
    };

    const clearAudioRetries = () => {
        audioRetries.forEach(clearTimeout);
        audioRetries = [];
    };

    const requestAudio = () => {
        if (lastInteraction !== true) return;
        sendPlayerCommand('unMute');
        sendPlayerCommand('playVideo');
    };

    const stopAudio = () => {
        clearAudioRetries();
        sendPlayerCommand('mute');
        sendPlayerCommand('pauseVideo');
    };

    const bindIframe = () => {
        const current = heroRoot?.querySelector('.hero-iframe') ?? null;
        if (current === activeIframe) return;
        if (iframeCleanup) iframeCleanup();
        clearAudioRetries();
        activeIframe = current;
        iframeCleanup = null;
        if (!current) return;
        const loaded = () => {
            if (lastInteraction !== true) return;
            // Allow the YouTube IFrame API to initialize before requesting audio.
            [0, 300, 850].forEach(delay => {
                audioRetries.push(setTimeout(requestAudio, delay));
            });
        };
        current.addEventListener('load', loaded);
        iframeCleanup = () => current.removeEventListener('load', loaded);
        // The iframe can have loaded before MutationObserver registers it.
        if (lastInteraction) audioRetries.push(setTimeout(requestAudio, 500));
    };

    const handlePlayerMessage = (event) => {
        if (event.origin !== 'https://www.youtube.com' &&
            event.origin !== 'https://www.youtube-nocookie.com') return;
        if (event.source !== activeIframe?.contentWindow) return;
        let data = event.data;
        if (typeof data === 'string') {
            try { data = JSON.parse(data); } catch { return; }
        }
        if (data?.event === 'onReady' && lastInteraction === true) requestAudio();
    };

    const notify = () => {
        if (!dotnetRef) return;
        // Um clique não deve deixar o carrossel pausado para sempre pelo foco
        // residual do botão. Consideramos foco somente na navegação por teclado.
        const focused = keyboardNavigation && targets.some(el => el && el.contains(document.activeElement));
        const active = hovered.size > 0 || focused;
        if (active === lastInteraction) return;
        lastInteraction = active;
        if (active) requestAudio();
        else stopAudio();
        dotnetRef.invokeMethodAsync('HeroInteractionChanged', active).catch(() => {});
    };

    const attach = (el) => {
        if (!el) return;
        const enter = () => { hovered.add(el); notify(); };
        const leave = () => { hovered.delete(el); notify(); };
        const focus = () => notify();
        const blur = () => queueMicrotask(notify); // Confere o novo activeElement.
        el.addEventListener('pointerenter', enter);
        el.addEventListener('pointerleave', leave);
        el.addEventListener('focusin', focus);
        el.addEventListener('focusout', blur);
        cleanup.push(() => {
            el.removeEventListener('pointerenter', enter);
            el.removeEventListener('pointerleave', leave);
            el.removeEventListener('focusin', focus);
            el.removeEventListener('focusout', blur);
        });
    };

    const unregister = () => {
        if (observer) observer.disconnect();
        observer = null;
        if (playerObserver) playerObserver.disconnect();
        playerObserver = null;
        if (iframeCleanup) iframeCleanup();
        iframeCleanup = null;
        stopAudio();
        activeIframe = null;
        heroRoot = null;
        window.removeEventListener('message', handlePlayerMessage);
        cleanup.forEach(fn => fn());
        cleanup = [];
        hovered.clear();
        keyboardNavigation = false;
        targets = [];
        dotnetRef = null;
        lastInteraction = null;
    };

    return {
        register: (hero, selector, dotnet) => {
            unregister();
            if (!hero || !dotnet) return;
            dotnetRef = dotnet;
            heroRoot = hero;
            targets = [hero, selector].filter(Boolean);
            playerObserver = new MutationObserver(bindIframe);
            playerObserver.observe(hero, { childList: true, subtree: true });
            bindIframe();
            window.addEventListener('message', handlePlayerMessage);
            const soundClick = event => {
                const button = event.target.closest?.('[data-trailer-audio]');
                if (!button) return;
                // Executed within a genuine click gesture for restrictive autoplay policies.
                requestAudio();
            };
            hero.addEventListener('click', soundClick);
            cleanup.push(() => hero.removeEventListener('click', soundClick));
            targets.forEach(attach);
            const pointerDown = () => { keyboardNavigation = false; queueMicrotask(notify); };
            const keyDown = e => {
                if (e.key === 'Tab' || e.key.startsWith('Arrow') || e.key === 'Enter' || e.key === ' ') {
                    keyboardNavigation = true;
                    queueMicrotask(notify);
                }
            };
            document.addEventListener('pointerdown', pointerDown, true);
            document.addEventListener('keydown', keyDown, true);
            cleanup.push(() => document.removeEventListener('pointerdown', pointerDown, true));
            cleanup.push(() => document.removeEventListener('keydown', keyDown, true));
            observer = new IntersectionObserver(entries => {
                for (const entry of entries) {
                    dotnet.invokeMethodAsync('HeroVisibilityChanged',
                        entry.isIntersecting && entry.intersectionRatio > 0.5).catch(() => {});
                }
            }, { threshold: [0, 0.5, 0.6, 1] });
            observer.observe(hero);
            notify();
        },
        ensureActiveIndicatorVisible: () => {
            const track = document.querySelector('.hero-progress-premium .hero-segments');
            const active = track?.querySelector('.progress-selected');
            if (!track || !active) return;
            const x = active.offsetLeft - track.offsetLeft;
            if (x < track.scrollLeft || x + active.offsetWidth > track.scrollLeft + track.clientWidth)
                track.scrollLeft = Math.max(0, x - track.clientWidth / 2);
        },
        unregister
    };
})();
