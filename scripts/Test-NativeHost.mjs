import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';

const child = spawn('dotnet', ['run', '--project', 'src/KillerMCP/KillerMCP.csproj', '--no-build'], { stdio: ['pipe', 'pipe', 'inherit'] });
let buffer = '';
const pending = new Map();
child.stdout.setEncoding('utf8');
child.stdout.on('data', chunk => {
  buffer += chunk;
  while (buffer.includes('\n')) {
    const end = buffer.indexOf('\n');
    const message = JSON.parse(buffer.slice(0, end));
    buffer = buffer.slice(end + 1);
    pending.get(message.id)?.(message.result);
    pending.delete(message.id);
  }
});
let id = 0;
function request(method, params = {}) {
  const requestId = ++id;
  return new Promise(resolve => {
    pending.set(requestId, resolve);
    child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', id: requestId, method, params })}\n`);
  });
}

const initialized = await request('initialize', { protocolVersion: '2025-06-18' });
assert.equal(initialized.serverInfo.name, 'KillerMCP');
const listed = await request('tools/list');
assert.deepEqual(listed.tools.map(tool => tool.name), [
  'text_statistics', 'convert_case', 'encode_base64', 'decode_base64', 'text_to_ascii_binary',
  'ascii_binary_to_text', 'arabic_to_roman', 'roman_to_arabic', 'text_to_nato_alphabet',
  'convert_integer_base', 'convert_temperature', 'calculate_percentage', 'escape_html_entities',
  'unescape_html_entities',
  'convert_date_time', 'lookup_exchange_ndr', 'lookup_group_policy', 'lookup_http_status',
  'lookup_m365_sku', 'lookup_port_protocol', 'lookup_windows_error', 'lookup_windows_event',
]);
const called = await request('tools/call', { name: 'text_statistics', arguments: { text: 'hello world' } });
assert.deepEqual(JSON.parse(called.content[0].text), { characterCount: 11, wordCount: 2, lineCount: 1, byteSize: 11 });
const encoded = await request('tools/call', { name: 'encode_base64', arguments: { text: 'hé' } });
assert.deepEqual(JSON.parse(encoded.content[0].text), { encoded: 'aMOp' });
const percentage = await request('tools/call', { name: 'calculate_percentage', arguments: { mode: 'change', x: 10, y: 12 } });
assert.deepEqual(JSON.parse(percentage.content[0].text), { value: '+20%' });
const statuses = await request('tools/call', { name: 'lookup_http_status', arguments: { query: 'not found' } });
assert.equal(JSON.parse(statuses.content[0].text)[0].code, 404);
const policies = await request('tools/call', { name: 'lookup_group_policy', arguments: { query: 'minimum password length', limit: 1 } });
assert.equal(JSON.parse(policies.content[0].text)[0].name, 'Minimum Password Length');
child.stdin.end();
console.log('Native KillerMCP stdio handshake, tool listing, and tool call passed.');
