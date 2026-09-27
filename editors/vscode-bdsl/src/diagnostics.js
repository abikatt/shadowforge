// Compile-checks BDSL documents with `sforge rpj check - --json` and shows the first error.

"use strict";

const vscode = require("vscode");
const sforge = require("./sforge");

const LINE_ERROR = /^BDSL line (\d+): ([\s\S]*)$/;

class Checker {
    constructor(extensionPath, output) {
        this.extensionPath = extensionPath;
        this.output = output;
        this.collection = vscode.languages.createDiagnosticCollection("bdsl");
        this.timers = new Map();
        this.generations = new Map();
        this.warnedMissing = false;
    }

    /** Checks the document after the delay, replacing any check already scheduled for it. */
    schedule(document, delayMs) {
        if (document.languageId !== "bdsl") return;
        const key = document.uri.toString();
        clearTimeout(this.timers.get(key));
        this.timers.set(key, setTimeout(() => {
            this.timers.delete(key);
            this.check(document);
        }, delayMs));
    }

    forget(document) {
        const key = document.uri.toString();
        clearTimeout(this.timers.get(key));
        this.timers.delete(key);
        this.generations.delete(key);
        this.collection.delete(document.uri);
    }

    clearAll() {
        this.collection.clear();
    }

    async check(document) {
        if (!vscode.workspace.getConfiguration("bdsl").get("check.enabled")) {
            this.collection.delete(document.uri);
            return;
        }

        const exe = sforge.find(this.extensionPath);
        if (!exe) {
            this.warnMissing();
            return;
        }

        // A result that arrives after a newer check started is stale.
        const key = document.uri.toString();
        const generation = (this.generations.get(key) || 0) + 1;
        this.generations.set(key, generation);

        let result;
        try {
            result = await sforge.run(exe, ["rpj", "check", "-", "--json"], document.getText());
        } catch (err) {
            this.output.appendLine(`check failed to run ${exe}: ${err.message}`);
            if (err.code === "ENOENT") this.warnMissing();
            return;
        }
        if (this.generations.get(key) !== generation || document.isClosed) return;

        let envelope;
        try {
            envelope = JSON.parse(result.stdout);
        } catch {
            this.output.appendLine(`check gave unexpected output (exit ${result.code}):\n${result.stdout}${result.stderr}`);
            return;
        }

        if (envelope.ok) {
            this.collection.delete(document.uri);
            return;
        }
        this.collection.set(document.uri, [toDiagnostic(document, envelope.error.message)]);
    }

    warnMissing() {
        if (this.warnedMissing) return;
        this.warnedMissing = true;
        vscode.window.showWarningMessage(
            "BDSL: sforge was not found, so files are not checked. Build src/ShadowForge.CLI or set bdsl.sforgePath.",
            "Open Settings",
        ).then((choice) => {
            if (choice) vscode.commands.executeCommand("workbench.action.openSettings", "bdsl.sforgePath");
        });
    }

    dispose() {
        for (const timer of this.timers.values()) clearTimeout(timer);
        this.collection.dispose();
    }
}

/** Underlines the reported line's text, or the first line when the error has no line number. */
function toDiagnostic(document, message) {
    const m = LINE_ERROR.exec(message);
    let line = 0;
    if (m) {
        line = Math.min(Math.max(Number(m[1]) - 1, 0), document.lineCount - 1);
        message = m[2];
    }
    const text = document.lineAt(line);
    const start = text.firstNonWhitespaceCharacterIndex;
    const end = Math.max(text.text.trimEnd().length, start + 1);
    const diagnostic = new vscode.Diagnostic(new vscode.Range(line, start, line, end), message, vscode.DiagnosticSeverity.Error);
    diagnostic.source = "sforge";
    return diagnostic;
}

module.exports = { Checker };
