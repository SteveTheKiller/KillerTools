import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

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
  'diff_text', 'calculate_nd_exposure', 'calculate_exposure_equivalence',
  'calculate_depth_of_field', 'generate_svg_placeholder', 'calculate_chmod',
  'expand_ipv4_range', 'calculate_ipv4_subnet', 'generate_ipv6_ula', 'json_to_csv',
  'parse_url', 'generate_spf_record', 'generate_dmarc_record', 'parse_email_headers',
  'test_regex',
  'encode_file_base64_local', 'decode_file_base64_local',
  'otp_private', 'generate_otp_secret_private',
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
const exposure = await request('tools/call', { name: 'calculate_nd_exposure', arguments: { baseSeconds: 0.5, stops: 6 } });
assert.deepEqual(JSON.parse(exposure.content[0].text), { seconds: 32 });
const difference = await request('tools/call', { name: 'diff_text', arguments: { left: 'one\ntwo', right: 'one\nnew' } });
assert.equal(JSON.parse(difference.content[0].text).added, 1);
const subnet = await request('tools/call', { name: 'calculate_ipv4_subnet', arguments: { address: '198.51.100.27/24' } });
assert.equal(JSON.parse(subnet.content[0].text).cidr, '198.51.100.0/24');
const spf = await request('tools/call', { name: 'generate_spf_record', arguments: { providers: ['include:_spf.google.com'], ipAddresses: ['198.51.100.7'], enforcement: '-all' } });
assert.deepEqual(JSON.parse(spf.content[0].text), { record: 'v=spf1 include:_spf.google.com ip4:198.51.100.7 -all' });
const regex = await request('tools/call', { name: 'test_regex', arguments: { pattern: 'a+', text: 'caaab' } });
assert.equal(JSON.parse(regex.content[0].text).matches.length, 1);
const directory = await mkdtemp(join(tmpdir(), 'killermcp-'));
try {
  const source = join(directory, 'source.bin');
  const destination = join(directory, 'decoded.bin');
  await writeFile(source, Buffer.from([0, 1, 2, 254, 255]));
  const fileEncoded = await request('tools/call', { name: 'encode_file_base64_local', arguments: { path: source } });
  assert.deepEqual(JSON.parse(fileEncoded.content[0].text), { base64: 'AAEC/v8=', byteLength: 5 });
  const fileDecoded = await request('tools/call', { name: 'decode_file_base64_local', arguments: { path: destination, base64: 'AAEC/v8=' } });
  assert.equal(JSON.parse(fileDecoded.content[0].text).byteLength, 5);
  assert.deepEqual(await readFile(destination), Buffer.from([0, 1, 2, 254, 255]));
  const overwrite = await request('tools/call', { name: 'decode_file_base64_local', arguments: { path: destination, base64: 'AAEC/v8=' } });
  assert.equal(overwrite.isError, true);
} finally {
  await rm(directory, { recursive: true, force: true });
}
const hotp = await request('tools/call', { name: 'otp_private', arguments: { secret: 'GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ', mode: 'hotp', counter: 0 } });
assert.deepEqual(JSON.parse(hotp.content[0].text), { code: '755224', counter: 0 });
const otpSecret = await request('tools/call', { name: 'generate_otp_secret_private', arguments: { issuer: 'Killer Tools', account: 'user@example.com' } });
const generatedOtp = JSON.parse(otpSecret.content[0].text);
assert.match(generatedOtp.secret, /^[A-Z2-7]{16}$/);
assert.match(generatedOtp.uri, /^otpauth:\/\/totp\/Killer%20Tools:user%40example\.com\?/);
child.stdin.end();
console.log('Native KillerMCP stdio handshake, tool listing, and tool call passed.');
