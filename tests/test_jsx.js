const fs = require('fs');
const vm = require('vm');

const code = fs.readFileSync('jsx/host.jsx', 'utf8');
const sandbox = {
    app: { project: {} },
    $: { os: 'Windows' },
    SP_HOST: 'PR'
};

try {
    vm.createContext(sandbox);
    vm.runInContext(code, sandbox);
    console.log('JSX parsed successfully, SP_run type:', typeof sandbox.SP_run);
} catch (err) {
    console.error('JSX parsing error:', err);
    process.exit(1);
}
