import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { resolve } from 'node:path';
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

const requests = [
  { operation: 'text_statistics', arguments: { text: '' } },
  { operation: 'text_statistics', arguments: { text: 'hello world' } },
  { operation: 'text_statistics', arguments: { text: 'a\r\né' } },
  { operation: 'text_statistics', arguments: { text: ' a ' } },
  { operation: 'text_statistics', arguments: { text: '😀 tools' } },
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
  { operation: 'calculate_nd_exposure', arguments: { baseSeconds: 0.5, stops: 6 } },
  { operation: 'calculate_exposure_equivalence', arguments: { shutterSeconds: 0.25, originalAperture: 4, targetAperture: 11 } },
  { operation: 'calculate_depth_of_field', arguments: { focalLengthMm: 50, aperture: 8, focusDistance: 3, focusUnit: 'm', circleOfConfusionMm: 0.03 } },
];

const oracleOperations = new Set(['calculate_nd_exposure', 'calculate_exposure_equivalence', 'calculate_depth_of_field']);
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
async function oracleCall(name, args) {
  const response = await oracleRequest('tools/call', { name, arguments: args });
  assert.ok(response.result && !response.result.isError, JSON.stringify(response));
  return JSON.parse(response.result.content[0].text);
}

await oracleRequest('initialize', { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'native-parity', version: '0.1.0' } });
oracle.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', method: 'notifications/initialized' })}\n`);

const expected = [];
for (const { operation, arguments: input } of requests) {
  if (oracleOperations.has(operation)) {
    expected.push(await oracleCall(operation, input));
    continue;
  }
  if (operation === 'text_statistics') {
    expected.push(statistics.getTextStatistics(input.text)); continue;
  }
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

assert.deepEqual(JSON.parse(run.stdout), expected);
console.log(`${requests.length} live JavaScript parity cases passed for 16 native operations.`);
