import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';

const root = resolve(import.meta.dirname, '..');
const site = resolve(process.argv[2] ?? '../../killer-tools-site');
const coverage = JSON.parse(await readFile(resolve(site, 'mcp/coverage.json'), 'utf8'));
const native = JSON.parse(await readFile(resolve(root, 'migration/native-operations.json'), 'utf8'));
const websiteOperations = Object.values(coverage).flat();

assert.equal(Object.keys(coverage).length, 81, 'Website tool count changed');
assert.equal(new Set(websiteOperations).size, 93, 'Website MCP operation count changed');
for (const [operation, status] of Object.entries(native)) {
  assert.ok(websiteOperations.includes(operation), `Unknown native operation: ${operation}`);
  assert.equal(status, 'verified', `Native operation is not verified: ${operation}`);
}

const remaining = websiteOperations.filter(operation => !Object.hasOwn(native, operation));
console.log(`${Object.keys(native).length} of ${websiteOperations.length} website operations are verified natively. ${remaining.length} remain.`);
