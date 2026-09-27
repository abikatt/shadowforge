// Generates two files from the opcode table in ShadowForge.Core. Run after changing OpcodeTable.cs:
//   npm run generate
//
// - snippets/opcodes.json: one completion snippet per opcode, with its argument names.
// - data/opcodes.json: names, numbers and arguments for hover and argument completion.

import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const tablePath = resolve(here, "../../../src/ShadowForge.Core/Scene/Script/OpcodeTable.cs");
const snippetPath = resolve(here, "../snippets/opcodes.json");
const dataPath = resolve(here, "../data/opcodes.json");

// Opcodes the writer always spells with dedicated syntax (labels, goto, end, var[N] = ...,
// if, overflow, // comments, init { }, flag[N] = ...). They get no snippet; battle has its own.
const sugared = new Set([5000, 5003, 5012, 5013, 5016, 5023, 5042, 5050, 5057, 5058, 5063]);

// battle(...) spells its win/lose action and label words as "on_win ... on_lose ...",
// matching the skip list in StatementWriter.
const battleActionArgs = new Set(["win_action", "win_label", "lose_action", "lose_label"]);

const party = "shu,jiro,kluke,marumaro,zola,all";

const kinds = {
    I: { name: "int", placeholder: (n) => `\${${n}:0}` },
    H: { name: "hex", placeholder: (n) => `\${${n}:0x0}` },
    F: { name: "float", placeholder: (n) => `\${${n}:0.0}` },
    V: { name: "var", placeholder: (n) => `var[\${${n}:0}]` },
    L: { name: "label", placeholder: (n) => `@\${${n}:0}` },
    C: { name: "party", placeholder: (n) => `\${${n}|${party}|}` },
};

const source = readFileSync(tablePath, "utf8");
const addLine = /^\s*Add\((\d+),\s*"(\w+)",\s*\d+((?:,\s*[A-Z]\("\w+"\))*)\);/gm;
const argSpec = /([A-Z])\("(\w+)"\)/g;

const snippets = {};
const data = [];
for (const [, opText, name, argText] of source.matchAll(addLine)) {
    const opcode = Number(opText);
    let args = [...argText.matchAll(argSpec)].map(([, kind, argName]) => {
        if (!kinds[kind]) throw new Error(`unknown arg kind ${kind} on ${name}`);
        return { kind: kinds[kind], name: argName };
    });
    if (name === "battle")
        args = args.filter((a) => !battleActionArgs.has(a.name));

    const signature = args.map((a) => `${a.name}: ${a.kind.name}`).join(", ");
    data.push({ name, opcode, sugared: sugared.has(opcode), args: args.map((a) => ({ name: a.name, kind: a.kind.name })) });
    if (sugared.has(opcode)) continue;

    snippets[name] = {
        prefix: name,
        description: `Opcode ${opcode}${signature ? ` - ${signature}` : ""}. Zero-valued arguments may be omitted.`,
        body: `${name}(${args.map((a, i) => `${a.name}=${a.kind.placeholder(i + 1)}`).join(", ")})`,
    };
}

if (data.length === 0)
    throw new Error(`no opcodes found in ${tablePath}`);

mkdirSync(dirname(dataPath), { recursive: true });
writeFileSync(snippetPath, JSON.stringify(snippets, null, 2) + "\n");
writeFileSync(dataPath, JSON.stringify(data, null, 2) + "\n");
console.log(`Wrote ${Object.keys(snippets).length} opcode snippets and ${data.length} opcode records`);
