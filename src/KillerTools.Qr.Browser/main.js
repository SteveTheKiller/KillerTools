import { dotnet } from './_framework/dotnet.js';

const runtime = await dotnet.create();
const exports = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
export const read = exports.KillerTools.Qr.Browser.Reader.Read;
