// Links this folder into the VS Code extensions directory, so edits here take effect on window
// reload. Pass --uninstall to remove the link.
//   npm run install-local

import { lstatSync, readFileSync, rmSync, symlinkSync } from "node:fs";
import { homedir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const manifest = JSON.parse(readFileSync(join(root, "package.json"), "utf8"));
const link = join(homedir(), ".vscode", "extensions", `${manifest.publisher}.${manifest.name}-${manifest.version}`);

function exists(path) {
    try { lstatSync(path); return true; } catch { return false; }
}

if (exists(link)) {
    if (!lstatSync(link).isSymbolicLink())
        throw new Error(`${link} exists and is not a link; remove it by hand`);
    rmSync(link);
}

if (process.argv.includes("--uninstall")) {
    console.log(`Removed ${link}`);
} else {
    // A junction needs no elevation on Windows; the type is ignored elsewhere.
    symlinkSync(root, link, "junction");
    console.log(`Linked ${link} -> ${root}\nReload VS Code windows to pick it up.`);
}
