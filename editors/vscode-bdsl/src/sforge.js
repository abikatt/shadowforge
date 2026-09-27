// Locates and runs the sforge CLI.

"use strict";

const cp = require("child_process");
const fs = require("fs");
const path = require("path");
const vscode = require("vscode");

const EXE = process.platform === "win32" ? "sforge.exe" : "sforge";

/**
 * The bdsl.sforgePath setting, else the newest sforge build in this repository
 * (the extension lives at editors/vscode-bdsl), else sforge on PATH. Null when none exists.
 */
function find(extensionPath) {
    const configured = vscode.workspace.getConfiguration("bdsl").get("sforgePath");
    if (configured) return configured;

    let real = extensionPath;
    try { real = fs.realpathSync(extensionPath); } catch { /* keep the link path */ }
    const binRoot = path.resolve(real, "..", "..", "src", "ShadowForge.CLI", "bin");

    let best = null;
    let bestTime = 0;
    for (const config of ["Release", "Debug"]) {
        let frameworks;
        try { frameworks = fs.readdirSync(path.join(binRoot, config)); } catch { continue; }
        for (const framework of frameworks) {
            const candidate = path.join(binRoot, config, framework, EXE);
            try {
                const time = fs.statSync(candidate).mtimeMs;
                if (time > bestTime) { best = candidate; bestTime = time; }
            } catch { /* not built for this framework */ }
        }
    }
    if (best) return best;

    for (const dir of (process.env.PATH || "").split(path.delimiter)) {
        const candidate = path.join(dir, EXE);
        if (dir && fs.existsSync(candidate)) return candidate;
    }
    return null;
}

/**
 * Runs sforge with the arguments and optional stdin text. Resolves to
 * { code, stdout, stderr }; rejects only when the process cannot start or times out.
 */
function run(exe, args, input, timeoutMs = 15000) {
    return new Promise((resolve, reject) => {
        const child = cp.spawn(exe, args, { windowsHide: true });
        let stdout = "";
        let stderr = "";
        const timer = setTimeout(() => {
            child.kill();
            reject(new Error(`sforge ${args.join(" ")} timed out`));
        }, timeoutMs);

        child.stdout.setEncoding("utf8").on("data", (d) => { stdout += d; });
        child.stderr.setEncoding("utf8").on("data", (d) => { stderr += d; });
        child.on("error", (err) => { clearTimeout(timer); reject(err); });
        child.on("close", (code) => { clearTimeout(timer); resolve({ code, stdout, stderr }); });

        if (input !== undefined) child.stdin.end(input, "utf8");
        else child.stdin.end();
    });
}

module.exports = { find, run };
