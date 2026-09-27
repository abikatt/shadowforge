# BDSL for VS Code

Local VS Code extension for BDSL, the text form of RPJ scene scripts produced by
`sforge rpj decompile`. It provides:

- **Highlighting** for scene headers, entries, when-blocks, statements and waypoints.
- **Compile checks.** Each file is run through `sforge rpj check` on open, on save and while typing,
  and the compiler's first error is underlined on its line.
- **Outline** of the scene, entries, when-blocks, waypoints and labels (Outline view, breadcrumbs,
  `Ctrl+Shift+O`).
- **Hover** for opcodes (number and arguments), argument names, `@directives`, condition helpers
  and labels.
- **Completion** for argument names inside a call (only those not yet used), party members,
  `@directive` names and their keyword values, plus snippets for every opcode and the structural
  forms (`entry`, `when`, `if`, `ifoverflow`, `giveitem`, `battle`, `waypoint`, ...).
- **Labels:** go to definition (`F12`), find references (`Shift+F12`), highlight and rename (`F2`)
  for `@N`, scoped to the enclosing when-block.
- **BDSL: Compile to RPJ** in the command palette saves the file and writes an `.rpj` beside it.

## Comments

- `# note` is stripped on compile. **Toggle Line Comment** (`Ctrl+/`) uses it, so commenting out code
  removes it from the build.
- `// text` on its own line compiles to a comment opcode (5050), limited to 28 characters.
- `!statement` compiles the instruction with its disabled bit set. It is dimmed like a comment.

## Install

From this folder:

```sh
npm run install-local
```

This links the folder into `~/.vscode/extensions`, so there is nothing to package. Reload the window
after installing and after any change here. `npm run uninstall-local` removes the link.

Checks and compiles need `sforge`. The extension uses the `bdsl.sforgePath` setting if set, otherwise
the newest build under `src/ShadowForge.CLI/bin`, otherwise `sforge` on PATH. Build the CLI once with
`dotnet build src/ShadowForge.CLI`.

## Settings

| Setting | Default | |
| --- | --- | --- |
| `bdsl.sforgePath` | empty | Path to `sforge.exe`. |
| `bdsl.check.enabled` | `true` | Run compile checks. |
| `bdsl.check.onType` | `true` | Also check while typing, not only on open and save. |

## Layout

- `extension.js` wires up the features in `src/`. There are no npm dependencies.
- `src/structure.js` parses the block outline. It does not depend on `vscode`.
- `data/opcodes.json` and `snippets/opcodes.json` are generated from
  `src/ShadowForge.Core/Scene/Script/OpcodeTable.cs`. After changing the table, run
  `npm run generate` and reload the window.
- `samples/example.bdsl` exercises every construct the grammar handles and compiles cleanly.
