// Line-level outline of a BDSL document: scene header, entries, when-blocks, nested if/init
// blocks, waypoints and labels. Mirrors the reader's rules closely enough for navigation;
// the compiler (sforge rpj check) remains the authority on validity.

"use strict";

const ENTRY_TYPES = "spawn|box|zone|enemy|link|entity|warp|entry_\\d+";

const HEADERS = [
    { kind: "scene", re: /^scene\s+("(?:[^"\\]|\\.)*")/ },
    { kind: "entry", re: new RegExp(`^(${ENTRY_TYPES})\\s+("(?:[^"\\\\]|\\\\.)*")(.*)$`) },
    { kind: "waypoint", re: /^waypoint\s+(\S+)(.*)$/ },
    { kind: "when", re: /^when\b\s*(.*?)\s*$/ },
    { kind: "init", re: /^init$/ },
    { kind: "if", re: /^if\s+(.*?)\s*$/ },
];

/**
 * The line with its note comment and string contents blanked, so braces and '#' inside
 * strings are not counted. A '//' comment line comes back empty.
 */
function codeOf(line) {
    if (line.trimStart().startsWith("//")) return "";
    let out = "";
    let inQuote = false;
    for (let i = 0; i < line.length; i++) {
        const ch = line[i];
        if (inQuote) {
            if (ch === "\\" && i + 1 < line.length) { out += "  "; i++; continue; }
            if (ch === '"') { inQuote = false; out += ch; continue; }
            out += " ";
            continue;
        }
        if (ch === "#") break;
        if (ch === '"') inQuote = true;
        out += ch;
    }
    return out;
}

/** The line without its '#' note, trimmed, with string contents kept. */
function cleanLine(line) {
    const code = codeOf(line);
    return line.slice(0, code.length).trim();
}

function unquote(text) {
    return text.slice(1, -1).replace(/\\(.)/g, "$1");
}

/**
 * Parses the document into a tree of { kind, name, detail, line, endLine, children }.
 * Lines are 0-based. A block left open ends on the last line.
 */
function parse(lines) {
    const root = { kind: "root", children: [], line: 0, endLine: lines.length - 1 };
    const stack = [root];

    for (let i = 0; i < lines.length; i++) {
        const code = codeOf(lines[i]).trim();
        if (code.length === 0) continue;
        const top = stack[stack.length - 1];

        const label = /^!?@(\d+):$/.exec(code);
        if (label) {
            top.children.push({ kind: "label", name: `@${label[1]}`, id: Number(label[1]), line: i, endLine: i, children: [] });
            continue;
        }

        if (code.endsWith("{")) {
            const head = cleanLine(lines[i]).slice(0, -1).trim();
            stack.push(openNode(top, head, i));
            continue;
        }

        if (code === "}" || code === "};") {
            if (stack.length > 1) stack.pop().endLine = i;
        }
    }

    const last = lines.length - 1;
    while (stack.length > 1) stack.pop().endLine = last;
    return root;
}

function openNode(parent, head, line) {
    const node = { kind: "block", name: head, detail: "", line, endLine: line, children: [] };
    for (const { kind, re } of HEADERS) {
        const m = re.exec(head);
        if (!m) continue;
        node.kind = kind;
        switch (kind) {
            case "scene":
                node.name = unquote(m[1]);
                break;
            case "entry":
                node.type = m[1];
                node.name = unquote(m[2]);
                node.detail = `${m[1]} ${m[3].trim()}`.trim();
                break;
            case "waypoint":
                node.name = `waypoint ${m[1]}`;
                node.detail = m[2].trim();
                break;
            case "when":
                node.name = m[1].length > 0 ? `when ${m[1]}` : "when";
                break;
            case "if":
                node.name = `if ${m[1]}`;
                break;
        }
        break;
    }
    parent.children.push(node);
    return node;
}

/** The innermost node of the given kind containing the line, or null. */
function enclosing(root, line, kind) {
    let found = null;
    let nodes = root.children;
    for (;;) {
        const next = nodes.find((n) => n.line <= line && line <= n.endLine && n.children);
        if (!next) return found;
        if (next.kind === kind) found = next;
        nodes = next.children;
    }
}

module.exports = { parse, enclosing, codeOf, cleanLine };
