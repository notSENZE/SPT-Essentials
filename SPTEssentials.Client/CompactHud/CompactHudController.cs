using System;
using EFT;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using EFT.UI.Screens;
using UnityEngine;

namespace SPTEssentials.Client.CompactHud
{
    internal sealed class CompactHudController : IDisposable
    {
        private const float CardWidth = 176f;
        private const float CardHeight = 54f;
        private const float ScreenMargin = 8f;
        private const float StateUpdateInterval = 0.1f;
        private const float WorldSearchInterval = 0.5f;

        private static readonly EBodyPart[] HealthBodyParts =
        {
            EBodyPart.Head,
            EBodyPart.Chest,
            EBodyPart.Stomach,
            EBodyPart.LeftArm,
            EBodyPart.RightArm,
            EBodyPart.LeftLeg,
            EBodyPart.RightLeg
        };

        private static readonly EquipmentSlot[] GrenadeSlots =
        {
            EquipmentSlot.TacticalVest,
            EquipmentSlot.Pockets
        };

        private static readonly Color HealthColor = new Color(0.91f, 0.25f, 0.28f, 1f);
        private static readonly Color EnergyColor = new Color(0.96f, 0.66f, 0.18f, 1f);
        private static readonly Color HydrationColor = new Color(0.20f, 0.62f, 0.93f, 1f);
        private static readonly Color GrenadeColor = new Color(0.57f, 0.68f, 0.32f, 1f);

        private static readonly Vector2[] EnergyIconPoints =
        {
            new Vector2(0.57f, 0.96f),
            new Vector2(0.25f, 0.49f),
            new Vector2(0.46f, 0.49f),
            new Vector2(0.37f, 0.05f),
            new Vector2(0.76f, 0.59f),
            new Vector2(0.54f, 0.59f)
        };

        private static readonly Vector2[] HealthIconPoints =
        {
            new Vector2(0.16f, 0.65f),
            new Vector2(0.84f, 0.65f),
            new Vector2(0.50f, 0.07f)
        };

        private static readonly Vector2[] GrenadeLeverPoints =
        {
            new Vector2(0.39f, 0.72f),
            new Vector2(0.70f, 0.72f),
            new Vector2(0.80f, 0.84f),
            new Vector2(0.43f, 0.84f)
        };

        private GameWorld _gameWorld;
        private bool _hasSnapshot;
        private float _health;
        private float _maximumHealth;
        private float _energy;
        private float _hydration;
        private int _grenades;
        private float _nextStateUpdate;
        private float _nextWorldSearch;

        private bool _editMode;
        private bool _dragging;
        private Vector2 _position;
        private Vector2 _dragOffset;
        private CursorLockMode _previousCursorLockMode;
        private bool _previousCursorVisible;

        private GUIStyle _labelStyle;
        private GUIStyle _valueStyle;
        private GUIStyle _editStyle;
        private float _styledScale = -1f;

        private readonly Texture2D _healthIcon;
        private readonly Texture2D _energyIcon;
        private readonly Texture2D _hydrationIcon;
        private readonly Texture2D _grenadeIcon;

        internal CompactHudController()
        {
            _position = new Vector2(CompactHudSettings.PositionX.Value, CompactHudSettings.PositionY.Value);
            _healthIcon = CreateIconTexture("CompactHUD Health", IsInsideHealthIcon);
            _energyIcon = CreateIconTexture("CompactHUD Energy", IsInsideEnergyIcon);
            _hydrationIcon = CreateIconTexture("CompactHUD Hydration", IsInsideHydrationIcon);
            _grenadeIcon = CreateIconTexture("CompactHUD Grenade", IsInsideGrenadeIcon);
        }

        internal void Tick()
        {
            if (CompactHudSettings.ResetPositionKey.Value.IsDown())
            {
                ResetPosition();
            }

            if (CompactHudSettings.EditModeKey.Value.IsDown())
            {
                if (_editMode)
                {
                    SetEditMode(false);
                }
                else if (CompactHudSettings.Enabled.Value && _hasSnapshot)
                {
                    SetEditMode(true);
                }
            }

            if ((!CompactHudSettings.Enabled.Value || !_hasSnapshot) && _editMode)
            {
                SetEditMode(false);
            }

            float currentTime = Time.unscaledTime;
            if (currentTime < _nextStateUpdate)
            {
                return;
            }

            _nextStateUpdate = currentTime + StateUpdateInterval;
            UpdateSnapshot(currentTime);
        }

        internal void Draw()
        {
            if (!CompactHudSettings.Enabled.Value || !_hasSnapshot)
            {
                return;
            }

            float scale = CompactHudSettings.Scale.Value;
            float gap = CompactHudSettings.Gap.Value * scale;
            Vector2 layoutSize = GetLayoutSize(scale, gap);

            if (!_dragging)
            {
                _position = new Vector2(CompactHudSettings.PositionX.Value, CompactHudSettings.PositionY.Value);
            }

            _position = ClampPosition(_position, layoutSize);
            Rect layoutRect = new Rect(_position.x, _position.y, layoutSize.x, layoutSize.y);

            HandleDrag(layoutRect, layoutSize);
            EnsureStyles(scale);

            int previousDepth = GUI.depth;
            Color previousColor = GUI.color;
            GUI.depth = -1000;

            for (int index = 0; index < 4; index++)
            {
                Rect cardRect = GetCardRect(index, scale, gap);
                if (index == 0)
                {
                    DrawCard(cardRect, _healthIcon, "HEALTH", FormatHealth(), HealthColor, scale);
                }
                else if (index == 1)
                {
                    DrawCard(cardRect, _energyIcon, "ENERGY", FormatValue(_energy), EnergyColor, scale);
                }
                else if (index == 2)
                {
                    DrawCard(cardRect, _hydrationIcon, "HYDRATION", FormatValue(_hydration), HydrationColor, scale);
                }
                else
                {
                    DrawCard(cardRect, _grenadeIcon, "GRENADES", _grenades.ToString(), GrenadeColor, scale);
                }
            }

            if (_editMode)
            {
                DrawEditFrame(layoutRect, scale);
            }

            GUI.color = previousColor;
            GUI.depth = previousDepth;
        }

        public void Dispose()
        {
            SetEditMode(false);
            UnityEngine.Object.Destroy(_healthIcon);
            UnityEngine.Object.Destroy(_energyIcon);
            UnityEngine.Object.Destroy(_hydrationIcon);
            UnityEngine.Object.Destroy(_grenadeIcon);
        }

        private void UpdateSnapshot(float currentTime)
        {
            if (!IsHudScreenVisible())
            {
                _hasSnapshot = false;
                return;
            }

            if (_gameWorld == null)
            {
                if (currentTime < _nextWorldSearch)
                {
                    _hasSnapshot = false;
                    return;
                }

                _nextWorldSearch = currentTime + WorldSearchInterval;
                _gameWorld = UnityEngine.Object.FindObjectOfType<GameWorld>();
            }

            Player player = _gameWorld != null ? _gameWorld.MainPlayer : null;
            IHealthController healthController = player != null ? player.HealthController : null;
            if (player == null || healthController == null)
            {
                _hasSnapshot = false;
                if (_gameWorld != null && player == null)
                {
                    _gameWorld = null;
                }

                return;
            }

            float totalHealth = 0f;
            float totalMaximumHealth = 0f;
            foreach (EBodyPart bodyPart in HealthBodyParts)
            {
                ValueStruct bodyPartHealth = healthController.GetBodyPartHealth(bodyPart, false);
                totalHealth += bodyPartHealth.Current;
                totalMaximumHealth += bodyPartHealth.Maximum;
            }

            _health = totalHealth;
            _maximumHealth = totalMaximumHealth;
            _energy = healthController.Energy.Current;
            _hydration = healthController.Hydration.Current;
            _grenades = CountGrenades(player.Inventory);
            _hasSnapshot = true;
        }

        private static int CountGrenades(Inventory inventory)
        {
            if (inventory == null || inventory.Equipment == null)
            {
                return 0;
            }

            int count = 0;
            foreach (Item item in inventory.GetItemsInSlots(GrenadeSlots))
            {
                if (item is ThrowWeap grenade)
                {
                    count += Math.Max(1, grenade.StackObjectsCount);
                }
            }

            return count;
        }

        private void HandleDrag(Rect layoutRect, Vector2 layoutSize)
        {
            if (!_editMode)
            {
                _dragging = false;
                return;
            }

            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown &&
                currentEvent.button == 0 &&
                layoutRect.Contains(currentEvent.mousePosition))
            {
                _dragging = true;
                _dragOffset = currentEvent.mousePosition - _position;
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.MouseDrag && _dragging)
            {
                _position = ClampPosition(currentEvent.mousePosition - _dragOffset, layoutSize);
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0 && _dragging)
            {
                _dragging = false;
                SavePosition();
                currentEvent.Use();
            }
        }

        private Rect GetCardRect(int index, float scale, float gap)
        {
            float width = CardWidth * scale;
            float height = CardHeight * scale;
            if (CompactHudSettings.Orientation.Value == HudOrientation.Horizontal)
            {
                return new Rect(_position.x + index * (width + gap), _position.y, width, height);
            }

            return new Rect(_position.x, _position.y + index * (height + gap), width, height);
        }

        private static Vector2 GetLayoutSize(float scale, float gap)
        {
            float width = CardWidth * scale;
            float height = CardHeight * scale;
            if (CompactHudSettings.Orientation.Value == HudOrientation.Horizontal)
            {
                return new Vector2(width * 4f + gap * 3f, height);
            }

            return new Vector2(width, height * 4f + gap * 3f);
        }

        private static Vector2 ClampPosition(Vector2 position, Vector2 layoutSize)
        {
            float maximumX = Mathf.Max(ScreenMargin, Screen.width - layoutSize.x - ScreenMargin);
            float maximumY = Mathf.Max(ScreenMargin, Screen.height - layoutSize.y - ScreenMargin);
            return new Vector2(
                Mathf.Clamp(position.x, ScreenMargin, maximumX),
                Mathf.Clamp(position.y, ScreenMargin, maximumY));
        }

        private void DrawCard(Rect rect, Texture2D icon, string label, string value, Color accent, float scale)
        {
            float opacity = CompactHudSettings.BackgroundOpacity.Value;
            DrawRectangle(rect, new Color(0.035f, 0.042f, 0.052f, opacity));
            DrawRectangle(new Rect(rect.x, rect.y, 4f * scale, rect.height), accent);

            float iconSize = 34f * scale;
            Rect iconRect = new Rect(
                rect.x + 12f * scale,
                rect.y + (rect.height - iconSize) * 0.5f,
                iconSize,
                iconSize);
            DrawRectangle(iconRect, new Color(accent.r, accent.g, accent.b, 0.18f * opacity));

            float pictogramSize = 23f * scale;
            Rect pictogramRect = new Rect(
                iconRect.center.x - pictogramSize * 0.5f,
                iconRect.center.y - pictogramSize * 0.5f,
                pictogramSize,
                pictogramSize);
            GUI.DrawTexture(pictogramRect, icon, ScaleMode.ScaleToFit, true);

            float textX = iconRect.xMax + 10f * scale;
            float textWidth = rect.xMax - textX - 11f * scale;
            GUI.Label(
                new Rect(textX, rect.y + 6f * scale, textWidth, 17f * scale),
                label,
                _labelStyle);
            GUI.Label(
                new Rect(textX, rect.y + 20f * scale, textWidth, 28f * scale),
                value,
                _valueStyle);
        }

        private void DrawEditFrame(Rect layoutRect, float scale)
        {
            float lineWidth = Mathf.Max(1f, 2f * scale);
            Color frameColor = new Color(0.30f, 0.82f, 1f, 0.95f);
            DrawRectangle(new Rect(layoutRect.x, layoutRect.y, layoutRect.width, lineWidth), frameColor);
            DrawRectangle(new Rect(layoutRect.x, layoutRect.yMax - lineWidth, layoutRect.width, lineWidth), frameColor);
            DrawRectangle(new Rect(layoutRect.x, layoutRect.y, lineWidth, layoutRect.height), frameColor);
            DrawRectangle(new Rect(layoutRect.xMax - lineWidth, layoutRect.y, lineWidth, layoutRect.height), frameColor);

            float hintHeight = 22f * scale;
            float hintY = layoutRect.y >= hintHeight + 4f
                ? layoutRect.y - hintHeight - 4f
                : layoutRect.yMax + 4f;
            Rect hintRect = new Rect(layoutRect.x, hintY, Mathf.Min(layoutRect.width, 270f * scale), hintHeight);
            DrawRectangle(hintRect, new Color(0.035f, 0.042f, 0.052f, 0.96f));
            GUI.Label(hintRect, "DRAG HUD  |  F10 DONE  |  F9 RESET", _editStyle);
        }

        private void EnsureStyles(float scale)
        {
            if (_labelStyle != null && Mathf.Abs(_styledScale - scale) < 0.001f)
            {
                return;
            }

            _styledScale = scale;
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = Mathf.RoundToInt(10f * scale),
                fontStyle = FontStyle.Normal,
                normal = { textColor = new Color(0.70f, 0.74f, 0.78f, 1f) }
            };
            _valueStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = Mathf.RoundToInt(18f * scale),
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _editStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(10f * scale),
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.30f, 0.82f, 1f, 1f) }
            };
        }

        private void SetEditMode(bool enabled)
        {
            if (_editMode == enabled)
            {
                return;
            }

            _editMode = enabled;
            _dragging = false;
            if (enabled)
            {
                _previousCursorLockMode = Cursor.lockState;
                _previousCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = _previousCursorLockMode;
                Cursor.visible = _previousCursorVisible;
            }
        }

        private void ResetPosition()
        {
            _dragging = false;
            _position = new Vector2(CompactHudSettings.DefaultPositionX, CompactHudSettings.DefaultPositionY);
            SavePosition();
        }

        private void SavePosition()
        {
            CompactHudSettings.PositionX.Value = Mathf.Round(_position.x);
            CompactHudSettings.PositionY.Value = Mathf.Round(_position.y);
        }

        private string FormatHealth()
        {
            return $"{Mathf.RoundToInt(_health)} / {Mathf.RoundToInt(_maximumHealth)}";
        }

        private static string FormatValue(float value)
        {
            return Mathf.RoundToInt(value).ToString();
        }

        private static bool IsHudScreenVisible()
        {
            EftScreenManager screenManager = EftScreenManager.Instance;
            if (screenManager == null || screenManager.CurrentScreenController == null)
            {
                return false;
            }

            EEftScreenType screenType = screenManager.CurrentScreenController.ScreenType;
            return screenType == EEftScreenType.BattleUI || screenType == EEftScreenType.Hideout;
        }

        private static Texture2D CreateIconTexture(string name, Func<float, float, bool> isInside)
        {
            const int textureSize = 64;
            const int samplesPerAxis = 4;
            Color[] pixels = new Color[textureSize * textureSize];

            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    int coveredSamples = 0;
                    for (int sampleY = 0; sampleY < samplesPerAxis; sampleY++)
                    {
                        for (int sampleX = 0; sampleX < samplesPerAxis; sampleX++)
                        {
                            float normalizedX = (x + (sampleX + 0.5f) / samplesPerAxis) / textureSize;
                            float normalizedY = (y + (sampleY + 0.5f) / samplesPerAxis) / textureSize;
                            if (isInside(normalizedX, normalizedY))
                            {
                                coveredSamples++;
                            }
                        }
                    }

                    float alpha = coveredSamples / (float)(samplesPerAxis * samplesPerAxis);
                    pixels[y * textureSize + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static bool IsInsideHealthIcon(float x, float y)
        {
            float leftX = x - 0.35f;
            float rightX = x - 0.65f;
            float lobeY = y - 0.68f;
            float radiusSquared = 0.22f * 0.22f;
            bool leftLobe = leftX * leftX + lobeY * lobeY <= radiusSquared;
            bool rightLobe = rightX * rightX + lobeY * lobeY <= radiusSquared;
            return leftLobe || rightLobe || IsInsidePolygon(x, y, HealthIconPoints);
        }

        private static bool IsInsideEnergyIcon(float x, float y)
        {
            return IsInsidePolygon(x, y, EnergyIconPoints);
        }

        private static bool IsInsideHydrationIcon(float x, float y)
        {
            float deltaX = x - 0.5f;
            float deltaY = y - 0.35f;
            bool roundedBase = deltaX * deltaX + deltaY * deltaY <= 0.28f * 0.28f;

            if (y < 0.35f || y > 0.96f)
            {
                return roundedBase;
            }

            float taperWidth = (0.96f - y) * 0.46f;
            return roundedBase || Mathf.Abs(deltaX) <= taperWidth;
        }

        private static bool IsInsideGrenadeIcon(float x, float y)
        {
            bool body = IsInsideRoundedRectangle(x, y, 0.23f, 0.10f, 0.75f, 0.66f, 0.12f);
            bool neck = x >= 0.39f && x <= 0.60f && y >= 0.64f && y <= 0.76f;
            bool lever = IsInsidePolygon(x, y, GrenadeLeverPoints);

            float pinX = x - 0.77f;
            float pinY = y - 0.70f;
            float pinDistanceSquared = pinX * pinX + pinY * pinY;
            bool pin = pinDistanceSquared <= 0.12f * 0.12f && pinDistanceSquared >= 0.07f * 0.07f;
            return body || neck || lever || pin;
        }

        private static bool IsInsideRoundedRectangle(
            float x,
            float y,
            float left,
            float bottom,
            float right,
            float top,
            float radius)
        {
            float nearestX = Mathf.Clamp(x, left + radius, right - radius);
            float nearestY = Mathf.Clamp(y, bottom + radius, top - radius);
            float deltaX = x - nearestX;
            float deltaY = y - nearestY;
            return deltaX * deltaX + deltaY * deltaY <= radius * radius;
        }

        private static bool IsInsidePolygon(float x, float y, Vector2[] points)
        {
            bool inside = false;
            int previousIndex = points.Length - 1;
            for (int index = 0; index < points.Length; index++)
            {
                Vector2 current = points[index];
                Vector2 previous = points[previousIndex];
                bool crosses = (current.y > y) != (previous.y > y) &&
                    x < (previous.x - current.x) * (y - current.y) /
                    (previous.y - current.y) + current.x;
                if (crosses)
                {
                    inside = !inside;
                }

                previousIndex = index;
            }

            return inside;
        }

        private static void DrawRectangle(Rect rect, Color color)
        {
            Color previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }
    }
}
