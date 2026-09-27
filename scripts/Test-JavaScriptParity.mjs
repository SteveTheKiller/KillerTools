import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
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
];

const expected = requests.map(({ operation, arguments: input }) => {
  if (operation === 'text_statistics') {
    return statistics.getTextStatistics(input.text);
  }
  if (operation === 'encode_base64') return { encoded: base64.textToBase64(input.text, { makeUrlSafe: input.urlSafe }) };
  if (operation === 'decode_base64') return { text: base64.base64ToText(input.encoded, { makeUrlSafe: input.urlSafe }) };
  if (operation === 'text_to_ascii_binary') return { binary: binary.convertTextToAsciiBinary(input.text) };
  if (operation === 'ascii_binary_to_text') return { text: binary.convertAsciiBinaryToText(input.binary) };
  if (operation === 'arabic_to_roman') return { roman: roman.arabicToRoman(input.number) };
  if (operation === 'roman_to_arabic') return { number: roman.romanToArabic(input.roman) };
  if (operation === 'text_to_nato_alphabet') return { nato: nato.textToNatoAlphabet({ text: input.text }).split(' ').filter(Boolean).join(' ') };
  if (operation === 'convert_integer_base') return { value: integerBase.convertBase(input) };
  const value = input.mode === 'percent_of'
    ? percentages.percentageOf(input.x, input.y)
    : input.mode === 'what_percent'
      ? percentages.percentageRatio(input.x, input.y)
      : percentages.percentageChange(input.x, input.y);
  return { value };
});

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
console.log(`${requests.length} live JavaScript parity cases passed for 10 native operations.`);
