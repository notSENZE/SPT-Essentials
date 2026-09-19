namespace BepInEx.Configuration
{
    public sealed class ConfigEntry<T>
    {
        private T _value;
        public event EventHandler SettingChanged;

        public ConfigEntry(T value)
        {
            _value = value;
        }

        public T Value
        {
            get => _value;
            set
            {
                _value = value;
                SettingChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}

namespace HarmonyLib
{
    using System.Reflection;

    public sealed class Harmony
    {
        public Harmony(string id) { }
        public void UnpatchSelf() { }
    }

    public sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch(Type type, string methodName) { }
    }

    public sealed class HarmonyPostfix : Attribute { }
    public sealed class HarmonyPrefix : Attribute { }

    public static class AccessTools
    {
        public static FieldInfo Field(Type type, string name)
        {
            return type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
    }
}

namespace UnityEngine
{
    public class Object
    {
        private static int _nextId;
        private readonly int _id = ++_nextId;

        public string name { get; set; }
        public int GetInstanceID() => _id;

        public static T Instantiate<T>(T original) where T : Object
        {
            if (original is Mesh mesh)
            {
                return new Mesh { name = mesh.name, colors32 = mesh.colors32 } as T;
            }

            throw new NotSupportedException();
        }

        public static void Destroy(Object value) { }
    }

    public struct Color
    {
        public float r, g, b, a;

        public Color(float red, float green, float blue, float alpha = 1f)
        {
            r = red;
            g = green;
            b = blue;
            a = alpha;
        }

        public static Color red => new Color(1f, 0f, 0f, 1f);
    }

    public struct Color32
    {
        public byte r, g, b, a;

        public Color32(byte red, byte green, byte blue, byte alpha)
        {
            r = red;
            g = green;
            b = blue;
            a = alpha;
        }
    }

    public static class Mathf
    {
        public static float Max(float first, float second) => Math.Max(first, second);
        public static float Clamp(float value, float minimum, float maximum) => Math.Min(maximum, Math.Max(minimum, value));
        public static int RoundToInt(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    public static class Shader
    {
        public static int PropertyToID(string name) => name.GetHashCode();
    }

    public sealed class Material : Object
    {
        private readonly Dictionary<int, Color> _colors = new Dictionary<int, Color>();
        private readonly Dictionary<int, Texture> _textures = new Dictionary<int, Texture>();

        public bool SupportsColor { get; set; } = true;
        public bool SupportsEmissionColor { get; set; }
        public bool SupportsMarkTexture { get; set; }
        public int SetCount { get; private set; }
        public int TextureSetCount { get; private set; }
        public HashSet<string> Keywords { get; } = new HashSet<string>();

        public Material() { }

        public Material(Material original)
        {
            SupportsColor = original.SupportsColor;
            SupportsEmissionColor = original.SupportsEmissionColor;
            SupportsMarkTexture = original.SupportsMarkTexture;
            name = original.name;
            foreach (var pair in original._colors)
            {
                _colors[pair.Key] = pair.Value;
            }
            foreach (var pair in original._textures)
            {
                _textures[pair.Key] = pair.Value;
            }
            foreach (var keyword in original.Keywords)
            {
                Keywords.Add(keyword);
            }
        }

        public bool HasProperty(int property)
        {
            return property == Shader.PropertyToID("_Color")
                ? SupportsColor
                : property == Shader.PropertyToID("_EmissionColor")
                    ? SupportsEmissionColor
                : property == Shader.PropertyToID("_MarkTex") && SupportsMarkTexture;
        }
        public Color GetColor(int property) => _colors[property];
        public void SeedColor(int property, Color color) => _colors[property] = color;
        public void SetColor(int property, Color color)
        {
            _colors[property] = color;
            SetCount++;
        }
        public Texture GetTexture(int property) => _textures.TryGetValue(property, out var texture) ? texture : null;
        public void SeedTexture(int property, Texture texture) => _textures[property] = texture;
        public void SetTexture(int property, Texture texture)
        {
            _textures[property] = texture;
            TextureSetCount++;
        }
        public void EnableKeyword(string keyword) => Keywords.Add(keyword);
    }

    public class Renderer : Object
    {
        public Material[] sharedMaterials;
        public Material sharedMaterial => sharedMaterials == null || sharedMaterials.Length == 0 ? null : sharedMaterials[0];
    }

    public sealed class MeshRenderer : Renderer { }

    public sealed class SkinnedMeshRenderer : Renderer
    {
        public Mesh sharedMesh;
    }

    public sealed class Mesh : Object
    {
        private Color32[] _colors = Array.Empty<Color32>();

        public Color32[] colors32
        {
            get => (Color32[])_colors.Clone();
            set => _colors = (Color32[])value.Clone();
        }
    }

    public enum TextureFormat { RGBA32 }
    public enum RenderTextureFormat { ARGB32 }
    public enum RenderTextureReadWrite { Default }
    public enum TextureWrapMode { Repeat, Clamp }
    public enum FilterMode { Point, Bilinear, Trilinear }

    public struct Rect
    {
        public Rect(float x, float y, float width, float height) { }
    }

    public class Texture : Object
    {
        internal Color32[] Pixels = Array.Empty<Color32>();
        public int width { get; protected set; }
        public int height { get; protected set; }
        public TextureWrapMode wrapMode { get; set; }
        public FilterMode filterMode { get; set; }
        public int anisoLevel { get; set; }
    }

    public sealed class Texture2D : Texture
    {
        public Texture2D(int width, int height, TextureFormat format, bool mipChain)
        {
            this.width = width;
            this.height = height;
            Pixels = new Color32[width * height];
        }

        public Color32[] GetPixels32() => (Color32[])Pixels.Clone();
        public void SetPixels32(Color32[] pixels) => Pixels = (Color32[])pixels.Clone();
        public void ReadPixels(Rect source, int destinationX, int destinationY, bool recalculateMipMaps)
        {
            Pixels = (Color32[])RenderTexture.active.Pixels.Clone();
        }
        public void Apply(bool updateMipmaps, bool makeNoLongerReadable) { }
    }

    public sealed class RenderTexture : Texture
    {
        public static RenderTexture active { get; set; }

        public static RenderTexture GetTemporary(
            int width,
            int height,
            int depthBuffer,
            RenderTextureFormat format,
            RenderTextureReadWrite readWrite)
        {
            return new RenderTexture { width = width, height = height, Pixels = new Color32[width * height] };
        }

        public static void ReleaseTemporary(RenderTexture temporary) { }
    }

    public static class Graphics
    {
        public static void Blit(Texture source, RenderTexture destination)
        {
            destination.Pixels = (Color32[])source.Pixels.Clone();
        }
    }
}

public sealed class CollimatorSight
{
    public UnityEngine.Material CollimatorMaterial;
    public UnityEngine.MeshRenderer CollimatorMeshRenderer;
    public T GetComponent<T>() where T : class => CollimatorMeshRenderer as T;
    public void Awake() { }
    public void OnEnable() { }
}

namespace EFT.CameraControl
{
    public sealed class ScopeReticle
    {
        public UnityEngine.Material Material;
        public UnityEngine.Mesh Mesh;
    }

    public sealed class ScopeData
    {
        public ScopeReticle Reticle;
    }

    public sealed class OpticSight
    {
        private static int _nextId;
        private readonly int _id = ++_nextId;
        public ScopeData ScopeData;
        public UnityEngine.Renderer LensRenderer;
        public string name = "test optic";
        public int GetInstanceID() => _id;
        public void Awake() { }
        public void OnEnable() { }
    }

    public sealed class OpticRetrice : UnityEngine.Object
    {
        private UnityEngine.Material _material;
        public UnityEngine.SkinnedMeshRenderer Renderer;
        public UnityEngine.Material ActiveMaterialForTest => _material;
        public void SetOpticSight(OpticSight sight) => _material = sight?.ScopeData?.Reticle?.Material;
        public void OnPreCull() { }
    }
}

public sealed class ScopePrefabCache
{
    public CollimatorSight[] Collimators = Array.Empty<CollimatorSight>();
    public EFT.CameraControl.OpticSight[] Optics = Array.Empty<EFT.CameraControl.OpticSight>();
    public void Awake() { }

    public T[] GetComponentsInChildren<T>(bool includeInactive)
    {
        if (typeof(T) == typeof(CollimatorSight))
        {
            return Collimators.Cast<T>().ToArray();
        }

        if (typeof(T) == typeof(EFT.CameraControl.OpticSight))
        {
            return Optics.Cast<T>().ToArray();
        }

        return Array.Empty<T>();
    }
}

namespace SPTEssentials.Client
{
    using BepInEx.Configuration;
    using UnityEngine;

    internal abstract class ClientModule
    {
        protected abstract string Name { get; }
        protected abstract void Enable();
        protected static void Patch(HarmonyLib.Harmony harmony, Type patchType) { }
    }

    internal sealed class TestSettings
    {
        internal ConfigEntry<bool> EnableCustomReticleColor { get; } = new ConfigEntry<bool>(true);
        internal ConfigEntry<Color> ReticleColor { get; } = new ConfigEntry<Color>(Color.red);
    }

    internal static class SPTEssentialsPlugin
    {
        internal const string PluginGuid = "test";
        internal static TestSettings Settings { get; } = new TestSettings();
        internal static TestLog Log { get; } = new TestLog();
    }

    internal sealed class TestLog
    {
        internal readonly List<string> Infos = new List<string>();
        internal readonly List<string> Warnings = new List<string>();
        internal void LogInfo(string message) => Infos.Add(message);
        internal void LogWarning(string message) => Warnings.Add(message);
    }
}
