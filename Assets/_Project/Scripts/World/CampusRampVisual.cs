using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Replaces the collision-only train ramp with the authored Campus Rush ramp.
/// The original slope and cap colliders stay active, so visuals cannot change
/// the already validated route geometry.
/// </summary>
public sealed class CampusRampVisual : MonoBehaviour
{
    private const string RootName = "CampusRampArt";

    private void Awake()
    {
        if (transform.Find(RootName) != null) return;

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            renderer.enabled = false;

        GameObject source = Resources.Load<GameObject>("CampusRush/HeroKit/CR_TrainRamp");
        if (source == null) return;

        var artRoot = new GameObject(RootName).transform;
        artRoot.SetParent(transform, false);
        GameObject model = Instantiate(source, artRoot);
        model.name = "CR_TrainRamp";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = CampusRushModels.HeroAxisFix;
        model.transform.localScale = Vector3.one;

        foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
            Destroy(collider);

        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = true;
            renderer.sharedMaterial = HeroMaterial.For(renderer.gameObject.name);
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }
}

/// <summary>Small shared runtime palette for authored hero-kit meshes.</summary>
public static class HeroMaterial
{
    private static Material _brick, _cream, _cobalt, _teal, _gold, _glass, _metal, _leaf, _wood;

    public static Material For(string part)
    {
        Ensure();
        if (part.Contains("Brick") || part.Contains("Red")) return _brick;
        if (part.Contains("Cream") || part.Contains("White") || part.Contains("Stone")) return _cream;
        if (part.Contains("Cobalt")) return _cobalt;
        if (part.Contains("Teal")) return _teal;
        if (part.Contains("Gold")) return _gold;
        if (part.Contains("Glass")) return _glass;
        if (part.Contains("Leaf")) return _leaf;
        if (part.Contains("Wood")) return _wood;
        return _metal;
    }

    private static void Ensure()
    {
        if (_brick != null) return;
        _brick = Make(new Color(0.78f, 0.20f, 0.09f), 0.30f);
        _cream = Make(new Color(0.94f, 0.82f, 0.63f), 0.28f);
        _cobalt = Make(new Color(0.035f, 0.22f, 0.62f), 0.38f, 0.10f);
        _teal = Make(new Color(0.03f, 0.40f, 0.36f), 0.34f);
        _gold = Make(new Color(1f, 0.58f, 0.06f), 0.58f, 0.42f);
        _glass = Make(new Color(0.035f, 0.20f, 0.30f), 0.72f, 0.12f);
        _metal = Make(new Color(0.06f, 0.09f, 0.14f), 0.52f, 0.35f);
        _leaf = Make(new Color(0.24f, 0.52f, 0.14f), 0.22f);
        _wood = Make(new Color(0.34f, 0.14f, 0.05f), 0.25f);
    }

    private static Material Make(Color color, float smoothness, float metallic = 0f)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader) { enableInstancing = true };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        material.color = color;
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        return material;
    }
}
