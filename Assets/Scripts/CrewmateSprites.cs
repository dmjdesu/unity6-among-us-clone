using System;
using UnityEngine;

namespace AmongUsClone
{
    internal static class CrewmateSprites
    {
        private const int Size = 96;

        private static Sprite _body;
        private static Sprite _visor;
        private static Sprite _backpack;
        private static Sprite _shadow;

        public static Sprite Body => _body ??= CreateSprite("Runtime Crewmate Body", IsBodyPixel);
        public static Sprite Visor => _visor ??= CreateSprite("Runtime Crewmate Visor", IsVisorPixel);
        public static Sprite Backpack => _backpack ??= CreateSprite("Runtime Crewmate Backpack", IsBackpackPixel);
        public static Sprite Shadow => _shadow ??= CreateSprite("Runtime Crewmate Shadow", IsShadowPixel);

        private static Sprite CreateSprite(string name, Func<float, float, bool> contains)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color[Size * Size];
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var normalizedX = (x + 0.5f) / Size - 0.5f;
                    var normalizedY = (y + 0.5f) / Size - 0.5f;
                    pixels[y * Size + x] = contains(normalizedX, normalizedY) ? Color.white : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, Size, Size),
                new Vector2(0.5f, 0.5f),
                Size,
                0,
                SpriteMeshType.FullRect);
        }

        private static bool IsBodyPixel(float x, float y)
        {
            var torso = RoundedRect(x, y, 0f, -0.08f, 0.62f, 0.72f, 0.18f);
            var helmet = Ellipse(x, y, 0f, 0.22f, 0.31f, 0.29f);
            var leftLeg = RoundedRect(x, y, -0.16f, -0.44f, 0.18f, 0.26f, 0.08f);
            var rightLeg = RoundedRect(x, y, 0.16f, -0.44f, 0.18f, 0.26f, 0.08f);
            return torso || helmet || leftLeg || rightLeg;
        }

        private static bool IsVisorPixel(float x, float y)
        {
            return RoundedRect(x, y, 0.04f, 0.12f, 0.5f, 0.23f, 0.11f);
        }

        private static bool IsBackpackPixel(float x, float y)
        {
            return RoundedRect(x, y, 0f, -0.02f, 0.34f, 0.58f, 0.13f);
        }

        private static bool IsShadowPixel(float x, float y)
        {
            return Ellipse(x, y, 0f, 0f, 0.42f, 0.16f);
        }

        private static bool Ellipse(float x, float y, float centerX, float centerY, float radiusX, float radiusY)
        {
            var normalizedX = (x - centerX) / radiusX;
            var normalizedY = (y - centerY) / radiusY;
            return normalizedX * normalizedX + normalizedY * normalizedY <= 1f;
        }

        private static bool RoundedRect(float x, float y, float centerX, float centerY, float width, float height, float radius)
        {
            var halfWidth = width * 0.5f;
            var halfHeight = height * 0.5f;
            var localX = Mathf.Abs(x - centerX);
            var localY = Mathf.Abs(y - centerY);

            if (localX > halfWidth || localY > halfHeight)
            {
                return false;
            }

            var cornerX = halfWidth - radius;
            var cornerY = halfHeight - radius;
            if (localX <= cornerX || localY <= cornerY)
            {
                return true;
            }

            var dx = localX - cornerX;
            var dy = localY - cornerY;
            return dx * dx + dy * dy <= radius * radius;
        }
    }
}
