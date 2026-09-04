(() => {
    const gisScriptUrl = 'https://accounts.google.com/gsi/client';
    const gisScriptId = 'google-identity-services';
    let scriptPromise;
    let componentReference;

    function loadGis() {
        if (window.google?.accounts?.id) {
            return Promise.resolve();
        }

        if (scriptPromise) {
            return scriptPromise;
        }

        scriptPromise = new Promise((resolve, reject) => {
            let script = document.getElementById(gisScriptId);
            const timeout = window.setTimeout(
                () => reject(new Error('Google Identity Services timed out.')),
                10000);

            const loaded = () => {
                window.clearTimeout(timeout);
                if (window.google?.accounts?.id) {
                    resolve();
                } else {
                    reject(new Error('Google Identity Services did not initialize.'));
                }
            };
            const failed = () => {
                window.clearTimeout(timeout);
                reject(new Error('Google Identity Services failed to load.'));
            };

            if (!script) {
                script = document.createElement('script');
                script.id = gisScriptId;
                script.src = gisScriptUrl;
                script.async = true;
                script.defer = true;
                document.head.appendChild(script);
            }

            script.addEventListener('load', loaded, { once: true });
            script.addEventListener('error', failed, { once: true });
        });

        return scriptPromise;
    }

    function renderButton(elementId) {
        const element = document.getElementById(elementId);
        if (!element || !window.google?.accounts?.id) {
            return;
        }

        element.replaceChildren();
        window.google.accounts.id.renderButton(element, {
            type: 'standard',
            theme: 'outline',
            size: 'large',
            text: 'signin_with'
        });
    }

    window.googleAuth = {
        initialize: async (dotNetReference, clientId, elementId) => {
            componentReference = dotNetReference;
            await loadGis();
            window.google.accounts.id.initialize({
                client_id: clientId,
                auto_select: false,
                callback: response => {
                    if (typeof response?.credential === 'string' && componentReference) {
                        componentReference.invokeMethodAsync(
                            'HandleGoogleCredential',
                            response.credential);
                    }
                }
            });
            renderButton(elementId);
        },
        renderButton,
        disableAutoSelect: () => {
            window.google?.accounts?.id?.disableAutoSelect();
        },
        dispose: () => {
            componentReference = undefined;
        }
    };
})();
