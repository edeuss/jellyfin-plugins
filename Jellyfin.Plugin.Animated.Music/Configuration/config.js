const pluginId = 'd5861930-8da6-499c-b7dd-235c60703f64';

function field(obj, names) {
    if (!obj) {
        return '';
    }
    for (let i = 0; i < names.length; i++) {
        const value = obj[names[i]];
        if (value !== undefined && value !== null && value !== '') {
            return value;
        }
    }
    return '';
}

function statusText(code) {
    switch (code) {
        case 'FileTransformation':
            return 'Loaded with File Transformation.';
        case 'JavaScriptInjector':
            return 'Loaded with JavaScript Injector.';
        case 'IndexHtml':
            return 'Script tag added to jellyfin-web/index.html.';
        case 'Manual':
            return 'Could not inject automatically. Add src="../AnimatedMusic/web.js" before </body> in jellyfin-web/index.html.';
        case 'Disabled':
            return 'Web UI is off.';
        case 'Pending':
            return 'Starting. Refresh this page in a few seconds.';
        default:
            return 'Status: ' + (code || 'unknown') + '. Restart Jellyfin after saving if nothing shows up.';
    }
}

function webUiEnabled(config) {
    if (config.EnableWebUi === false || config.enableWebUi === false) {
        return false;
    }
    return true;
}

function loadStatus() {
    if (ApiClient.ajax) {
        return ApiClient.ajax({ url: ApiClient.getUrl('AnimatedMusic'), type: 'GET', dataType: 'json' });
    }
    return ApiClient.getJSON(ApiClient.getUrl('AnimatedMusic'));
}

export default function (view) {
    view.addEventListener('viewshow', function () {
        Dashboard.showLoadingMsg();
        Promise.all([
            ApiClient.getPluginConfiguration(pluginId),
            loadStatus()
        ]).then(function (results) {
            const config = results[0] || {};
            let status = results[1];
            if (typeof status === 'string') {
                try {
                    status = JSON.parse(status);
                } catch (e) {
                    status = {};
                }
            }
            view.querySelector('#EnableWebUi').checked = webUiEnabled(config);
            view.querySelector('#InjectionStatus').textContent = statusText(
                field(status, ['injectionStatus', 'InjectionStatus'])
            );
            Dashboard.hideLoadingMsg();
        }).catch(function () {
            view.querySelector('#InjectionStatus').textContent = statusText('');
            Dashboard.hideLoadingMsg();
        });
    });

    view.querySelector('#saveConfig').addEventListener('click', function (e) {
        e.preventDefault();
        Dashboard.showLoadingMsg();
        ApiClient.getPluginConfiguration(pluginId).then(function (config) {
            config.EnableWebUi = view.querySelector('#EnableWebUi').checked;
            config.enableWebUi = config.EnableWebUi;
            return ApiClient.updatePluginConfiguration(pluginId, config);
        }).then(function (result) {
            Dashboard.processPluginConfigurationUpdateResult(result);
        }).catch(function () {
            Dashboard.hideLoadingMsg();
        });
    });
}
