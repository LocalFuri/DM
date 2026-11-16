import * as vscode from 'vscode';
import { DotnetInstallMode, EventStream, IDotnetSearchContext, IEventStream } from 'vscode-dotnet-runtime-library';
/**
 * Tool name constants matching those in package.json
 */
export declare namespace ToolNames {
    const installSdk = "install_dotnet_sdk";
    const listVersions = "list_available_dotnet_versions_to_install";
    const recommendedSdkVersion = "recommended_dotnet_sdk_version";
    const listInstalledVersions = "list_installed_dotnet_versions";
    const findPath = "find_dotnet_executable_path";
    const uninstallSystemSdk = "uninstall_system_dotnet_sdk";
    const uninstallVSCodeRuntime = "uninstall_vscode_owned_dotnet_runtime";
    const getSettingsInfo = "get_settings_info_for_dotnet_installation_management";
}
/**
 * Determines whether a version string fully specifies an SDK patch version (e.g. "10.0.106"),
 * as opposed to a partial version ("10", "10.0"), a feature band ("10.0.1xx"), or a wildcard.
 * Only fully-specified versions can meaningfully mismatch the patch that actually gets installed.
 */
export declare function isFullySpecifiedSdkVersion(version: string | undefined, eventStream: IEventStream): version is string;
/**
 * Builds the `IDotnetSearchContext` the Language Model tools use to query `dotnet.availableInstalls`.
 * Exported so tests can assert the tool opts in to the findPath fallback (see `fallbackToFindPathInstalls`)
 * without having to drive the full command end to end.
 */
export declare function buildAvailableInstallsSearchContext(mode: DotnetInstallMode, dotnetExecutablePath?: string): IDotnetSearchContext;
/**
 * From a list of installed SDK versions, returns the highest fully-specified patch that shares the
 * same major.minor and feature band as the requested version, or undefined if none match.
 * This identifies the patch the Linux package manager actually installed when the requested one was unavailable.
 */
export declare function highestPatchInSameFeatureBand(requestedVersion: string, installedVersions: string[], eventStream: IEventStream): string | undefined;
/**
 * Computes the note appended to a successful SDK install result when the Linux package manager installed a
 * different patch than the one requested, or '' when no note is warranted.
 *
 * The note is emitted only when ALL of these hold:
 * - the install happened on Linux (the distro package manager only offers the latest patch in a feature band),
 * - the requested version fully specifies a patch (e.g. "10.0.106"),
 * - the requested patch is NOT among the installed SDKs, and
 * - a different, higher patch in the same feature band IS installed (the substitute the package manager chose).
 *
 * Extracted as a pure function (the only side effect is version-parse event posting) so the conditional logic
 * can be unit-tested without performing a real install.
 *
 * @param installedSdkVersions The SDK versions visible after the install, or undefined when that query failed.
 */
export declare function computeLinuxPatchMismatchNote(platform: NodeJS.Platform, requestedVersion: string | undefined, installedSdkVersions: string[] | undefined, eventStream: IEventStream): string;
/**
 * Normalizes the requested SDK version for the install platform.
 *
 * Linux distro package managers only expose the .1xx feature band, so a bare major ("6") or major.minor ("6.0")
 * request must be converted to the major.minor.1xx feature band (e.g. "6.0.1xx") before the distro install is
 * attempted. On Windows and macOS the version is returned unchanged, because their installers can target an
 * exact patch. Versions that already specify a patch or feature band are returned unchanged on every platform.
 *
 * Extracted as a pure function (taking `platform` explicitly rather than reading `process.platform`, and posting
 * only version-parse events) so the platform-specific normalization can be unit-tested without performing a real
 * install or stubbing the global platform.
 *
 * @param version The requested version (e.g. "6", "6.0", "6.0.301", "6.0.1xx").
 * @param platform The target platform; only 'linux' triggers feature-band normalization.
 */
export declare function resolveSdkVersionForInstall(version: string, platform: NodeJS.Platform, eventStream: IEventStream): string;
/**
 * Registers all Language Model Tools for the .NET Install Tool extension.
 * These tools enable AI agents (like GitHub Copilot) to help users manage .NET installations.
 */
export declare function registerLanguageModelTools(context: vscode.ExtensionContext, eventStream: EventStream): void;
