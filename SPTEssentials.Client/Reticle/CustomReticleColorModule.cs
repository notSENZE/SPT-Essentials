using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using EFT.CameraControl;
using HarmonyLib;
using UnityEngine;

namespace SPTEssentials.Client.Reticle;

internal sealed class CustomReticleColorModule : ClientModule
{
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");
    private static readonly int HdrProperty = Shader.PropertyToID("_HDR");
    private static readonly int MarkTextureProperty = Shader.PropertyToID("_MarkTex");
    private static readonly int MarkLightnessProperty = Shader.PropertyToID("_MarkLightness");
    private static readonly FieldInfo ActiveMaterialField = AccessTools.Field(typeof(OpticRetrice), "_material");
    private static CustomReticleColorModule _instance;

    private readonly Harmony _harmony = new Harmony(SPTEssentialsPlugin.PluginGuid + ".reticlecolor");
    private readonly Dictionary<int, ReticleMaterialState> _materials = new Dictionary<int, ReticleMaterialState>();
    private readonly Dictionary<int, ReticleTextureState> _textures = new Dictionary<int, ReticleTextureState>();
    private readonly Dictionary<int, ActiveOpticMaterialState> _activeMaterials = new Dictionary<int, ActiveOpticMaterialState>();
    private readonly Dictionary<int, ActiveOpticMaterialState> _generatedActiveMaterials = new Dictionary<int, ActiveOpticMaterialState>();
    private readonly Dictionary<int, ActiveOpticBinding> _activeOptics = new Dictionary<int, ActiveOpticBinding>();
    private readonly Dictionary<int, CollimatorBinding> _collimators = new Dictionary<int, CollimatorBinding>();
    private readonly Dictionary<int, LensTextureBinding> _lensTextures = new Dictionary<int, LensTextureBinding>();
    private readonly HashSet<int> _failedTextureMaterials = new HashSet<int>();
    private readonly HashSet<int> _loggedOptics = new HashSet<int>();

    protected override string Name => "Custom Reticle Color";

    protected override void Enable()
    {
        _instance = this;
        SPTEssentialsPlugin.Settings.EnableCustomReticleColor.SettingChanged += OnSettingChanged;
        SPTEssentialsPlugin.Settings.ReticleColor.SettingChanged += OnSettingChanged;
        SPTEssentialsPlugin.Settings.ReticleIntensity.SettingChanged += OnSettingChanged;

        Patch(_harmony, typeof(CollimatorAwakePatch));
        Patch(_harmony, typeof(CollimatorEnabledPatch));
        Patch(_harmony, typeof(CollimatorLookAtPatch));
        Patch(_harmony, typeof(OpticAwakePatch));
        Patch(_harmony, typeof(OpticEnabledPatch));
        Patch(_harmony, typeof(ActiveOpticPatch));
        Patch(_harmony, typeof(ActiveOpticRenderPatch));
        Patch(_harmony, typeof(ScopePrefabAwakePatch));
        EnableNvgSightDimmerCompatibility();
    }

    internal void Shutdown()
    {
        SPTEssentialsPlugin.Settings.EnableCustomReticleColor.SettingChanged -= OnSettingChanged;
        SPTEssentialsPlugin.Settings.ReticleColor.SettingChanged -= OnSettingChanged;
        SPTEssentialsPlugin.Settings.ReticleIntensity.SettingChanged -= OnSettingChanged;
        RestoreOriginalState();
        _materials.Clear();
        DestroyGeneratedAssets();
        _textures.Clear();
        _activeMaterials.Clear();
        _generatedActiveMaterials.Clear();
        _activeOptics.Clear();
        _collimators.Clear();
        _lensTextures.Clear();
        _failedTextureMaterials.Clear();
        _loggedOptics.Clear();
        _harmony.UnpatchSelf();

        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void OnSettingChanged(object sender, EventArgs eventArgs)
    {
        ApplyCurrentSetting();
    }

    private void Track(Material material)
    {
        if (material == null || !material.HasProperty(ColorProperty))
        {
            return;
        }

        var materialId = material.GetInstanceID();
        if (!_materials.ContainsKey(materialId))
        {
            _materials.Add(
                materialId,
                new ReticleMaterialState(
                    material,
                    material.GetColor(ColorProperty),
                    material.HasProperty(HdrProperty),
                    material.HasProperty(HdrProperty) ? material.GetFloat(HdrProperty) : 0f));
        }

        ApplyCurrentSetting(_materials[materialId]);
    }

    private void Track(CollimatorSight sight)
    {
        if (sight == null)
        {
            return;
        }

        var renderer = sight.CollimatorMeshRenderer ?? sight.GetComponent<MeshRenderer>();
        var material = sight.CollimatorMaterial ?? renderer?.sharedMaterial;

        if (renderer == null || material == null || !material.HasProperty(ColorProperty))
        {
            return;
        }

        var sightId = sight.GetInstanceID();
        if (!_collimators.TryGetValue(sightId, out var binding))
        {
            binding = new CollimatorBinding(sight, renderer, material);
            _collimators.Add(sightId, binding);
        }

        ApplyCurrentSetting(binding);
    }

    private void Track(OpticSight sight)
    {
        if (sight == null)
        {
            return;
        }

        var reticle = sight.ScopeData?.Reticle;
        if (reticle?.Mesh == null)
        {
            Track(reticle?.Material);
        }

        var lensMaterials = sight.LensRenderer?.sharedMaterials;
        if (lensMaterials == null)
        {
            return;
        }

        foreach (var material in lensMaterials)
        {
            if (material != null && material.HasProperty(MarkTextureProperty))
            {
                var textureState = TrackMarkTexture(material);
                if (textureState == null)
                {
                    continue;
                }

                var rendererId = sight.LensRenderer.GetInstanceID();
                if (!_lensTextures.ContainsKey(rendererId))
                {
                    _lensTextures.Add(
                        rendererId,
                        new LensTextureBinding(sight.LensRenderer, textureState));
                }

                ApplyCurrentSetting(_lensTextures[rendererId]);
            }
        }
    }

    private ReticleTextureState TrackMarkTexture(Material material)
    {
        if (material == null)
        {
            return null;
        }

        var materialId = material.GetInstanceID();
        if (_textures.TryGetValue(materialId, out var existingState))
        {
            ApplyCurrentSetting(existingState);
            return existingState;
        }

        if (_failedTextureMaterials.Contains(materialId))
        {
            return null;
        }

        var originalTexture = material.GetTexture(MarkTextureProperty) as Texture2D;
        if (originalTexture == null)
        {
            _failedTextureMaterials.Add(materialId);
            return null;
        }

        try
        {
            var generatedTexture = CreateReadableCopy(originalTexture);
            var state = new ReticleTextureState(
                material,
                originalTexture,
                generatedTexture,
                generatedTexture.GetPixels32(),
                material.HasProperty(MarkLightnessProperty),
                material.HasProperty(MarkLightnessProperty)
                    ? material.GetFloat(MarkLightnessProperty)
                    : 0f);

            _textures.Add(materialId, state);
            ApplyCurrentSetting(state);
            return state;
        }
        catch (Exception exception)
        {
            _failedTextureMaterials.Add(materialId);
            SPTEssentialsPlugin.Log.LogWarning(
                $"Custom Reticle Color could not read '{originalTexture.name}': {exception.Message}");
            return null;
        }
    }

    private void EnableNvgSightDimmerCompatibility()
    {
        var pluginType = AccessTools.TypeByName("NVGSightDimmer.NVGSightDimmerPlugin");
        var updateSights = pluginType == null
            ? null
            : AccessTools.Method(pluginType, "UpdateSights");

        if (updateSights == null)
        {
            return;
        }

        var postfix = AccessTools.Method(
            typeof(NvgSightDimmerUpdatePatch),
            nameof(NvgSightDimmerUpdatePatch.Postfix));
        _harmony.Patch(updateSights, postfix: new HarmonyMethod(postfix));
        SPTEssentialsPlugin.Log.LogInfo("Custom Reticle Color: NVG Sight Dimmer compatibility enabled.");
    }

    private void ApplyAfterNvgSightDimmer()
    {
        if (!SPTEssentialsPlugin.Settings.EnableCustomReticleColor.Value)
        {
            return;
        }

        foreach (var binding in _collimators.Values)
        {
            if (binding.Renderer == null)
            {
                continue;
            }

            binding.Renderer.GetPropertyBlock(binding.WorkingPropertyBlock);
            var externalHdr = binding.HasHdrProperty
                ? binding.WorkingPropertyBlock.GetFloat(HdrProperty)
                : 0f;
            binding.ExternalHdr = externalHdr > 0f ? externalHdr : binding.OriginalHdr;
            ApplyCurrentSetting(binding);
        }

        foreach (var binding in _lensTextures.Values)
        {
            if (binding.Renderer != null)
            {
                ApplyCurrentSetting(binding);
            }
        }
    }

    private void TrackActiveOptic(OpticRetrice opticRetrice, OpticSight sight)
    {
        if (sight == null)
        {
            return;
        }

        Track(sight);

        if (!_loggedOptics.Add(sight.GetInstanceID()))
        {
            return;
        }

        var reticle = sight.ScopeData?.Reticle;
        var lensMaterials = sight.LensRenderer?.sharedMaterials;
        var markedLensCount = 0;
        if (lensMaterials != null)
        {
            foreach (var material in lensMaterials)
            {
                if (material != null && material.HasProperty(MarkTextureProperty))
                {
                    markedLensCount++;
                }
            }
        }

        SPTEssentialsPlugin.Log.LogInfo(
            $"Custom Reticle Color diagnostic: optic='{sight.name}', "
            + $"reticleMesh='{reticle?.Mesh?.name ?? "none"}', "
            + $"activeMesh='{opticRetrice?.Renderer?.sharedMesh?.name ?? "none"}', "
            + $"reticleMaterial='{reticle?.Material?.name ?? "none"}', "
            + $"markedLensMaterials={markedLensCount}, "
            + $"renderedMesh='{opticRetrice?.Renderer?.sharedMesh?.name ?? "none"}'."
        );
    }

    private void TrackActiveMaterial(OpticRetrice opticRetrice)
    {
        if (opticRetrice == null || ActiveMaterialField == null)
        {
            return;
        }

        var currentMaterial = ActiveMaterialField.GetValue(opticRetrice) as Material;
        if (currentMaterial == null)
        {
            return;
        }

        ActiveOpticMaterialState state;
        var currentMaterialId = currentMaterial.GetInstanceID();
        if (!_activeMaterials.TryGetValue(currentMaterialId, out state)
            && !_generatedActiveMaterials.TryGetValue(currentMaterialId, out state))
        {
            if (!currentMaterial.HasProperty(ColorProperty)
                || !currentMaterial.HasProperty(EmissionColorProperty))
            {
                return;
            }

            var generatedMaterial = new Material(currentMaterial)
            {
                name = currentMaterial.name + " (SPT Essentials)"
            };
            state = new ActiveOpticMaterialState(
                currentMaterial,
                generatedMaterial,
                currentMaterial.GetColor(ColorProperty));
            _activeMaterials.Add(currentMaterialId, state);
            _generatedActiveMaterials.Add(generatedMaterial.GetInstanceID(), state);

            SPTEssentialsPlugin.Log.LogInfo(
                $"Custom Reticle Color active material: original='{currentMaterial.name}', "
                + $"generated='{generatedMaterial.name}'.");
        }

        var opticId = opticRetrice.GetInstanceID();
        if (!_activeOptics.TryGetValue(opticId, out var binding) || binding.State != state)
        {
            binding = new ActiveOpticBinding(opticRetrice, state);
            _activeOptics[opticId] = binding;
        }

        ApplyCurrentSetting(binding);
    }

    private void ApplyCurrentSetting()
    {
        var staleMaterialIds = new List<int>();

        foreach (var pair in _materials)
        {
            if (pair.Value.Material == null)
            {
                staleMaterialIds.Add(pair.Key);
                continue;
            }

            ApplyCurrentSetting(pair.Value);
        }

        foreach (var materialId in staleMaterialIds)
        {
            _materials.Remove(materialId);
        }

        var staleTextureIds = new List<int>();

        foreach (var pair in _textures)
        {
            if (pair.Value.Material == null || pair.Value.GeneratedTexture == null)
            {
                pair.Value.DestroyGeneratedTexture();
                staleTextureIds.Add(pair.Key);
                continue;
            }

            ApplyCurrentSetting(pair.Value);
        }

        foreach (var textureId in staleTextureIds)
        {
            _textures.Remove(textureId);
        }

        var staleOpticIds = new List<int>();

        foreach (var pair in _activeOptics)
        {
            if (pair.Value.OpticRetrice == null
                || pair.Value.State.OriginalMaterial == null
                || pair.Value.State.GeneratedMaterial == null)
            {
                staleOpticIds.Add(pair.Key);
                continue;
            }

            ApplyCurrentSetting(pair.Value);
        }

        foreach (var opticId in staleOpticIds)
        {
            _activeOptics.Remove(opticId);
        }

        var staleCollimatorIds = new List<int>();
        foreach (var pair in _collimators)
        {
            if (pair.Value.Sight == null || pair.Value.Renderer == null || pair.Value.Material == null)
            {
                staleCollimatorIds.Add(pair.Key);
                continue;
            }

            ApplyCurrentSetting(pair.Value);
        }

        foreach (var sightId in staleCollimatorIds)
        {
            _collimators.Remove(sightId);
        }

        var staleLensIds = new List<int>();
        foreach (var pair in _lensTextures)
        {
            if (pair.Value.Renderer == null
                || pair.Value.TextureState.GeneratedTexture == null)
            {
                staleLensIds.Add(pair.Key);
                continue;
            }

            ApplyCurrentSetting(pair.Value);
        }

        foreach (var rendererId in staleLensIds)
        {
            _lensTextures.Remove(rendererId);
        }
    }

    private static void ApplyCurrentSetting(CollimatorBinding binding)
    {
        if (!SPTEssentialsPlugin.Settings.EnableCustomReticleColor.Value)
        {
            binding.Renderer.SetPropertyBlock(binding.OriginalPropertyBlock);
            return;
        }

        binding.Renderer.GetPropertyBlock(binding.WorkingPropertyBlock);
        binding.WorkingPropertyBlock.SetColor(
            ColorProperty,
            BuildReticleColor(
                binding.OriginalColor,
                SPTEssentialsPlugin.Settings.ReticleColor.Value,
                1f));

        if (binding.HasHdrProperty)
        {
            binding.WorkingPropertyBlock.SetFloat(
                HdrProperty,
                binding.ExternalHdr * SPTEssentialsPlugin.Settings.ReticleIntensity.Value);
        }

        binding.Renderer.SetPropertyBlock(binding.WorkingPropertyBlock);
    }

    private static void ApplyCurrentSetting(LensTextureBinding binding)
    {
        if (!SPTEssentialsPlugin.Settings.EnableCustomReticleColor.Value)
        {
            binding.Renderer.SetPropertyBlock(binding.OriginalPropertyBlock);
            return;
        }

        binding.Renderer.GetPropertyBlock(binding.WorkingPropertyBlock);
        binding.WorkingPropertyBlock.SetTexture(
            MarkTextureProperty,
            binding.TextureState.GeneratedTexture);
        binding.Renderer.SetPropertyBlock(binding.WorkingPropertyBlock);
    }

    private static void ApplyCurrentSetting(ReticleMaterialState state)
    {
        var enabled = SPTEssentialsPlugin.Settings.EnableCustomReticleColor.Value;
        var intensity = SPTEssentialsPlugin.Settings.ReticleIntensity.Value;
        var color = enabled
            ? BuildReticleColor(
                state.OriginalColor,
                SPTEssentialsPlugin.Settings.ReticleColor.Value,
                state.HasHdrProperty ? 1f : intensity)
            : state.OriginalColor;

        state.Material.SetColor(ColorProperty, color);
        if (state.HasHdrProperty)
        {
            state.Material.SetFloat(
                HdrProperty,
                enabled ? state.OriginalHdr * intensity : state.OriginalHdr);
        }
    }

    private static void ApplyCurrentSetting(ReticleTextureState state)
    {
        if (!SPTEssentialsPlugin.Settings.EnableCustomReticleColor.Value)
        {
            state.Material.SetTexture(MarkTextureProperty, state.OriginalTexture);
            if (state.HasMarkLightnessProperty)
            {
                state.Material.SetFloat(MarkLightnessProperty, state.OriginalMarkLightness);
            }
            return;
        }

        state.GeneratedTexture.SetPixels32(
            BuildReticleTextureColors(
                state.OriginalPixels,
                SPTEssentialsPlugin.Settings.ReticleColor.Value,
                SPTEssentialsPlugin.Settings.ReticleIntensity.Value));
        state.GeneratedTexture.Apply(true, false);
        state.Material.SetTexture(MarkTextureProperty, state.GeneratedTexture);
        if (state.HasMarkLightnessProperty)
        {
            state.Material.SetFloat(
                MarkLightnessProperty,
                state.OriginalMarkLightness * SPTEssentialsPlugin.Settings.ReticleIntensity.Value);
        }
    }

    private static void ApplyCurrentSetting(ActiveOpticBinding binding)
    {
        var enabled = SPTEssentialsPlugin.Settings.EnableCustomReticleColor.Value;
        if (enabled)
        {
            binding.State.Apply(
                SPTEssentialsPlugin.Settings.ReticleColor.Value,
                SPTEssentialsPlugin.Settings.ReticleIntensity.Value);
        }

        ActiveMaterialField.SetValue(
            binding.OpticRetrice,
            enabled ? binding.State.GeneratedMaterial : binding.State.OriginalMaterial);
    }

    private void RestoreOriginalState()
    {
        foreach (var state in _materials.Values)
        {
            if (state.Material != null)
            {
                state.Material.SetColor(ColorProperty, state.OriginalColor);
                if (state.HasHdrProperty)
                {
                    state.Material.SetFloat(HdrProperty, state.OriginalHdr);
                }
            }
        }

        foreach (var state in _textures.Values)
        {
            if (state.Material != null)
            {
                state.Material.SetTexture(MarkTextureProperty, state.OriginalTexture);
                if (state.HasMarkLightnessProperty)
                {
                    state.Material.SetFloat(MarkLightnessProperty, state.OriginalMarkLightness);
                }
            }
        }

        foreach (var binding in _activeOptics.Values)
        {
            if (binding.OpticRetrice != null && binding.State.OriginalMaterial != null)
            {
                ActiveMaterialField.SetValue(binding.OpticRetrice, binding.State.OriginalMaterial);
            }
        }

        foreach (var binding in _collimators.Values)
        {
            if (binding.Renderer != null)
            {
                binding.Renderer.SetPropertyBlock(binding.OriginalPropertyBlock);
            }
        }

        foreach (var binding in _lensTextures.Values)
        {
            if (binding.Renderer != null)
            {
                binding.Renderer.SetPropertyBlock(binding.OriginalPropertyBlock);
            }
        }
    }

    private void DestroyGeneratedAssets()
    {
        foreach (var state in _textures.Values)
        {
            state.DestroyGeneratedTexture();
        }

        foreach (var state in _activeMaterials.Values)
        {
            state.DestroyGeneratedMaterial();
        }
    }

    internal static Color BuildReticleColor(Color originalColor, Color selectedColor)
    {
        return BuildReticleColor(originalColor, selectedColor, 1f);
    }

    internal static Color BuildReticleColor(Color originalColor, Color selectedColor, float intensity)
    {
        var originalIntensity = Mathf.Max(originalColor.r, Mathf.Max(originalColor.g, originalColor.b));
        return new Color(
            selectedColor.r * originalIntensity * intensity,
            selectedColor.g * originalIntensity * intensity,
            selectedColor.b * originalIntensity * intensity,
            originalColor.a);
    }

    internal static Color32[] BuildReticleTextureColors(Color32[] originalColors, Color selectedColor)
    {
        return BuildReticleTextureColors(originalColors, selectedColor, 1f);
    }

    internal static Color32[] BuildReticleTextureColors(
        Color32[] originalColors,
        Color selectedColor,
        float intensity)
    {
        var colors = new Color32[originalColors.Length];

        for (var index = 0; index < originalColors.Length; index++)
        {
            var original = originalColors[index];
            var maximum = Math.Max(original.r, Math.Max(original.g, original.b));
            var minimum = Math.Min(original.r, Math.Min(original.g, original.b));

            if (maximum - minimum < 8)
            {
                colors[index] = original;
                continue;
            }

            colors[index] = new Color32(
                ToByte(selectedColor.r * maximum * intensity),
                ToByte(selectedColor.g * maximum * intensity),
                ToByte(selectedColor.b * maximum * intensity),
                ToByte(original.a * intensity));
        }

        return colors;
    }

    private static Texture2D CreateReadableCopy(Texture2D source)
    {
        var previousRenderTexture = RenderTexture.active;
        var temporaryRenderTexture = RenderTexture.GetTemporary(
            source.width,
            source.height,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Default);

        try
        {
            Graphics.Blit(source, temporaryRenderTexture);
            RenderTexture.active = temporaryRenderTexture;

            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true);
            copy.name = source.name + " (SPT Essentials)";
            copy.wrapMode = source.wrapMode;
            copy.filterMode = source.filterMode;
            copy.anisoLevel = source.anisoLevel;
            copy.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0, false);
            copy.Apply(true, false);
            return copy;
        }
        finally
        {
            RenderTexture.active = previousRenderTexture;
            RenderTexture.ReleaseTemporary(temporaryRenderTexture);
        }
    }

    private static byte ToByte(float value)
    {
        return (byte)Mathf.RoundToInt(Mathf.Clamp(value, 0f, 255f));
    }

    [HarmonyPatch(typeof(CollimatorSight), nameof(CollimatorSight.Awake))]
    private static class CollimatorAwakePatch
    {
        [HarmonyPostfix]
        private static void Postfix(CollimatorSight __instance)
        {
            _instance?.Track(__instance);
        }
    }

    [HarmonyPatch(typeof(CollimatorSight), nameof(CollimatorSight.OnEnable))]
    private static class CollimatorEnabledPatch
    {
        [HarmonyPostfix]
        private static void Postfix(CollimatorSight __instance)
        {
            _instance?.Track(__instance);
        }
    }

    [HarmonyPatch(typeof(CollimatorSight), nameof(CollimatorSight.LookAt))]
    private static class CollimatorLookAtPatch
    {
        [HarmonyPostfix]
        private static void Postfix(CollimatorSight __instance)
        {
            _instance?.Track(__instance);
        }
    }

    [HarmonyPatch(typeof(OpticSight), nameof(OpticSight.Awake))]
    private static class OpticAwakePatch
    {
        [HarmonyPostfix]
        private static void Postfix(OpticSight __instance)
        {
            _instance?.Track(__instance);
        }
    }

    [HarmonyPatch(typeof(OpticSight), nameof(OpticSight.OnEnable))]
    private static class OpticEnabledPatch
    {
        [HarmonyPostfix]
        private static void Postfix(OpticSight __instance)
        {
            _instance?.Track(__instance);
        }
    }

    [HarmonyPatch(typeof(OpticRetrice), nameof(OpticRetrice.SetOpticSight))]
    private static class ActiveOpticPatch
    {
        [HarmonyPrefix]
        private static void Prefix(OpticSight opticSight)
        {
            _instance?.Track(opticSight);
        }

        [HarmonyPostfix]
        private static void Postfix(OpticRetrice __instance, OpticSight opticSight)
        {
            _instance?.TrackActiveOptic(__instance, opticSight);
        }
    }

    [HarmonyPatch(typeof(OpticRetrice), nameof(OpticRetrice.OnPreCull))]
    private static class ActiveOpticRenderPatch
    {
        [HarmonyPrefix]
        private static void Prefix(OpticRetrice __instance)
        {
            _instance?.TrackActiveMaterial(__instance);
        }
    }

    [HarmonyPatch(typeof(ScopePrefabCache), nameof(ScopePrefabCache.Awake))]
    private static class ScopePrefabAwakePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ScopePrefabCache __instance)
        {
            if (_instance == null || __instance == null)
            {
                return;
            }

            foreach (var sight in __instance.GetComponentsInChildren<CollimatorSight>(true))
            {
                _instance.Track(sight);
            }

            foreach (var sight in __instance.GetComponentsInChildren<OpticSight>(true))
            {
                _instance.Track(sight);
            }
        }
    }

    private static class NvgSightDimmerUpdatePatch
    {
        internal static void Postfix()
        {
            _instance?.ApplyAfterNvgSightDimmer();
        }
    }

    private sealed class ReticleMaterialState
    {
        internal Material Material { get; }
        internal Color OriginalColor { get; }
        internal bool HasHdrProperty { get; }
        internal float OriginalHdr { get; }

        internal ReticleMaterialState(
            Material material,
            Color originalColor,
            bool hasHdrProperty,
            float originalHdr)
        {
            Material = material;
            OriginalColor = originalColor;
            HasHdrProperty = hasHdrProperty;
            OriginalHdr = originalHdr;
        }
    }

    private sealed class ReticleTextureState
    {
        internal Material Material { get; }
        internal Texture2D OriginalTexture { get; }
        internal Texture2D GeneratedTexture { get; }
        internal Color32[] OriginalPixels { get; }
        internal bool HasMarkLightnessProperty { get; }
        internal float OriginalMarkLightness { get; }

        internal ReticleTextureState(
            Material material,
            Texture2D originalTexture,
            Texture2D generatedTexture,
            Color32[] originalPixels,
            bool hasMarkLightnessProperty,
            float originalMarkLightness)
        {
            Material = material;
            OriginalTexture = originalTexture;
            GeneratedTexture = generatedTexture;
            OriginalPixels = (Color32[])originalPixels.Clone();
            HasMarkLightnessProperty = hasMarkLightnessProperty;
            OriginalMarkLightness = originalMarkLightness;
        }

        internal void DestroyGeneratedTexture()
        {
            if (GeneratedTexture != null)
            {
                UnityEngine.Object.Destroy(GeneratedTexture);
            }
        }
    }

    private sealed class ActiveOpticMaterialState
    {
        internal Material OriginalMaterial { get; }
        internal Material GeneratedMaterial { get; }
        private Color OriginalColor { get; }

        internal ActiveOpticMaterialState(
            Material originalMaterial,
            Material generatedMaterial,
            Color originalColor)
        {
            OriginalMaterial = originalMaterial;
            GeneratedMaterial = generatedMaterial;
            OriginalColor = originalColor;
        }

        internal void Apply(Color selectedColor, float intensity)
        {
            GeneratedMaterial.SetColor(
                ColorProperty,
                new Color(0f, 0f, 0f, OriginalColor.a));
            GeneratedMaterial.SetColor(
                EmissionColorProperty,
                new Color(
                    selectedColor.r * intensity,
                    selectedColor.g * intensity,
                    selectedColor.b * intensity,
                    1f));
            GeneratedMaterial.EnableKeyword("_EMISSION");
        }

        internal void DestroyGeneratedMaterial()
        {
            if (GeneratedMaterial != null)
            {
                UnityEngine.Object.Destroy(GeneratedMaterial);
            }
        }
    }

    private sealed class ActiveOpticBinding
    {
        internal OpticRetrice OpticRetrice { get; }
        internal ActiveOpticMaterialState State { get; }

        internal ActiveOpticBinding(OpticRetrice opticRetrice, ActiveOpticMaterialState state)
        {
            OpticRetrice = opticRetrice;
            State = state;
        }
    }

    private sealed class CollimatorBinding
    {
        internal CollimatorSight Sight { get; }
        internal MeshRenderer Renderer { get; }
        internal Material Material { get; }
        internal Color OriginalColor { get; }
        internal bool HasHdrProperty { get; }
        internal float OriginalHdr { get; }
        internal float ExternalHdr { get; set; }
        internal MaterialPropertyBlock OriginalPropertyBlock { get; }
        internal MaterialPropertyBlock WorkingPropertyBlock { get; }

        internal CollimatorBinding(CollimatorSight sight, MeshRenderer renderer, Material material)
        {
            Sight = sight;
            Renderer = renderer;
            Material = material;
            OriginalColor = material.GetColor(ColorProperty);
            HasHdrProperty = material.HasProperty(HdrProperty);
            OriginalHdr = HasHdrProperty ? material.GetFloat(HdrProperty) : 0f;
            ExternalHdr = OriginalHdr;
            OriginalPropertyBlock = new MaterialPropertyBlock();
            WorkingPropertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(OriginalPropertyBlock);
        }
    }

    private sealed class LensTextureBinding
    {
        internal Renderer Renderer { get; }
        internal ReticleTextureState TextureState { get; }
        internal MaterialPropertyBlock OriginalPropertyBlock { get; }
        internal MaterialPropertyBlock WorkingPropertyBlock { get; }

        internal LensTextureBinding(Renderer renderer, ReticleTextureState textureState)
        {
            Renderer = renderer;
            TextureState = textureState;
            OriginalPropertyBlock = new MaterialPropertyBlock();
            WorkingPropertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(OriginalPropertyBlock);
        }
    }
}
