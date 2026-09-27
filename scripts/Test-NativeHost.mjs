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
assert.deepEqual(listed.tools.map(tool => tool.name), ['text_statistics', 'convert_case']);
const called = await request('tools/call', { name: 'text_statistics', arguments: { text: 'hello world' } });
assert.deepEqual(JSON.parse(called.content[0].text), { characterCount: 11, wordCount: 2, lineCount: 1, byteSize: 11 });
child.stdin.end();
console.log('Native KillerMCP stdio handshake, tool listing, and tool call passed.');
