// Outline, hover, completion and label navigation for BDSL documents.

"use strict";

const vscode = require("vscode");
const structure = require("./structure");
const opcodeList = require("../data/opcodes.json");

const opcodes = new Map(opcodeList.map((op) => [op.name, op]));
const PARTY = ["shu", "jiro", "kluke", "marumaro", "zola", "all"];
const WORD = /@\d+|[A-Za-z_][A-Za-z0-9_]*/;

// Parsing is cheap, but hover and completion ask for it on every keystroke.
const trees = new WeakMap();
function treeOf(document) {
    const cached = trees.get(document);
    if (cached && cached.version === document.version) return cached.root;
    const root = structure.parse(document.getText().split(/\r?\n/));
    trees.set(document, { version: document.version, root });
    return root;
}

// ---------------------------------------------------------------- docs

const WORD_DOCS = {
    when: "Script block. Runs when every condition holds: `chapter A..B`, `var[N] op V`, `item_count(N) < V`, `party_all(...)`, `party_missing(...)`, `savebit(N)`, `global_4095` or `cond(type, operand, op, value)`, joined by `and`. At most 8 condition slots.",
    and: "Joins when-block conditions.",
    chapter: "In a when header, `chapter A..B` limits the block to a chapter range. In an if, compares the current chapter.",
    if: "`if cond { ... }` runs the body when the condition holds. `if cond then goto @N else end` is the flat form.",
    then: "Action taken when a flat if's condition holds: `end` or `goto @N`.",
    else: "Action taken when a flat if's condition fails: `end` or `goto @N`.",
    goto: "Jumps to a label in the same when-block.",
    end: "Ends the script.",
    init: "`init { ... }` wraps statements between init_begin (5057) and init_end (5058).",
    on_win: "Action after winning the battle: `end` or `goto @N`.",
    on_lose: "Action after losing the battle: `end` or `goto @N`.",
    raw: "`raw HEX` holds instruction bytes that did not decode.",
    param_data: "`param_data HEX` holds the when-block's trailing parameter bytes.",
    item_count: "`item_count(item) < n` or `>= n`: how many of an item the party holds.",
    inventory_count: "`inventory_count(item) < n` or `>= n`: item count, inventory sub-type.",
    party_all: "`party_all(members...)`: every listed member is in the party.",
    party_missing: "`party_missing(members...)`: the listed members are not all in the party.",
    savebit: "`savebit(N)`: global save bit N is set.",
    global_4095: "Global flag condition (type 5, operand 2, op 1).",
    cond: "`cond(type, operand, op, value)`: a raw condition slot.",
    in_party: "`in_party(member)`, or `!in_party(member)`: whether a character is in the party.",
    overflow: "`overflow(item N, +amount)`, `overflow(gold, +amount)` or `overflow(medals, +amount)`: adding would exceed the cap. `if !overflow(...) { }` runs the body when it fits.",
    random: "`random(min, max)`: random value for an assignment.",
    chara: "`chara(member, property)`: a character property for an assignment.",
    var: "`var[N]`: script variable N.",
    flag: "`flag[N]`: flag N. `flag[N] = V` writes it; `var[M] = flag[N]` reads it.",
    playtime: "Play time.",
    realtime: "Real-time clock.",
    leader_id: "Party leader's character index.",
    party_count: "Number of party members.",
    gold: "Party gold.",
    medals: "Medal count.",
    encounters: "Encounter count.",
};

const DIRECTIVES = {
    when: {
        block_type: { doc: "Block type.", values: ["event", "npc", "auto_run", "link", "cube"] },
        render: { doc: "Render flags, comma separated.", values: ["visible", "type_b", "variant_b", "variant_a", "special_anim", "collision", "has_model"] },
        behavior: { doc: "Behavior mode.", values: ["default", "alternate", "disabled"] },
        auto_run: { doc: "Auto-run flags." },
        encounter: { doc: "Encounter mode.", values: ["normal", "boss"] },
        encounter_range: { doc: "Encounter range (float)." },
        auto_set_var: { doc: "Variable set automatically when the block runs." },
        auto_set_var_index: { doc: "Auto-set variable index without the enable flag." },
        linked_entry: { doc: "Linked entry id." },
        spawn_flags: { doc: "Spawn flags." },
        spawn_angle: { doc: "Spawn angle (float)." },
        spawn_scale: { doc: "Spawn scale (float)." },
        interaction_radius: { doc: "Interaction radius (float)." },
        type_data: { doc: "Type-specific data word." },
        spawn_mode: { doc: "Spawn mode." },
        raw_header: { doc: "`@raw_header offset value`: an unnamed header word, by block offset." },
    },
    entry: {
        runtime_ref: { doc: "Runtime reference word." },
        target_refs: { doc: "Three raw target reference words (entries other than box, enemy and warp)." },
        field_44: { doc: "Raw trigger-radius field (entries other than box, enemy and warp)." },
        padding: { doc: "Four raw words, as one hex run." },
        overlap: { doc: "Three raw words, as one hex run." },
    },
    waypoint: {
        condition_value: { doc: "Condition value without a condition type." },
        unk_field_20: { doc: "Raw word at 0x20." },
        unk_field_24: { doc: "Raw word at 0x24." },
        unk_field_28: { doc: "Raw word at 0x28." },
        unk_field_2c: { doc: "Raw word at 0x2C." },
        ref_data: { doc: "Raw words 0x20..0x2C, as one hex run." },
        ref_data_extra: { doc: "Raw words after the resource name, as one hex run." },
    },
};

function opcodeMarkdown(op) {
    const md = new vscode.MarkdownString();
    md.appendCodeblock(`${op.name}(${op.args.map((a) => `${a.name}=${a.kind}`).join(", ")})`, "bdsl");
    md.appendMarkdown(`Opcode **${op.opcode}**.`);
    if (op.sugared) md.appendMarkdown(" Usually written with dedicated syntax.");
    else if (op.args.length) md.appendMarkdown(" Zero-valued arguments may be omitted.");
    return md;
}

// ---------------------------------------------------------------- call context

/**
 * The opcode call containing the column, or null: { name, open, used, argStart, argText }
 * where argText is the current argument up to the column.
 */
function callAt(lineText, column) {
    const before = lineText.slice(0, column);
    let depth = 0;
    for (let i = before.length - 1; i >= 0; i--) {
        const ch = before[i];
        if (ch === ")") depth++;
        else if (ch === "(") {
            if (depth > 0) { depth--; continue; }
            const name = /([A-Za-z_][A-Za-z0-9_]*)\s*$/.exec(before.slice(0, i));
            if (!name) return null;
            const inner = before.slice(i + 1);
            const comma = inner.lastIndexOf(",");
            const close = lineText.indexOf(")", column);
            const all = lineText.slice(i + 1, close < 0 ? lineText.length : close);
            const used = new Set([...all.matchAll(/([A-Za-z_][A-Za-z0-9_]*)\s*=/g)].map((m) => m[1]));
            return { name: name[1], argText: inner.slice(comma + 1).trimStart(), used };
        }
    }
    return null;
}

// ---------------------------------------------------------------- labels

/** Every @N occurrence in the when-block containing the line, as { range, isDefinition }. */
function labelOccurrences(document, line, id) {
    const block = structure.enclosing(treeOf(document), line, "when");
    if (!block) return [];
    const result = [];
    const pattern = new RegExp(`@${id}(?!\\d)`, "g");
    for (let i = block.line + 1; i < block.endLine; i++) {
        const text = document.lineAt(i).text;
        const code = structure.codeOf(text);
        for (const m of code.matchAll(pattern)) {
            const isDefinition = /^\s*!?$/.test(code.slice(0, m.index)) && code[m.index + m[0].length] === ":";
            result.push({ range: new vscode.Range(i, m.index, i, m.index + m[0].length), isDefinition });
        }
    }
    return result;
}

function labelAt(document, position) {
    const range = document.getWordRangeAtPosition(position, /@\d+/);
    if (!range) return null;
    const code = structure.codeOf(document.lineAt(position.line).text);
    if (range.end.character > code.length) return null;
    return { range, id: Number(document.getText(range).slice(1)) };
}

// ---------------------------------------------------------------- providers

const SYMBOL_KINDS = {
    scene: vscode.SymbolKind.Namespace,
    entry: vscode.SymbolKind.Object,
    waypoint: vscode.SymbolKind.Struct,
    when: vscode.SymbolKind.Event,
    label: vscode.SymbolKind.Key,
};

function toSymbols(document, nodes) {
    const symbols = [];
    for (const node of nodes) {
        const kind = SYMBOL_KINDS[node.kind];
        // if/init blocks are not shown; their labels move up to the when-block.
        if (kind === undefined) {
            symbols.push(...toSymbols(document, node.children));
            continue;
        }
        const header = document.lineAt(node.line);
        const selection = new vscode.Range(node.line, header.firstNonWhitespaceCharacterIndex, node.line, header.text.length);
        const full = new vscode.Range(node.line, 0, node.endLine, document.lineAt(node.endLine).text.length);
        const symbol = new vscode.DocumentSymbol(node.name || "(unnamed)", node.detail || "", kind, full, selection);
        symbol.children = toSymbols(document, node.children);
        symbols.push(symbol);
    }
    return symbols;
}

const symbolProvider = {
    provideDocumentSymbols(document) {
        return toSymbols(document, treeOf(document).children);
    },
};

const hoverProvider = {
    provideHover(document, position) {
        const label = labelAt(document, position);
        if (label) {
            const uses = labelOccurrences(document, position.line, label.id);
            const def = uses.find((u) => u.isDefinition);
            const where = def ? `defined on line ${def.range.start.line + 1}` : "not defined in this block";
            return new vscode.Hover(`Label **@${label.id}**, ${where}. ${uses.length - (def ? 1 : 0)} reference(s).`, label.range);
        }

        const range = document.getWordRangeAtPosition(position, WORD);
        if (!range) return null;
        const lineText = document.lineAt(position.line).text;
        if (range.end.character > structure.codeOf(lineText).length) return null;
        const word = document.getText(range);
        const after = lineText.slice(range.end.character);

        const op = opcodes.get(word);
        if (op && /^\s*\(/.test(after)) return new vscode.Hover(opcodeMarkdown(op), range);

        const call = callAt(lineText, range.start.character);
        if (call && /^\s*=/.test(after)) {
            const arg = opcodes.get(call.name)?.args.find((a) => a.name === word);
            if (arg) return new vscode.Hover(`\`${word}\`: ${arg.kind} argument of \`${call.name}\``, range);
        }

        if (lineText[range.start.character - 1] === "@") {
            const scope = scopeOf(document, position.line);
            const directive = DIRECTIVES[scope]?.[word];
            if (directive) return new vscode.Hover(`**@${word}**: ${directive.doc}`, range);
        }

        if (PARTY.includes(word)) return new vscode.Hover(word === "all" ? "The whole party." : `Party member **${word}**.`, range);
        if (WORD_DOCS[word]) return new vscode.Hover(WORD_DOCS[word], range);
        return null;
    },
};

/** Which directive set applies on the line: when, entry, waypoint or null. */
function scopeOf(document, line) {
    const root = treeOf(document);
    if (structure.enclosing(root, line, "when")) return "when";
    if (structure.enclosing(root, line, "entry")) return "entry";
    if (structure.enclosing(root, line, "waypoint")) return "waypoint";
    return null;
}

const completionProvider = {
    provideCompletionItems(document, position) {
        const lineText = document.lineAt(position.line).text;
        const before = lineText.slice(0, position.character);
        if (before.length > structure.codeOf(lineText).length) return null;

        // @directive names, then their keyword values.
        const directiveName = /^\s*@([A-Za-z_]\w*)?$/.exec(before);
        if (directiveName) {
            const set = DIRECTIVES[scopeOf(document, position.line)];
            if (!set) return null;
            return Object.entries(set).map(([name, d]) => {
                const item = new vscode.CompletionItem(name, vscode.CompletionItemKind.Property);
                item.documentation = new vscode.MarkdownString(d.doc);
                item.insertText = name + " ";
                return item;
            });
        }
        const directiveValue = /^\s*@([A-Za-z_]\w*)\s+(?:[\w]+\s*,\s*)*\w*$/.exec(before);
        if (directiveValue) {
            const values = DIRECTIVES[scopeOf(document, position.line)]?.[directiveValue[1]]?.values;
            return values?.map((v) => new vscode.CompletionItem(v, vscode.CompletionItemKind.EnumMember)) ?? null;
        }

        const call = callAt(lineText, position.character);
        if (!call) return null;
        const op = opcodes.get(call.name);

        // Value position: party names for a party argument, and anywhere a party name fits.
        const valueOf = /^([A-Za-z_][A-Za-z0-9_]*)\s*=\s*\w*$/.exec(call.argText);
        if (valueOf) {
            const arg = op?.args.find((a) => a.name === valueOf[1]);
            if (arg?.kind !== "party") return null;
            return PARTY.map((p) => new vscode.CompletionItem(p, vscode.CompletionItemKind.EnumMember));
        }
        if (["party_all", "party_missing", "in_party", "chara"].includes(call.name))
            return PARTY.filter((p) => p !== "all" || call.name === "in_party")
                .map((p) => new vscode.CompletionItem(p, vscode.CompletionItemKind.EnumMember));

        // Argument names not yet used in this call, in table order.
        if (!op || !/^[A-Za-z_]*$/.test(call.argText)) return null;
        const items = op.args
            .filter((a) => !call.used.has(a.name) || a.name === call.argText)
            .map((a, i) => {
                const item = new vscode.CompletionItem(a.name, vscode.CompletionItemKind.Field);
                item.detail = a.kind;
                item.insertText = new vscode.SnippetString(`${a.name}=\${1}`);
                item.sortText = String(i).padStart(3, "0");
                item.command = a.kind === "party" ? { command: "editor.action.triggerSuggest", title: "" } : undefined;
                return item;
            });
        return new vscode.CompletionList(items, false);
    },
};

const definitionProvider = {
    provideDefinition(document, position) {
        const label = labelAt(document, position);
        if (!label) return null;
        const def = labelOccurrences(document, position.line, label.id).find((u) => u.isDefinition);
        return def ? new vscode.Location(document.uri, def.range) : null;
    },
};

const referenceProvider = {
    provideReferences(document, position, context) {
        const label = labelAt(document, position);
        if (!label) return null;
        return labelOccurrences(document, position.line, label.id)
            .filter((u) => context.includeDeclaration || !u.isDefinition)
            .map((u) => new vscode.Location(document.uri, u.range));
    },
};

const highlightProvider = {
    provideDocumentHighlights(document, position) {
        const label = labelAt(document, position);
        if (!label) return null;
        return labelOccurrences(document, position.line, label.id).map((u) => new vscode.DocumentHighlight(
            u.range, u.isDefinition ? vscode.DocumentHighlightKind.Write : vscode.DocumentHighlightKind.Read));
    },
};

const renameProvider = {
    prepareRename(document, position) {
        const label = labelAt(document, position);
        if (!label) throw new Error("Only labels (@N) can be renamed.");
        return { range: label.range, placeholder: `@${label.id}` };
    },
    provideRenameEdits(document, position, newName) {
        const label = labelAt(document, position);
        if (!label) return null;
        const m = /^@?(\d+)$/.exec(newName.trim());
        if (!m) throw new Error("A label is @ followed by a number.");
        const edit = new vscode.WorkspaceEdit();
        for (const u of labelOccurrences(document, position.line, label.id))
            edit.replace(document.uri, u.range, `@${m[1]}`);
        return edit;
    },
};

function register(context) {
    const selector = { language: "bdsl" };
    context.subscriptions.push(
        vscode.languages.registerDocumentSymbolProvider(selector, symbolProvider),
        vscode.languages.registerHoverProvider(selector, hoverProvider),
        vscode.languages.registerCompletionItemProvider(selector, completionProvider, "(", ",", " ", "=", "@"),
        vscode.languages.registerDefinitionProvider(selector, definitionProvider),
        vscode.languages.registerReferenceProvider(selector, referenceProvider),
        vscode.languages.registerDocumentHighlightProvider(selector, highlightProvider),
        vscode.languages.registerRenameProvider(selector, renameProvider),
    );
}

module.exports = { register, callAt };
