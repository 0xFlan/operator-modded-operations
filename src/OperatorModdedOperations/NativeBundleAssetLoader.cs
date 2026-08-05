using System;
using System.Reflection;
using Il2CppInterop.Runtime;
using UnityEngine;

/// <summary>
/// Loads typed Unity assets through the installed Unity 6 IL2CPP binding.
/// The generated public AssetBundle wrapper is span-bound in this game build,
/// so package terrain textures use the same verified native method-info route
/// as the earlier Forest prototype.
/// </summary>
internal static unsafe class NativeBundleAssetLoader
{
    internal static Texture2D LoadTexture2D(
        AssetBundle bundle,
        string assetPath,
        out string diagnostic)
    {
        diagnostic = string.Empty;
        if (bundle == null)
        {
            diagnostic = "AssetBundle was null.";
            return null;
        }
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            diagnostic = "Texture asset path was empty.";
            return null;
        }

        try
        {
            IntPtr methodInfo = FindNativeMethodInfo(
                typeof(AssetBundle),
                "NativeMethodInfoPtr_LoadAssetAsync_Internal_");
            if (methodInfo == IntPtr.Zero)
            {
                diagnostic = "AssetBundle.LoadAssetAsync_Internal MethodInfo was unavailable.";
                return null;
            }

            IntPtr bundlePointer = IL2CPP.Il2CppObjectBaseToPtrNotNull(bundle);
            var type = Il2CppType.Of<Texture2D>();
            IntPtr typePointer = IL2CPP.Il2CppObjectBaseToPtrNotNull(type);
            IntPtr namePointer = IL2CPP.ManagedStringToIl2Cpp(assetPath);
            IntPtr exception = IntPtr.Zero;
            void*[] arguments =
            {
                (void*)namePointer,
                (void*)typePointer
            };
            fixed (void** argumentPointer = arguments)
            {
                IntPtr requestPointer = IL2CPP.il2cpp_runtime_invoke(
                    methodInfo,
                    bundlePointer,
                    argumentPointer,
                    ref exception);
                if (exception != IntPtr.Zero)
                {
                    diagnostic = "LoadAssetAsync_Internal raised a native exception.";
                    return null;
                }
                if (requestPointer == IntPtr.Zero)
                {
                    diagnostic = "Unity returned a null AssetBundleRequest.";
                    return null;
                }

                var request = new AssetBundleRequest(requestPointer);
                UnityEngine.Object asset = request.asset;
                if (asset == null)
                {
                    diagnostic = "The Texture2D request returned a null asset.";
                    return null;
                }
                IntPtr assetPointer = IL2CPP.Il2CppObjectBaseToPtr(asset);
                if (assetPointer == IntPtr.Zero)
                {
                    diagnostic = "The Texture2D request returned a null native pointer.";
                    return null;
                }
                diagnostic = "Loaded through the Unity 6 native AssetBundle request.";
                return new Texture2D(assetPointer);
            }
        }
        catch (Exception exception)
        {
            diagnostic = exception.GetType().Name + ": " + exception.Message;
            return null;
        }
    }

    private static IntPtr FindNativeMethodInfo(Type declaringType, string fieldPrefix)
    {
        foreach (FieldInfo field in declaringType.GetFields(
                     BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.Name.StartsWith(fieldPrefix, StringComparison.Ordinal) &&
                field.GetValue(null) is IntPtr pointer && pointer != IntPtr.Zero)
            {
                return pointer;
            }
        }
        return IntPtr.Zero;
    }
}
