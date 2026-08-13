internal sealed class SceneVariantSelection
{
	internal string Id { get; }

	internal string ScenePath { get; }

	internal int RemainingVariantCount { get; }

	internal SceneVariantSelection(string id, string scenePath, int remainingVariantCount)
	{
		Id = id;
		ScenePath = scenePath;
		RemainingVariantCount = remainingVariantCount;
	}
}
