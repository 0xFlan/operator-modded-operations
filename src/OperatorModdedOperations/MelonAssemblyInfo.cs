#if MELONLOADER
using MelonLoader;

[assembly: MelonInfo(
    typeof(CerberusNativeTabFix),
    "OPERATOR: Modded Operations",
    "0.3.35",
    "OPERATOR Modding Project")]
[assembly: MelonProcess("OPERATOR")]
[assembly: MelonPlatform(MelonPlatformAttribute.CompatiblePlatforms.WINDOWS_X64)]
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.IL2CPP)]
[assembly: HarmonyDontPatchAll]
[assembly: MelonAdditionalDependencies("OperatorModAPI.MelonLoader")]
#endif
