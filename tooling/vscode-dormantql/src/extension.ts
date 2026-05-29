import * as vscode from 'vscode';

export function activate(context: vscode.ExtensionContext) {
  // This extension primarily contributes a grammar and language configuration.
  // No additional runtime logic is required for basic syntax highlighting.
  // Future versions may add Language Server support here for semantic tokens, diagnostics, etc.
  console.log('DormantQL extension activated');
}

export function deactivate() {}