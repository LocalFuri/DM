/**
 * Heuristically detects whether an error/installer message indicates the user cancelled or declined an
 * elevation/credential prompt. Kept private to this module: the more robust check in CommandExecutor's
 * parseVSCodeSudoExecError is preferred everywhere else, and this heuristic does not consider exit code 126.
 * "did not grant permission" comes from @vscode/sudo-prompt when the UAC dialog is dismissed on Windows.
 */
export declare function isUserCancellationMessage(message: string): boolean;
/**
 * Builds the message thrown for a failed `dotnet.uninstall` so the LLM-tool layer (and other callers) can
 * surface the real installer failure reason instead of a generic "another install may be in progress".
 */
export declare function buildUninstallFailureMessage(version: string, result: string): string;
