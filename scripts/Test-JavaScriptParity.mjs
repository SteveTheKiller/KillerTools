import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { mkdtemp, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const site = resolve(process.argv[2] ?? '../../killer-tools-site');
if (!existsSync(site)) {
  throw new Error(`KillerTools website repository not found: ${site}`);
}

const statistics = await import(pathToFileURL(resolve(site, 'src/tools/text-statistics/text-statistics.service.ts')));
const percentages = await import(pathToFileURL(resolve(site, 'src/tools/percentage-calculator/percentage-calculator.service.ts')));
const base64 = await import(pathToFileURL(resolve(site, 'src/utils/base64.ts')));
const binary = await import(pathToFileURL(resolve(site, 'src/tools/text-to-binary/text-to-binary.models.ts')));
const roman = await import(pathToFileURL(resolve(site, 'src/tools/roman-numeral-converter/roman-numeral-converter.service.ts')));
const nato = await import(pathToFileURL(resolve(site, 'src/tools/text-to-nato-alphabet/text-to-nato-alphabet.service.ts')));
const integerBase = await import(pathToFileURL(resolve(site, 'src/tools/integer-base-converter/integer-base-converter.model.ts')));
const temperatures = await import(pathToFileURL(resolve(site, 'src/tools/temperature-converter/temperature-converter.models.ts')));
const chmod = await import(pathToFileURL(resolve(site, 'src/tools/chmod-calculator/chmod-calculator.service.ts')));
const svg = await import(pathToFileURL(resolve(site, 'src/tools/svg-placeholder-generator/svg-placeholder-generator.service.ts')));
const caseConverter = await import(pathToFileURL(resolve(site, 'src/tools/case-converter/case-converter.models.ts')));
const parityDirectory = await mkdtemp(join(tmpdir(), 'killertools-parity-'));
const parityFile = join(parityDirectory, 'source.bin');
await writeFile(parityFile, Buffer.from([0, 1, 2, 254, 255]));

const requests = [
  { operation: 'text_statistics', arguments: { text: '' } },
  { operation: 'text_statistics', arguments: { text: 'hello world' } },
  { operation: 'text_statistics', arguments: { text: 'a\r\né' } },
  { operation: 'text_statistics', arguments: { text: ' a ' } },
  { operation: 'text_statistics', arguments: { text: '😀 tools' } },
  { operation: 'convert_case', arguments: { text: 'hello world' } },
  { operation: 'convert_case', arguments: { text: 'XMLHttp request' } },
  { operation: 'convert_date_time', arguments: { value: '0', inputFormat: 'unix_seconds' } },
  { operation: 'convert_date_time', arguments: { value: '5e0be1000000000000000000', inputFormat: 'mongo_object_id' } },
  { operation: 'calculate_percentage', arguments: { mode: 'percent_of', x: 25, y: 200 } },
  { operation: 'calculate_percentage', arguments: { mode: 'what_percent', x: 1, y: 3 } },
  { operation: 'calculate_percentage', arguments: { mode: 'change', x: 10, y: 12 } },
  { operation: 'calculate_percentage', arguments: { mode: 'change', x: 10, y: 8 } },
  { operation: 'encode_base64', arguments: { text: 'hé' } },
  { operation: 'encode_base64', arguments: { text: 'hello?', urlSafe: true } },
  { operation: 'decode_base64', arguments: { encoded: 'aMOp' } },
  { operation: 'text_to_ascii_binary', arguments: { text: 'Az' } },
  { operation: 'ascii_binary_to_text', arguments: { binary: '01000001 01111010' } },
  { operation: 'arabic_to_roman', arguments: { number: 1999 } },
  { operation: 'roman_to_arabic', arguments: { roman: 'MCMXCIX' } },
  { operation: 'text_to_nato_alphabet', arguments: { text: 'SOS 2' } },
  { operation: 'convert_integer_base', arguments: { value: '255', fromBase: 10, toBase: 16 } },
  { operation: 'convert_integer_base', arguments: { value: '100000000', fromBase: 16, toBase: 2 } },
  { operation: 'convert_temperature', arguments: { value: 32, scale: 'fahrenheit' } },
  { operation: 'convert_temperature', arguments: { value: -40, scale: 'celsius' } },
  { operation: 'calculate_chmod', arguments: { permissions: { owner: { read: true, write: true, execute: true }, group: { read: true, write: false, execute: true }, public: { read: true, write: false, execute: false } } } },
  { operation: 'generate_svg_placeholder', arguments: { width: 320, height: 180, fontSize: 26, bgColor: '#cccccc', fgColor: '#333333', useExactSize: true, customText: 'A&B' } },
  { operation: 'generate_qr_code', arguments: { mode: 'text', text: 'https://killertools.net', foreground: '#112233', background: '#fefefe', errorCorrectionLevel: 'high' } },
  { operation: 'generate_qr_code', arguments: { mode: 'wifi', wifi: { ssid: 'Example;Guest', password: 'sec:ret', encryption: 'WPA', eapMethod: null, isHiddenSSID: true, eapAnonymous: false, eapIdentity: '', eapPhase2Method: null }, foreground: '#000000', background: '#ffffff', errorCorrectionLevel: 'medium' } },
  { operation: 'calculate_nd_exposure', arguments: { baseSeconds: 0.5, stops: 6 } },
  { operation: 'calculate_exposure_equivalence', arguments: { shutterSeconds: 0.25, originalAperture: 4, targetAperture: 11 } },
  { operation: 'calculate_depth_of_field', arguments: { focalLengthMm: 50, aperture: 8, focusDistance: 3, focusUnit: 'm', circleOfConfusionMm: 0.03 } },
  { operation: 'escape_html_entities', arguments: { text: `<a title="x">Tom & Jerry's</a>` } },
  { operation: 'unescape_html_entities', arguments: { text: '&lt;a title=&quot;x&quot;&gt;Tom &amp; Jerry&#39;s&lt;/a&gt;' } },
  { operation: 'diff_text', arguments: { left: 'one\ntwo\nthree', right: 'one\nnew\nthree' } },
  { operation: 'diff_text', arguments: { left: 'same\n', right: 'same\n' } },
  { operation: 'diff_text', arguments: { left: '', right: 'added' } },
  { operation: 'expand_ipv4_range', arguments: { startIp: '192.0.2.10', endIp: '192.0.2.70' } },
  { operation: 'expand_ipv4_range', arguments: { startIp: '10.0.0.1', endIp: '10.0.0.17' } },
  { operation: 'calculate_ipv4_subnet', arguments: { address: '198.51.100.27/24' } },
  { operation: 'calculate_ipv4_subnet', arguments: { address: '203.0.113.4/31' } },
  { operation: 'generate_ipv6_ula', arguments: { macAddress: '00:11:22:33:44:55', timestampMs: 1700000000000 } },
  { operation: 'format_json', arguments: { text: '{b:2,a:1,}', indentSize: 3, sortKeys: true } },
  { operation: 'format_json', arguments: { text: "{/* note */ title:'KillerTools',items:[3,2,1,],}", indentSize: 2, sortKeys: false } },
  { operation: 'minify_json', arguments: { text: "{ text: 'hello', hex: 0x10, trailing: true, }" } },
  { operation: 'diff_json', arguments: { left: '{ a: 1, same: true }', right: '{ a: 2, same: true }', onlyDifferences: true } },
  { operation: 'diff_json', arguments: { left: '[1, 2]', right: '[1, 3, 4]', onlyDifferences: false } },
  { operation: 'convert_xml_json', arguments: { text: '<a x="1"/>', direction: 'xml_to_json' } },
  { operation: 'convert_xml_json', arguments: { text: '<?xml version="1.0"?><root><item id="1">one</item><item>two</item></root>', direction: 'xml_to_json' } },
  { operation: 'convert_xml_json', arguments: { text: "{root:{item:[{_attributes:{id:'1'},_text:'one'},{_text:'two'}]}}", direction: 'json_to_xml' } },
  { operation: 'format_xml', arguments: { text: '<a><b>text</b></a>', indentSize: 2, collapseContent: true } },
  { operation: 'format_xml', arguments: { text: '<a><b>text</b></a>', indentSize: 2, collapseContent: false } },
  { operation: 'format_xml', arguments: { text: '<?xml version="1.0"?><root><item id="1">one</item><item>two</item></root>', indentSize: 4, collapseContent: true } },
  { operation: 'convert_json', arguments: { text: '{a:1}', to: 'yaml' } },
  { operation: 'convert_json', arguments: { text: '{a:1}', to: 'toml' } },
  { operation: 'convert_yaml', arguments: { text: 'a: 1', to: 'json' } },
  { operation: 'convert_yaml', arguments: { text: 'a: 1', to: 'toml' } },
  { operation: 'convert_toml', arguments: { text: 'a = 1', to: 'json' } },
  { operation: 'convert_toml', arguments: { text: 'a = 1', to: 'yaml' } },
  { operation: 'format_yaml', arguments: { text: 'b: 2\na: 1', indentSize: 2, sortKeys: true } },
  { operation: 'format_yaml', arguments: { text: 'root:\n  list:\n    - one\n    - two', indentSize: 4, sortKeys: false } },
  { operation: 'generate_meta_tags', arguments: { type: 'website', fields: { title: 'KillerTools', description: 'Useful tools', url: 'https://killertools.net', image: 'https://killertools.net/logo.png', 'twitter:site': '@killertools' } } },
  { operation: 'generate_meta_tags', arguments: { type: 'article', fields: { title: 'News', 'article:author': 'Steve', 'article:published_time': '2026-09-27' } } },
  { operation: 'describe_cron', arguments: { expression: '*/5 * * * *', use24HourTimeFormat: true, dayOfWeekStartIndexZero: true } },
  { operation: 'describe_cron', arguments: { expression: '0 30 9 * * 1-5', use24HourTimeFormat: true, dayOfWeekStartIndexZero: true } },
  { operation: 'describe_cron', arguments: { expression: '0 0 18 ? * SUN', use24HourTimeFormat: false, dayOfWeekStartIndexZero: true } },
  { operation: 'json_to_csv', arguments: { rows: [{ a: 1, b: 'hello, world' }, { a: 2, c: null }] } },
  { operation: 'json_to_csv', arguments: { rows: [{ text: 'line 1\nline 2', active: true, nested: { a: 1 } }] } },
  { operation: 'parse_url', arguments: { url: 'https://user:pass@example.com:8443/a%20b?x=1&x=two+words#top' } },
  { operation: 'parse_url', arguments: { url: 'http://example.com/' } },
  { operation: 'parse_user_agent', arguments: { userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36' } },
  { operation: 'parse_user_agent', arguments: { userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_2 like Mac OS X) AppleWebKit/605.1.15 Version/17.2 Mobile/15E148 Safari/604.1' } },
  { operation: 'parse_user_agent', arguments: { userAgent: 'curl/8.5.0' } },
  { operation: 'parse_user_agent', arguments: { userAgent: 'Mozilla/5.0 (X11; Linux aarch64; rv:121.0) Gecko/20100101 Firefox/121.0' } },
  { operation: 'markdown_to_html', arguments: { markdown: '# Hello\n\n- one\n- two with **bold**' } },
  { operation: 'markdown_to_html', arguments: { markdown: '[link](https://example.com)\n\n> quoted `code`' } },
  { operation: 'markdown_to_html', arguments: { markdown: '```csharp\nConsole.WriteLine("hi");\n```' } },
  { operation: 'search_gifs', arguments: { query: 'killertools-parity-query-with-no-expected-results-4f7c2a', limit: 2 } },
  { operation: 'generate_password_private', arguments: { mode: 'random', length: 20, uppercase: true, lowercase: true, numbers: true, symbols: true, excludeAmbiguous: true, requireOneOfEach: true, wordCount: 6, wordSeparator: '-', capitalizeWords: true, appendNumber: false, format: 'hex' } },
  { operation: 'generate_password_private', arguments: { mode: 'pronounceable', length: 20, uppercase: true, lowercase: true, numbers: true, symbols: true, excludeAmbiguous: true, requireOneOfEach: true, wordCount: 6, wordSeparator: '-', capitalizeWords: true, appendNumber: true, format: 'hex' } },
  { operation: 'generate_password_private', arguments: { mode: 'format', length: 20, uppercase: true, lowercase: true, numbers: true, symbols: true, excludeAmbiguous: true, requireOneOfEach: true, wordCount: 6, wordSeparator: '-', capitalizeWords: true, appendNumber: false, format: 'base64url' } },
  { operation: 'generate_password_private', arguments: { mode: 'passphrase', length: 20, uppercase: true, lowercase: true, numbers: true, symbols: true, excludeAmbiguous: true, requireOneOfEach: true, wordCount: 4, wordSeparator: '.', capitalizeWords: false, appendNumber: true, format: 'hex' } },
  ...['English', 'Czech', 'French', 'Italian', 'Japanese', 'Korean', 'Portuguese', 'Spanish', 'Chinese simplified', 'Chinese traditional'].map(language => ({ operation: 'bip39_private', arguments: { entropy: '0000000000000000', language } })),
  { operation: 'lookup_domain_dns', arguments: { name: 'nonexistent-killertools-parity.invalid', type: 'A' } },
  { operation: 'lookup_domain_rdap', arguments: { domain: 'example.com' } },
  { operation: 'lookup_cve', arguments: { query: 'CVE-2021-44228' } },
  { operation: 'generate_spf_record', arguments: { providers: ['include:_spf.google.com', 'include:_spf.google.com'], ipAddresses: ['192.0.2.0/24', '2001:db8::/32'], enforcement: '-all' } },
  { operation: 'generate_spf_record', arguments: { providers: [], ipAddresses: [], enforcement: '~all' } },
  { operation: 'generate_dmarc_record', arguments: { policy: 'quarantine', subdomainPolicy: 'reject', percentage: 50, ruaEmails: ['reports@example.com'], rufEmails: [], adkim: 's', aspf: '' } },
  { operation: 'generate_dmarc_record', arguments: { policy: 'none', subdomainPolicy: '', percentage: 100, ruaEmails: [], rufEmails: ['forensic@example.com'], adkim: '', aspf: 's' } },
  { operation: 'parse_email_headers', arguments: { headers: 'From: Sender <sender@example.com>\r\nSender: relay@other.example\r\nTo: User <user@example.net>\r\nReceived: from mail.example.com (mail.example.com [192.0.2.5]) by mx.example.net; Tue, 1 Jan 2025 12:00:10 +0000\r\nReceived: from client.example.com [198.51.100.9] by mail.example.com; Tue, 1 Jan 2025 12:00:00 +0000\r\nAuthentication-Results: mx.example; spf=pass smtp.mailfrom=example.com; dkim=fail header.d=example.com\r\nX-MS-Exchange-Organization-SCL: 5' } },
  { operation: 'test_regex', arguments: { pattern: '(cat)|(dog)', text: 'Cat dog cat', global: true, ignoreCase: true, multiline: false, dotAll: true } },
  { operation: 'test_regex', arguments: { pattern: '^.+$', text: 'one\ntwo', global: true, ignoreCase: false, multiline: true, dotAll: false } },
  { operation: 'test_regex', arguments: { pattern: 'a.*b', text: 'a\n\nb', global: false, ignoreCase: false, multiline: false, dotAll: true } },
  { operation: 'lookup_http_status', arguments: { query: 'not found', limit: 10 } },
  { operation: 'lookup_http_status', arguments: { query: 'webdav', limit: 5 } },
  { operation: 'lookup_http_status', arguments: { query: '50', limit: 3 } },
  { operation: 'lookup_port_protocol', arguments: { query: 'https', limit: 6 } },
  { operation: 'lookup_port_protocol', arguments: { query: 'dangerous', limit: 4 } },
  { operation: 'lookup_port_protocol', arguments: { query: '3389', limit: 10 } },
  { operation: 'lookup_exchange_ndr', arguments: { query: 'spf', limit: 4 } },
  { operation: 'lookup_m365_sku', arguments: { query: 'business premium', limit: 5 } },
  { operation: 'lookup_windows_error', arguments: { query: 'access denied', limit: 5 } },
  { operation: 'lookup_windows_event', arguments: { query: '4625', limit: 5 } },
  { operation: 'lookup_group_policy', arguments: { query: 'minimum password length', limit: 5 } },
  { operation: 'list_killer_modules', arguments: {} },
  { operation: 'list_killer_modules', arguments: { name: 'pivot' } },
  { operation: 'list_killer_scripts', arguments: { query: 'AMORT' } },
  { operation: 'search_powershell_cmdlets', arguments: { query: 'ActiveDirectory', limit: 20 } },
  { operation: 'get_powershell_cmdlet', arguments: { cmdlet: 'Get-ADUser' } },
  { operation: 'build_powershell_command', arguments: { cmdlet: 'Get-ADUser', parameters: { Identity: "O'Brien", Properties: 'DisplayName, mail' } } },
  { operation: 'encode_file_base64_local', arguments: { path: parityFile } },
  { operation: 'otp_private', arguments: { secret: 'GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ', mode: 'hotp', counter: 0, timeStep: 30 } },
  { operation: 'otp_private', arguments: { secret: 'GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ', mode: 'hotp', counter: 0, timeStep: 30, code: '755224' } },
  { operation: 'generate_uuids', arguments: { version: 'NIL', count: 2 } },
  { operation: 'generate_uuids', arguments: { version: 'v3', count: 1, name: 'example.com', namespace: '6ba7b810-9dad-11d1-80b4-00c04fd430c8' } },
  { operation: 'generate_uuids', arguments: { version: 'v5', count: 1, name: 'example.com', namespace: '6ba7b810-9dad-11d1-80b4-00c04fd430c8' } },
  { operation: 'generate_lorem_ipsum', arguments: { paragraphCount: 1, sentencePerParagraph: 1, wordCount: 4, startWithLoremIpsum: true, asHTML: false } },
  { operation: 'parse_jwt_private', arguments: { token: 'eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.eyJzdWIiOiJsb2NhbCIsInJvbGVzIjpbImFkbWluIl19.x' } },
  ...['MD5', 'RIPEMD160', 'SHA1', 'SHA3', 'SHA224', 'SHA256', 'SHA384', 'SHA512'].map(algorithm => ({ operation: 'hash_text_private', arguments: { value: 'abc', algorithm, encoding: 'Hex' } })),
  ...['MD5', 'RIPEMD160', 'SHA1', 'SHA3', 'SHA224', 'SHA256', 'SHA384', 'SHA512'].map(algorithm => ({ operation: 'hmac_private', arguments: { value: 'abc', secret: 'key', algorithm, encoding: 'Base64url' } })),
  { operation: 'bcrypt_private', arguments: { value: 'private', action: 'compare', rounds: 4, hash: '$2b$04$abcdefghijklmnopqrstuuSOK8qq7pwSzIsE/gr5lck387Zp7ucXu' } },
  { operation: 'analyze_password_private', arguments: { password: 'Secret123!' } },
  { operation: 'analyze_password_private', arguments: { password: '' } },
  { operation: 'evaluate_math', arguments: { expression: '2 + 3 * 4' } },
  { operation: 'evaluate_math', arguments: { expression: 'sqrt(81) + max(2, 5)' } },
  { operation: 'evaluate_math', arguments: { expression: 'sin(pi / 2)' } },
  { operation: 'evaluate_math', arguments: { expression: '2^10' } },
  { operation: 'list_film_stocks', arguments: {} },
  { operation: 'calculate_reciprocity', arguments: { filmStockId: 'ilford-hp5', meteredSeconds: 10 } },
  { operation: 'calculate_reciprocity', arguments: { filmStockId: 'fuji-acros100ii', meteredSeconds: 120 } },
  { operation: 'list_film_development_options', arguments: {} },
  { operation: 'calculate_film_development', arguments: { filmName: 'Ilford HP5 Plus', developerId: 'd76' } },
  { operation: 'calculate_film_development', arguments: { filmName: 'Ilford HP5 Plus', developerId: 'd76', dilutionIndex: 1, temperatureC: 24, pushPullStops: 1, tankMl: 501 } },
];

const oracleOperations = new Set(['convert_date_time', 'calculate_nd_exposure', 'calculate_exposure_equivalence', 'calculate_depth_of_field', 'escape_html_entities', 'unescape_html_entities', 'markdown_to_html', 'diff_text', 'expand_ipv4_range', 'calculate_ipv4_subnet', 'generate_ipv6_ula', 'format_json', 'minify_json', 'diff_json', 'convert_xml_json', 'format_xml', 'convert_json', 'convert_yaml', 'convert_toml', 'format_yaml', 'generate_meta_tags', 'search_gifs', 'describe_cron', 'json_to_csv', 'parse_url', 'parse_user_agent', 'generate_qr_code', 'lookup_domain_dns', 'lookup_domain_rdap', 'lookup_cve', 'generate_spf_record', 'generate_dmarc_record', 'parse_email_headers', 'test_regex', 'lookup_http_status', 'lookup_port_protocol', 'lookup_exchange_ndr', 'lookup_m365_sku', 'lookup_windows_error', 'lookup_windows_event', 'lookup_group_policy', 'list_killer_modules', 'list_killer_scripts', 'search_powershell_cmdlets', 'get_powershell_cmdlet', 'build_powershell_command', 'encode_file_base64_local', 'otp_private', 'generate_uuids', 'generate_lorem_ipsum', 'parse_jwt_private', 'hash_text_private', 'hmac_private', 'bcrypt_private', 'generate_password_private', 'bip39_private', 'analyze_password_private', 'evaluate_math', 'list_film_stocks', 'calculate_reciprocity', 'list_film_development_options', 'calculate_film_development']);
const oracle = spawn(process.execPath, [resolve(site, 'mcp/dist/killermcp.mjs')], { cwd: resolve(site, 'mcp'), stdio: ['pipe', 'pipe', 'pipe'] });
let oracleBuffer = '';
let oracleError = '';
let nextId = 1;
const pending = new Map();
oracle.stderr.on('data', chunk => oracleError += chunk);
oracle.stdout.on('data', (chunk) => {
  oracleBuffer += chunk;
  while (oracleBuffer.includes('\n')) {
    const newline = oracleBuffer.indexOf('\n');
    const line = oracleBuffer.slice(0, newline);
    oracleBuffer = oracleBuffer.slice(newline + 1);
    if (!line.trim()) continue;
    const message = JSON.parse(line);
    pending.get(message.id)?.(message);
    pending.delete(message.id);
  }
});
function oracleRequest(method, params = {}) {
  const id = nextId++;
  return new Promise((resolveRequest, reject) => {
    const timeout = setTimeout(() => { pending.delete(id); reject(new Error(`MCP oracle timed out: ${oracleError}`)); }, 30000);
    pending.set(id, (message) => { clearTimeout(timeout); resolveRequest(message); });
    oracle.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', id, method, params })}\n`);
  });
}
async function oracleCall(name, args, allowError = false) {
  const response = await oracleRequest('tools/call', { name, arguments: args });
  if (allowError && response.result?.isError) return { error: response.result.content[0].text };
  assert.ok(response.result && !response.result.isError, JSON.stringify(response));
  return JSON.parse(response.result.content[0].text);
}

await oracleRequest('initialize', { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'native-parity', version: '0.1.0' } });
oracle.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', method: 'notifications/initialized' })}\n`);

const expected = [];
for (const { operation, arguments: input } of requests) {
  if (operation === 'search_gifs') {
    expected.push(await oracleCall(operation, input, true));
    continue;
  }
  if (oracleOperations.has(operation)) {
    expected.push(await oracleCall(operation, input));
    continue;
  }
  if (operation === 'text_statistics') {
    expected.push(statistics.getTextStatistics(input.text)); continue;
  }
  if (operation === 'convert_case') { expected.push(caseConverter.convertCase(input.text)); continue; }
  if (operation === 'encode_base64') { expected.push({ encoded: base64.textToBase64(input.text, { makeUrlSafe: input.urlSafe }) }); continue; }
  if (operation === 'decode_base64') { expected.push({ text: base64.base64ToText(input.encoded, { makeUrlSafe: input.urlSafe }) }); continue; }
  if (operation === 'text_to_ascii_binary') { expected.push({ binary: binary.convertTextToAsciiBinary(input.text) }); continue; }
  if (operation === 'ascii_binary_to_text') { expected.push({ text: binary.convertAsciiBinaryToText(input.binary) }); continue; }
  if (operation === 'arabic_to_roman') { expected.push({ roman: roman.arabicToRoman(input.number) }); continue; }
  if (operation === 'roman_to_arabic') { expected.push({ number: roman.romanToArabic(input.roman) }); continue; }
  if (operation === 'text_to_nato_alphabet') { expected.push({ nato: nato.textToNatoAlphabet({ text: input.text }).split(' ').filter(Boolean).join(' ') }); continue; }
  if (operation === 'convert_integer_base') { expected.push({ value: integerBase.convertBase(input) }); continue; }
  if (operation === 'convert_temperature') {
    const pairs = {
      kelvin: [value => value, value => value], celsius: [temperatures.convertCelsiusToKelvin, temperatures.convertKelvinToCelsius],
      fahrenheit: [temperatures.convertFahrenheitToKelvin, temperatures.convertKelvinToFahrenheit], rankine: [temperatures.convertRankineToKelvin, temperatures.convertKelvinToRankine],
      delisle: [temperatures.convertDelisleToKelvin, temperatures.convertKelvinToDelisle], newton: [temperatures.convertNewtonToKelvin, temperatures.convertKelvinToNewton],
      reaumur: [temperatures.convertReaumurToKelvin, temperatures.convertKelvinToReaumur], romer: [temperatures.convertRomerToKelvin, temperatures.convertKelvinToRomer],
    };
    const kelvin = pairs[input.scale][0](input.value);
    expected.push(Object.fromEntries(Object.entries(pairs).map(([scale, pair]) => [scale, scale === input.scale ? input.value : Math.floor(pair[1](kelvin) * 100) / 100]))); continue;
  }
  if (operation === 'calculate_chmod') {
    const octal = chmod.computeChmodOctalRepresentation(input);
    expected.push({ octal, symbolic: chmod.computeChmodSymbolicRepresentation(input), command: `chmod ${octal} path` }); continue;
  }
  if (operation === 'generate_svg_placeholder') {
    const value = svg.buildSvgPlaceholder(input);
    expected.push({ svg: value, dataUrl: `data:image/svg+xml;base64,${base64.textToBase64(value)}` }); continue;
  }
  const value = input.mode === 'percent_of'
    ? percentages.percentageOf(input.x, input.y)
    : input.mode === 'what_percent'
      ? percentages.percentageRatio(input.x, input.y)
      : percentages.percentageChange(input.x, input.y);
  expected.push({ value });
}
oracle.stdin.end();
oracle.kill();

const run = spawnSync('dotnet', [
  'run',
  '--project',
  'tests/KillerTools.Engine.ParityHost/KillerTools.Engine.ParityHost.csproj',
  '--no-build',
], { cwd: resolve(import.meta.dirname, '..'), input: JSON.stringify(requests), encoding: 'utf8' });

if (run.status !== 0) {
  throw new Error(run.stderr || `Native parity host exited with code ${run.status}`);
}

function normalizeDynamicResults(values) {
  return values.map((value, index) => {
    const request = requests[index];
    if (request.operation === 'generate_qr_code') return { valid: typeof value.svg === 'string' && value.svg.startsWith('<svg') && value.svg.length <= 65536 };
    if (request.operation === 'lookup_domain_rdap') return { ...value, events: value.events.map(event => event.eventAction === 'last update of RDAP database' ? { ...event, eventDate: '<dynamic>' } : event) };
    if (request.operation !== 'generate_password_private') return value;
    const password = value.password;
    if (request.arguments.mode === 'random') return { valid: password.length === 20 && /[A-Z]/.test(password) && /[a-z]/.test(password) && /[0-9]/.test(password) && /[^A-Za-z0-9]/.test(password) && !/[0O1lI|`'".,;:{}[\]()\\/]/.test(password) };
    if (request.arguments.mode === 'pronounceable') return { valid: /^[bcdfghjkmnpqrstvwxz][aeiouy](?:[bcdfghjkmnpqrstvwxz][aeiouy]){8}[0-9]{2}$/.test(password) };
    if (request.arguments.mode === 'format') return { valid: /^[A-Za-z0-9_-]{20}$/.test(password) };
    const parts = password.split('.');
    return { valid: parts.length === 5 && parts.slice(0, 4).every(part => /^[a-z]+$/.test(part)) && /^[0-9]{2}$/.test(parts[4]) };
  });
}

assert.deepEqual(normalizeDynamicResults(JSON.parse(run.stdout)), normalizeDynamicResults(expected));
await rm(parityDirectory, { recursive: true, force: true });
console.log(`${requests.length} live JavaScript parity cases passed for ${new Set(requests.map(request => request.operation)).size} native operations.`);
