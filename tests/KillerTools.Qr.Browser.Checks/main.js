import { dotnet } from './_framework/dotnet.js';
const runtime = await dotnet.create();
const exports = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
console.log(exports.BrowserHarness.RunChecks());
