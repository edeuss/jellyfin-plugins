const pluginId = 'd5861930-8da6-499c-b7dd-235c60703f64';

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

export default function (view) {
    view.addEventListener('viewshow', function () {
        Dashboard.showLoadingMsg();
        Promise.all([
            ApiClient.getPluginConfiguration(pluginId),
            ApiClient.ajax({ url: ApiClient.getUrl('AnimatedMusic'), type: 'GET', dataType: 'json' })
        ]).then(function (results) {
            view.querySelector('#EnableWebUi').checked = webUiEnabled(results[0]);
            view.querySelector('#InjectionStatus').textContent = statusText(results[1].injectionStatus);
            Dashboard.hideLoadingMsg();
        }).catch(function () {
            Dashboard.hideLoadingMsg();
        });
    });

    view.querySelector('#saveConfig').addEventListener('click', function (e) {
        e.preventDefault();
        Dashboard.showLoadingMsg();
        ApiClient.getPluginConfiguration(pluginId).then(function (config) {
            config.EnableWebUi = view.querySelector('#EnableWebUi').checked;
            return ApiClient.updatePluginConfiguration(pluginId, config);
        }).then(function (result) {
            Dashboard.processPluginConfigurationUpdateResult(result);
        }).catch(function () {
            Dashboard.hideLoadingMsg();
        });
    });
}
