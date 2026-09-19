using System.Reflection;
using EFT.CameraControl;
using SPTEssentials.Client;
using SPTEssentials.Client.Reticle;
using UnityEngine;

var colorProperty = Shader.PropertyToID("_Color");
var materialTrack = typeof(CustomReticleColorModule).GetMethod(
    "Track",
    BindingFlags.Instance | BindingFlags.NonPublic,
    null,
    new[] { typeof(Material) },
    null);
var opticTrack = typeof(CustomReticleColorModule).GetMethod(
    "Track",
    BindingFlags.Instance | BindingFlags.NonPublic,
    null,
    new[] { typeof(OpticSight) },
    null);
var activeOpticTrack = typeof(CustomReticleColorModule).GetMethod(
    "TrackActiveOptic",
    BindingFlags.Instance | BindingFlags.NonPublic);
var activeMaterialTrack = typeof(CustomReticleColorModule).GetMethod(
    "TrackActiveMaterial",
    BindingFlags.Instance | BindingFlags.NonPublic);
var enable = typeof(CustomReticleColorModule).GetMethod("Enable", BindingFlags.Instance | BindingFlags.NonPublic);
var passed = 0;

void Check(bool condition, string name)
{
    if (!condition)
    {
        throw new Exception("FAIL: " + name);
    }

    passed++;
    Console.WriteLine("PASS: " + name);
}

bool IsColor(Color actual, float red, float green, float blue, float alpha)
{
    return Math.Abs(actual.r - red) < 0.0001f
        && Math.Abs(actual.g - green) < 0.0001f
        && Math.Abs(actual.b - blue) < 0.0001f
        && Math.Abs(actual.a - alpha) < 0.0001f;
}

var converted = CustomReticleColorModule.BuildReticleColor(
    new Color(0.2f, 3f, 0.5f, 0.4f),
    new Color(0.25f, 0.5f, 1f, 0.1f));
Check(IsColor(converted, 0.75f, 1.5f, 3f, 0.4f), "selected hue keeps original HDR intensity and alpha");

var module = new CustomReticleColorModule();
enable.Invoke(module, null);

SPTEssentialsPlugin.Settings.EnableCustomReticleColor.Value = true;
SPTEssentialsPlugin.Settings.ReticleColor.Value = new Color(1f, 0f, 0f, 1f);
var material = new Material();
material.SeedColor(colorProperty, new Color(0f, 2f, 0f, 0.6f));
materialTrack.Invoke(module, new object[] { material });
Check(IsColor(material.GetColor(colorProperty), 2f, 0f, 0f, 0.6f), "tracked sight receives the global color");

SPTEssentialsPlugin.Settings.ReticleColor.Value = new Color(0f, 0.5f, 1f, 1f);
Check(IsColor(material.GetColor(colorProperty), 0f, 1f, 2f, 0.6f), "color changes update tracked sights immediately");

materialTrack.Invoke(module, new object[] { material });
SPTEssentialsPlugin.Settings.EnableCustomReticleColor.Value = false;
Check(IsColor(material.GetColor(colorProperty), 0f, 2f, 0f, 0.6f), "disabling restores the first observed material color");

SPTEssentialsPlugin.Settings.EnableCustomReticleColor.Value = true;
Check(IsColor(material.GetColor(colorProperty), 0f, 1f, 2f, 0.6f), "re-enabling reapplies the selected global color");

var unsupported = new Material { SupportsColor = false };
unsupported.SeedColor(colorProperty, new Color(0.1f, 0.2f, 0.3f, 1f));
materialTrack.Invoke(module, new object[] { unsupported });
Check(unsupported.SetCount == 0, "materials without _Color are ignored");

SPTEssentialsPlugin.Settings.ReticleColor.Value = new Color(0f, 1f, 0f, 1f);
var emissionColorProperty = Shader.PropertyToID("_EmissionColor");
var vertexMaterial = new Material { SupportsEmissionColor = true, name = "Crosshair" };
vertexMaterial.SeedColor(colorProperty, new Color(1f, 1f, 1f, 1f));
vertexMaterial.SeedColor(emissionColorProperty, new Color(0f, 0f, 0f, 1f));
var reticleMesh = new Mesh
{
    colors32 = new[]
    {
        new Color32(0, 0, 0, 255),
        new Color32(255, 0, 0, 220),
        new Color32(128, 64, 0, 110)
    }
};
var lensMaterial = new Material { SupportsMarkTexture = true };
lensMaterial.SeedColor(colorProperty, new Color(0.5f, 0.5f, 0.5f, 0.75f));
var markTextureProperty = Shader.PropertyToID("_MarkTex");
var markTexture = new Texture2D(3, 1, TextureFormat.RGBA32, false) { name = "test marks" };
markTexture.SetPixels32(new[]
{
    new Color32(0, 0, 0, 255),
    new Color32(200, 0, 10, 180),
    new Color32(90, 90, 90, 120)
});
lensMaterial.SeedTexture(markTextureProperty, markTexture);
var optic = new OpticSight
{
    ScopeData = new ScopeData
    {
        Reticle = new ScopeReticle { Material = vertexMaterial, Mesh = reticleMesh }
    },
    LensRenderer = new Renderer { sharedMaterials = new[] { lensMaterial } }
};
opticTrack.Invoke(module, new object[] { optic });
var changedVertices = reticleMesh.colors32;
Check(changedVertices[1].r == 255 && changedVertices[1].g == 0, "inactive scope mesh is left untouched until it is bound to the renderer");
Check(vertexMaterial.SetCount == 0, "vertex-colored reticles are not multiplied by a material tint");
Check(optic.ScopeData.Reticle.Material == vertexMaterial, "scope data keeps its original mesh-reticle material");
var opticRetrice = new OpticRetrice
{
    Renderer = new SkinnedMeshRenderer { sharedMesh = reticleMesh }
};
opticRetrice.SetOpticSight(optic);
activeMaterialTrack.Invoke(module, new object[] { opticRetrice });
var generatedReticleMaterial = opticRetrice.ActiveMaterialForTest;
Check(generatedReticleMaterial != vertexMaterial, "active renderer receives a generated material copy");
Check(IsColor(generatedReticleMaterial.GetColor(colorProperty), 0f, 0f, 0f, 1f), "mesh reticle base color is neutralized");
Check(IsColor(generatedReticleMaterial.GetColor(emissionColorProperty), 0f, 1f, 0f, 1f), "mesh reticle uses the selected emission color");
Check(generatedReticleMaterial.Keywords.Contains("_EMISSION"), "mesh reticle emission is enabled");
Check(IsColor(lensMaterial.GetColor(colorProperty), 0.5f, 0.5f, 0.5f, 0.75f), "marked lens material color is left untouched");
var generatedMarkTexture = (Texture2D)lensMaterial.GetTexture(markTextureProperty);
var changedPixels = generatedMarkTexture.GetPixels32();
Check(generatedMarkTexture != markTexture, "marked lens receives a generated texture copy");
Check(changedPixels[0].r == 0 && changedPixels[0].g == 0 && changedPixels[0].b == 0, "black texture pixels remain black");
Check(changedPixels[1].r == 0 && changedPixels[1].g == 200 && changedPixels[1].b == 0 && changedPixels[1].a == 180, "colored texture pixels use the selected color");
Check(changedPixels[2].r == 90 && changedPixels[2].g == 90 && changedPixels[2].b == 90 && changedPixels[2].a == 120, "neutral texture pixels remain unchanged");

activeOpticTrack.Invoke(module, new object[] { opticRetrice, optic });
Check(opticRetrice.Renderer.sharedMesh == reticleMesh, "mesh geometry remains untouched during active binding");
Check(SPTEssentialsPlugin.Log.Infos.Count == 2, "active optic and material bindings emit one diagnostic line each");

SPTEssentialsPlugin.Settings.EnableCustomReticleColor.Value = false;
var restoredVertices = reticleMesh.colors32;
Check(restoredVertices[1].r == 255 && restoredVertices[1].g == 0 && restoredVertices[2].r == 128 && restoredVertices[2].g == 64, "disabling restores original vertex colors");
Check(IsColor(lensMaterial.GetColor(colorProperty), 0.5f, 0.5f, 0.5f, 0.75f), "disabling restores the lens material color");
Check(lensMaterial.GetTexture(markTextureProperty) == markTexture, "disabling restores the original mark texture");
Check(opticRetrice.ActiveMaterialForTest == vertexMaterial, "disabling restores the original active material reference");
SPTEssentialsPlugin.Settings.EnableCustomReticleColor.Value = true;

module.Shutdown();
Check(IsColor(material.GetColor(colorProperty), 0f, 2f, 0f, 0.6f), "shutdown restores original colors");
var setCountAfterShutdown = material.SetCount;
SPTEssentialsPlugin.Settings.ReticleColor.Value = Color.red;
Check(material.SetCount == setCountAfterShutdown, "shutdown removes live configuration listeners");

Console.WriteLine($"{passed} checks passed. These are isolated module tests, not an EFT runtime test.");
