"use strict";

const path = require("path");
const vscode = require("vscode");
const features = require("./src/features");
const sforge = require("./src/sforge");
const { Checker } = require("./src/diagnostics");

const TYPING_DELAY_MS = 400;

function activate(context) {
    const output = vscode.window.createOutputChannel("BDSL");
    const checker = new Checker(context.extensionPath, output);
    context.subscriptions.push(output, checker);

    features.register(context);

    const config = () => vscode.workspace.getConfiguration("bdsl");
    context.subscriptions.push(
        vscode.workspace.onDidOpenTextDocument((doc) => checker.schedule(doc, 0)),
        vscode.workspace.onDidSaveTextDocument((doc) => checker.schedule(doc, 0)),
        vscode.workspace.onDidChangeTextDocument((e) => {
            if (config().get("check.onType")) checker.schedule(e.document, TYPING_DELAY_MS);
        }),
        vscode.workspace.onDidCloseTextDocument((doc) => checker.forget(doc)),
        vscode.workspace.onDidChangeConfiguration((e) => {
            if (!e.affectsConfiguration("bdsl")) return;
            checker.clearAll();
            vscode.workspace.textDocuments.forEach((doc) => checker.schedule(doc, 0));
        }),
        vscode.commands.registerCommand("bdsl.compile", () => compile(context, output)),
    );

    vscode.workspace.textDocuments.forEach((doc) => checker.schedule(doc, 0));
}

/** Saves the active BDSL file and compiles it to an .rpj beside it. */
async function compile(context, output) {
    const editor = vscode.window.activeTextEditor;
    if (!editor || editor.document.languageId !== "bdsl") return;
    const document = editor.document;
    if (document.isUntitled) {
        vscode.window.showWarningMessage("BDSL: save the file before compiling.");
        return;
    }
    if (document.isDirty && !(await document.save())) return;

    const exe = sforge.find(context.extensionPath);
    if (!exe) {
        vscode.window.showErrorMessage("BDSL: sforge was not found. Build src/ShadowForge.CLI or set bdsl.sforgePath.");
        return;
    }

    const file = document.uri.fsPath;
    const target = file.replace(/\.bdsl$/i, "") + ".rpj";
    const result = await sforge.run(exe, ["rpj", "compile", file, "-o", target]);
    output.appendLine(`> sforge rpj compile ${file}`);
    output.append(result.stdout + result.stderr);
    if (result.code === 0)
        vscode.window.showInformationMessage(`BDSL: compiled to ${path.basename(target)}.`);
    else
        vscode.window.showErrorMessage("BDSL: compile failed. See the BDSL output for details.", "Show Output")
            .then((choice) => { if (choice) output.show(); });
}

function deactivate() {}

module.exports = { activate, deactivate };
