internal sealed class SceneVariantCandidate
{
	internal string Id { get; }

	internal string ScenePath { get; }

	internal SceneVariantCandidate(string id, string scenePath)
	{
		Id = id;
		ScenePath = scenePath;
	}
}
