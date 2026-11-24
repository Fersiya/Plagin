const http = require('http');
const SP_Bridge = require('../js/bridge.js');

// Mock csInterface
const mockCsInterface = {
    evalScript: function (script, cb) {
        if (script.indexOf('add_markers') !== -1) {
            cb(JSON.stringify({ ok: true, action: 'add_markers', count: 3 }));
        } else if (script.indexOf('punchin') !== -1) {
            cb(JSON.stringify({ ok: true, action: 'punchin', cuts: 5 }));
        } else if (script === 'test_eval') {
            cb(JSON.stringify({ evaluated: 42 }));
        } else {
            cb(JSON.stringify({ ok: true, script: script }));
        }
    }
};

SP_Bridge.init(mockCsInterface, 'PPRO', function (status) {
    if (status.running) {
        console.log('Bridge started on port:', status.port);

        // Test GET /health
        http.get(`http://127.0.0.1:${status.port}/health`, (res) => {
            let data = '';
            res.on('data', chunk => data += chunk);
            res.on('end', () => {
                const parsed = JSON.parse(data);
                console.log('GET /health response:', parsed.service, parsed.host);

                // Test POST /markers
                const postData = JSON.stringify({ times: [1.5, 3.0, 4.5], name: "Drop" });
                const req = http.request({
                    hostname: '127.0.0.1',
                    port: status.port,
                    path: '/markers',
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' }
                }, (postRes) => {
                    let postRespData = '';
                    postRes.on('data', c => postRespData += c);
                    postRes.on('end', () => {
                        console.log('POST /markers response:', JSON.parse(postRespData));
                        console.log('Bridge test passed successfully!');
                        process.exit(0);
                    });
                });
                req.write(postData);
                req.end();
            });
        });
    } else {
        console.error('Bridge failed to start:', status.error);
        process.exit(1);
    }
});
