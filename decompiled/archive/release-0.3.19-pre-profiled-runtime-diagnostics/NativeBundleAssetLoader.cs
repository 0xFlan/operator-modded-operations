using System;
using System.Reflection;
using Il2CppInterop.Runtime;
using UnityEngine;

internal static class NativeBundleAssetLoader
{
	internal unsafe static Texture2D LoadTexture2D(AssetBundle bundle, string assetPath, out string diagnostic)
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
			IntPtr intPtr = FindNativeMethodInfo(typeof(AssetBundle), "NativeMethodInfoPtr_LoadAssetAsync_Internal_");
			if (intPtr == IntPtr.Zero)
			{
				diagnostic = "AssetBundle.LoadAssetAsync_Internal MethodInfo was unavailable.";
				return null;
			}
			IntPtr obj = IL2CPP.Il2CppObjectBaseToPtrNotNull(bundle);
			IntPtr intPtr2 = IL2CPP.Il2CppObjectBaseToPtrNotNull(Il2CppType.Of<Texture2D>());
			IntPtr intPtr3 = IL2CPP.ManagedStringToIl2Cpp(assetPath);
			IntPtr exc = IntPtr.Zero;
			void*[] array = new void*[2]
			{
				(void*)intPtr3,
				(void*)intPtr2
			};
			fixed (void** param = array)
			{
				IntPtr intPtr4 = IL2CPP.il2cpp_runtime_invoke(intPtr, obj, param, ref exc);
				if (exc != IntPtr.Zero)
				{
					diagnostic = "LoadAssetAsync_Internal raised a native exception.";
					return null;
				}
				if (intPtr4 == IntPtr.Zero)
				{
					diagnostic = "Unity returned a null AssetBundleRequest.";
					return null;
				}
				UnityEngine.Object asset = new AssetBundleRequest(intPtr4).asset;
				if (asset == null)
				{
					diagnostic = "The Texture2D request returned a null asset.";
					return null;
				}
				IntPtr intPtr5 = IL2CPP.Il2CppObjectBaseToPtr(asset);
				if (intPtr5 == IntPtr.Zero)
				{
					diagnostic = "The Texture2D request returned a null native pointer.";
					return null;
				}
				diagnostic = "Loaded through the Unity 6 native AssetBundle request.";
				return new Texture2D(intPtr5);
			}
		}
		catch (Exception ex)
		{
			diagnostic = ex.GetType().Name + ": " + ex.Message;
			return null;
		}
	}

	private static IntPtr FindNativeMethodInfo(Type declaringType, string fieldPrefix)
	{
		FieldInfo[] fields = declaringType.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		foreach (FieldInfo fieldInfo in fields)
		{
			if (fieldInfo.Name.StartsWith(fieldPrefix, StringComparison.Ordinal) && fieldInfo.GetValue(null) is IntPtr intPtr && intPtr != IntPtr.Zero)
			{
				return intPtr;
			}
		}
		return IntPtr.Zero;
	}
}
