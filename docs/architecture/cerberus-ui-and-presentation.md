# Cerberus UI and mission presentation

## Native shell

Cerberus is the shipped mission-laptop location. Modded Operations is the
framework. The framework does not replace the complete laptop.

`MissionLaptop.osCanvas` and `MissionLaptop.uiRaycaster` are the physical
interactive surface. The framework discovers each `MissionLaptop` through the
closed IL2CPP generic resource route. It creates one binding per laptop
instance ID.

## Tab construction

The framework clones a shipped `Michsky.DreamOS.PanelButton`. The parent is
the live `Operation Selection` transform for that same laptop. The clone keeps
the shipped `Animator`, sprites, font, state roots, and pointer behavior.

Do not scale the complete button to fit text. Change only the title
`TMP_Text` fit settings. A complete hierarchy scale changes hit area and
animation geometry.

## Page construction

The framework creates a private page and private preparation page. It does
not append custom rows to `OperationsManager.ActiveOperations` or
`SimulationOperations`. This avoids shared integer-index conflicts when peers
have different local packages.

The core presentation object is:

```csharp
private sealed class CatalogPresentation
{
    public MissionLaptop Laptop;
    public GameObject Page;
    public GameObject HomeShell;
    public GameObject PreparationPanel;
    public OperationBoardUI Board;
    public CerebusOpboard NativeBoardData;
    public CerebusTargetPackage NativeTargetData;
    public GameObject NativeInfiltrationMapPrefab;
    public ModdedOperationDefinition SelectedOperation;
    public string SelectedTimeCode;
}
```

This type is in
[`CerberusNativeTabFix.cs`](../../src/OperatorModdedOperations/CerberusNativeTabFix.cs).

## Row and briefing

`SelectCatalogOperation` selects immutable catalog data.
`FormatCatalogBriefing` formats package text.
`UpdateCatalogOperationBoard` updates the private board.

The native board objects are clones. The framework replaces every
mission-bearing value. Do not leave a donor operation ID, marker index, target
package, or callback on a visible custom row.

## Infiltration selector

`BuildPackageInfiltrationMapPrefab` creates one package-owned map object.
Each `infiltrations[]` item clones a shipped `MapInfilMarker` visual. The code
then replaces:

- object ID;
- marker index;
- display name;
- maximum player count;
- ground, helicopter, and exfil flags;
- board owner;
- selected state.

The code assigns both anchor bounds from `mapPositionX` and `mapPositionY`.
It sets `anchoredPosition` to zero. `(0,0)` is lower-left. `(1,1)` is
upper-right.

These values position a two-dimensional UI marker. They do not place a player
in the Unity scene. `spawnSet` and scene marker transforms own player
placement.

## Preview image

`GetOrLoadPreviewSprite` reads only the Core-verified package file. The image
must be outside an AssetBundle. The code uses clamp wrapping and preserves the
image aspect ratio. `ReplaceNativeMapPreview` uses the same sprite in all
native map views.

An author changes the preview as a package update. An end user does not browse
to a private file at runtime under schema version 1.

## Physical verification

Use the world-space pointer. Do not call a UnityEvent directly as proof.
Test:

1. Active Operations.
2. Operation Simulation.
3. MODDED OPS.
4. Repeated tab switches.
5. Back.
6. Execute.
7. Cancel.
8. Confirm.
9. Close and reopen the laptop.
10. Return from an operation and repeat the flow.

The button and page must have the same `MissionLaptop` owner. The page must be
active in the hierarchy, not only active on itself.
