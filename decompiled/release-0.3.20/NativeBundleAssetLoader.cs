using System;
using System.Reflection;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

internal static class NativeBundleAssetLoader
{
	internal unsafe static Texture2D LoadTexture2D(AssetBundle bundle, string assetPath, out string diagnostic)
	{
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Expected O, but got Unknown
		diagnostic = string.Empty;
		if ((Object)(object)bundle == (Object)null)
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
			IntPtr intPtr2 = IL2CPP.Il2CppObjectBaseToPtrNotNull((Il2CppObjectBase)(object)bundle);
			IntPtr intPtr3 = IL2CPP.Il2CppObjectBaseToPtrNotNull((Il2CppObjectBase)(object)Il2CppType.Of<Texture2D>());
			IntPtr intPtr4 = IL2CPP.ManagedStringToIl2Cpp(assetPath);
			IntPtr zero = IntPtr.Zero;
			void*[] array = new void*[2]
			{
				(void*)intPtr4,
				(void*)intPtr3
			};
			fixed (void** ptr = array)
			{
				IntPtr intPtr5 = IL2CPP.il2cpp_runtime_invoke(intPtr, intPtr2, ptr, ref zero);
				if (zero != IntPtr.Zero)
				{
					diagnostic = "LoadAssetAsync_Internal raised a native exception.";
					return null;
				}
				if (intPtr5 == IntPtr.Zero)
				{
					diagnostic = "Unity returned a null AssetBundleRequest.";
					return null;
				}
				Object asset = new AssetBundleRequest(intPtr5).asset;
				if (asset == (Object)null)
				{
					diagnostic = "The Texture2D request returned a null asset.";
					return null;
				}
				IntPtr intPtr6 = IL2CPP.Il2CppObjectBaseToPtr((Il2CppObjectBase)(object)asset);
				if (intPtr6 == IntPtr.Zero)
				{
					diagnostic = "The Texture2D request returned a null native pointer.";
					return null;
				}
				diagnostic = "Loaded through the Unity 6 native AssetBundle request.";
				return new Texture2D(intPtr6);
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
