import assert from 'node:assert/strict';
import { pathToFileURL } from 'node:url';

const directory = process.argv[2];
assert.ok(directory, 'Pass the browser check AppBundle directory.');
const { dotnet } = await import(pathToFileURL(`${directory}/_framework/dotnet.js`).href);
const runtime = await dotnet.withDiagnosticTracing(false).create();
const exports = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
assert.equal(exports.BrowserHarness.RunChecks(), 0, 'The browser runtime must pass the same QRCoder corpus as the native engine.');
const fixture = JSON.parse(exports.BrowserHarness.Fixture());
const found = JSON.parse(exports.KillerTools.Qr.Browser.Reader.Read(fixture.gray, fixture.width, fixture.height));
assert.equal(found.length, 1);
assert.equal(found[0].text, 'https://killershot.net/qr');
assert.equal(found[0].link.disposition, 'Allowed');
assert.equal(found[0].corners.length, 4);
console.log('PASS browser interop and shared QR corpus.');
process.exit(0);
