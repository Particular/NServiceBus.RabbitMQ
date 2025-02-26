const path = require('path');
const core = require('@actions/core');
const exec = require('@actions/exec');

const setupPs1 = path.resolve(__dirname, '../setup.ps1');
const cleanupPs1 = path.resolve(__dirname, '../cleanup.ps1');

console.log('Setup path: ' + setupPs1);
console.log('Cleanup path: ' + cleanupPs1);

// Only one endpoint, so determine if this is the post action, and set it true so that
// the next time we're executed, it goes to the post action
let isPost = core.getState('IsPost');
core.saveState('IsPost', true);

let connectionStringName = core.getInput('connection-string-name');
let hostEnvVarName = core.getInput('host-env-var-name');
let tagName = core.getInput('tag');
let imageTag = core.getInput('image-tag');
let registryLoginServer = core.getInput('registry-login-server');
let registryUser = core.getInput('registry-username');
let registryPass = core.getInput('registry-password');

async function run() {

    try {

        if (!isPost) {

            console.log("Running setup action");

            let LavinMQName = 'psw-lavinmq-' + Math.round(10000000000 * Math.random());
            core.saveState('LavinMQName', LavinMQName);

            console.log("LavinMQName = " + LavinMQName);

            await exec.exec('pwsh', [
                '-File', setupPs1,
                '-hostname', LavinMQName,
                '-connectionStringName', connectionStringName,
                '-tagName', tagName,
                '-hostEnvVarName', hostEnvVarName,
                '-imageTag', imageTag,
                '-registryLoginServer', registryLoginServer,
                '-registryUser', registryUser,
                '-registryPass', registryPass
            ]);

        } else { // Cleanup

            console.log("Running cleanup");

            let LavinMQName = core.getState('LavinMQName');

            await exec.exec('pwsh', [
                '-File', cleanupPs1,
                '-LavinMQName', LavinMQName
            ]);

        }

    } catch (err) {
        core.setFailed(err);
        console.log(err);
    }

}

run();