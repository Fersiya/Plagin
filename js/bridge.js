/**
 * SubPanel AI Live Bridge (v1.5.0)
 * Starts an internal localhost HTTP server inside CEP Node.js runtime.
 * Allows Antigravity, Python scripts, CLI tools, and external agents
 * to control Premiere Pro and After Effects via REST / JSON-RPC.
 */

var SP_Bridge = (function () {
    var server = null;
    var currentPort = 8999;
    var isRunning = false;
    var lastError = null;

    function init(csInterface, hostApp, onStatusChange) {
        if (typeof require === 'undefined') {
            console.log('[SP_Bridge] Node.js is not available in this environment.');
            if (onStatusChange) onStatusChange({ running: false, error: 'Node.js disabled' });
            return;
        }

        try {
            var http = require('http');

            server = http.createServer(function (req, res) {
                // Set CORS headers
                res.setHeader('Access-Control-Allow-Origin', '*');
                res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
                res.setHeader('Access-Control-Allow-Headers', 'Content-Type');

                if (req.method === 'OPTIONS') {
                    res.writeHead(204);
                    res.end();
                    return;
                }

                var urlParts = req.url.split('?');
                var path = urlParts[0];

                if (req.method === 'GET' && (path === '/' || path === '/status' || path === '/health')) {
                    var statusData = {
                        ok: true,
                        service: 'SubPanel AI Bridge',
                        version: '1.5.0',
                        host: hostApp || 'UNKNOWN',
                        port: currentPort,
                        uptime: process.uptime(),
                        timestamp: new Date().toISOString()
                    };
                    res.writeHead(200, { 'Content-Type': 'application/json; charset=utf-8' });
                    res.end(JSON.stringify(statusData, null, 2));
                    return;
                }

                if (req.method === 'POST') {
                    var body = '';
                    req.on('data', function (chunk) {
                        body += chunk;
                        // Avoid payload flood (> 10MB)
                        if (body.length > 10 * 1024 * 1024) {
                            res.writeHead(413, { 'Content-Type': 'application/json' });
                            res.end(JSON.stringify({ ok: false, error: 'payload_too_large' }));
                            req.connection.destroy();
                        }
                    });

                    req.on('end', function () {
                        var parsed = {};
                        if (body.trim().length > 0) {
                            try {
                                parsed = JSON.parse(body);
                            } catch (e) {
                                res.writeHead(400, { 'Content-Type': 'application/json' });
                                res.end(JSON.stringify({ ok: false, error: 'invalid_json', detail: e.message }));
                                return;
                            }
                        }

                        handlePostRequest(path, parsed, csInterface, function (err, result) {
                            if (err) {
                                res.writeHead(500, { 'Content-Type': 'application/json; charset=utf-8' });
                                res.end(JSON.stringify({ ok: false, error: err.toString() }));
                            } else {
                                res.writeHead(200, { 'Content-Type': 'application/json; charset=utf-8' });
                                res.end(JSON.stringify(result));
                            }
                        });
                    });
                    return;
                }

                res.writeHead(404, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({ ok: false, error: 'not_found' }));
            });

            server.on('error', function (err) {
                lastError = err;
                console.error('[SP_Bridge] Server error:', err);
                // If port already in use, try next port (9000, 9001...)
                if (err.code === 'EADDRINUSE') {
                    currentPort++;
                    if (currentPort <= 9010) {
                        console.log('[SP_Bridge] Port busy, trying port ' + currentPort);
                        server.listen(currentPort, '127.0.0.1');
                        return;
                    }
                }
                isRunning = false;
                if (onStatusChange) onStatusChange({ running: false, error: err.message, port: currentPort });
            });

            server.listen(currentPort, '127.0.0.1', function () {
                isRunning = true;
                lastError = null;
                console.log('[SP_Bridge] Server listening on http://127.0.0.1:' + currentPort);
                if (onStatusChange) onStatusChange({ running: true, port: currentPort, host: hostApp });
            });

        } catch (ex) {
            lastError = ex;
            console.error('[SP_Bridge] Initialization failed:', ex);
            if (onStatusChange) onStatusChange({ running: false, error: ex.message });
        }
    }

    function handlePostRequest(path, data, csInterface, callback) {
        if (!csInterface) {
            return callback(new Error('CSInterface is not available'));
        }

        if (path === '/eval') {
            var script = data.script || data.code;
            if (!script) return callback(new Error('Missing script parameter'));
            csInterface.evalScript(script, function (res) {
                try {
                    var parsedRes = JSON.parse(res);
                    callback(null, { ok: true, result: parsedRes });
                } catch (e) {
                    callback(null, { ok: true, raw: res });
                }
            });
            return;
        }

        if (path === '/action') {
            var action = data.action;
            var payload = data.data || {};
            var jsxCall = 'SP_run(' + JSON.stringify(action) + ', ' + JSON.stringify(payload) + ')';
            csInterface.evalScript(jsxCall, function (res) {
                try {
                    callback(null, JSON.parse(res));
                } catch (e) {
                    callback(null, { ok: false, error: 'parse_failed', raw: res });
                }
            });
            return;
        }

        if (path === '/markers') {
            var markersCall = 'SP_run("add_markers", ' + JSON.stringify(data) + ')';
            csInterface.evalScript(markersCall, function (res) {
                try {
                    callback(null, JSON.parse(res));
                } catch (e) {
                    callback(null, { ok: false, error: 'parse_failed', raw: res });
                }
            });
            return;
        }

        if (path === '/punchin') {
            var punchCall = 'SP_run("punchin", ' + JSON.stringify(data) + ')';
            csInterface.evalScript(punchCall, function (res) {
                try {
                    callback(null, JSON.parse(res));
                } catch (e) {
                    callback(null, { ok: false, error: 'parse_failed', raw: res });
                }
            });
            return;
        }

        if (path === '/silence_cut') {
            var silenceCall = 'SP_run("silence_cut", ' + JSON.stringify(data) + ')';
            csInterface.evalScript(silenceCall, function (res) {
                try {
                    callback(null, JSON.parse(res));
                } catch (e) {
                    callback(null, { ok: false, error: 'parse_failed', raw: res });
                }
            });
            return;
        }

        callback(new Error('Unknown endpoint: ' + path));
    }

    function getStatus() {
        return {
            running: isRunning,
            port: currentPort,
            error: lastError ? lastError.message : null
        };
    }

    return {
        init: init,
        getStatus: getStatus
    };
})();

if (typeof module !== 'undefined' && module.exports) {
    module.exports = SP_Bridge;
}
